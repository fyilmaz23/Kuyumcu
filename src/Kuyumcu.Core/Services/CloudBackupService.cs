using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using Google.Apis.Auth.OAuth2.Responses;
using Kuyumcu.Platform;

namespace Kuyumcu.Services
{
    public enum CloudBackupHealth
    {
        /// <summary>Bu derlemede Drive bağlantısı tanımlı değil.</summary>
        NotConfigured,
        /// <summary>Google hesabı bağlanmamış.</summary>
        NotConnected,
        /// <summary>Oturum geçersiz; kullanıcı yeniden bağlanmalı.</summary>
        NeedsReconnect,
        /// <summary>Yedek güncel.</summary>
        UpToDate,
        /// <summary>Son deneme başarısız ama yakın zamanda başarılı yedek var / henüz hiç yedek yok.</summary>
        Warning,
        /// <summary>Uzun süredir (2 gün) başarılı yedek yok.</summary>
        Failing,
    }

    public sealed class CloudBackupStatus
    {
        public CloudBackupHealth Health { get; init; }
        public bool IsRunning { get; init; }
        /// <summary>Son başarılı yükleme.</summary>
        public DateTime? LastSuccessUtc { get; init; }
        /// <summary>Verinin buluttakiyle aynı olduğunun en son doğrulandığı an (yükleme veya "değişiklik yok").</summary>
        public DateTime? LastVerifiedUtc { get; init; }
        public string? LastError { get; init; }
        public string? AccountEmail { get; init; }
        public string? FolderId { get; init; }
        public string? LastFileName { get; init; }
    }

    public enum CloudBackupOutcome { Uploaded, Unchanged, NotConnected, Busy, Failed }

    public sealed record CloudBackupResult(CloudBackupOutcome Outcome, string Message);

    /// <summary>
    /// Google Drive'a tam otomatik yedek.
    ///
    /// - 15 dakikada bir bakar; son yedekten (ya da "değişiklik yok" kontrolünden) 2 saat geçmişse yeni bir anlık
    ///   kopya alır. Veri buluttaki son yedekle aynıysa (MD5) yüklemez.
    /// - Hata olursa 15 dakika sonra yeniden dener. Oturum geçersizse durur ve kullanıcıdan yeniden bağlanmasını ister.
    /// - Uygulama kapanırken (masaüstü) değişiklik varsa bir kez daha yükler.
    /// - Her başarılı yüklemede telefonun okuyacağı "son yedek" dosyası güncellenir ve saklama kuralı uygulanır
    ///   (en yeni 10 + son 14 günün her günü + son 12 ayın her ayı).
    /// Durum, uygulama klasöründeki cloud-backup.json dosyasında tutulur.
    /// </summary>
    public class CloudBackupService : IDisposable
    {
        public static readonly TimeSpan BackupInterval = TimeSpan.FromHours(2);
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan RetryAfterError = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan FailingAfter = TimeSpan.FromDays(2);
        private const int KeepRecent = 10, KeepDailyDays = 14, KeepMonthlyMonths = 12;

        private readonly DatabaseService _databaseService;
        private readonly GoogleDriveService _drive;
        private readonly IAppEnvironment _environment;
        private readonly SemaphoreSlim _runLock = new(1, 1);
        private readonly object _stateLock = new();
        private Timer? _timer;
        private State _state;
        private DriveConnectionState _connection = DriveConnectionState.NotConnected;
        private bool _isRunning;

        public CloudBackupService(DatabaseService databaseService, GoogleDriveService drive, IAppEnvironment environment)
        {
            _databaseService = databaseService;
            _drive = drive;
            _environment = environment;
            _state = LoadState();
        }

        /// <summary>Durum değişince (yedek başladı/bitti, bağlantı değişti) tetiklenir. Arka plan iş parçacığından gelebilir.</summary>
        public event Action? StatusChanged;

        private string StatePath => Path.Combine(_environment.AppDataDirectory, "cloud-backup.json");

        public CloudBackupStatus Status
        {
            get
            {
                lock (_stateLock)
                {
                    var lastVerified = Max(_state.LastSuccessUtc, _state.LastCheckUtc);
                    var errorIsLatest = _state.LastErrorUtc is { } e && (lastVerified == null || e > lastVerified);
                    var health = _connection switch
                    {
                        DriveConnectionState.NotConfigured => CloudBackupHealth.NotConfigured,
                        DriveConnectionState.NotConnected => CloudBackupHealth.NotConnected,
                        _ when _state.NeedsReconnect || _connection == DriveConnectionState.NeedsReconnect => CloudBackupHealth.NeedsReconnect,
                        _ when lastVerified == null => CloudBackupHealth.Warning,
                        _ when DateTime.UtcNow - lastVerified > FailingAfter => CloudBackupHealth.Failing,
                        _ when errorIsLatest => CloudBackupHealth.Warning,
                        _ => CloudBackupHealth.UpToDate,
                    };

                    return new CloudBackupStatus
                    {
                        Health = health,
                        IsRunning = _isRunning,
                        LastSuccessUtc = _state.LastSuccessUtc,
                        LastVerifiedUtc = lastVerified,
                        LastError = errorIsLatest || _state.NeedsReconnect ? _state.LastError : null,
                        AccountEmail = _state.AccountEmail,
                        FolderId = _state.FolderId,
                        LastFileName = _state.LastFileName,
                    };
                }
            }
        }

        /// <summary>Otomatik yedeklemeyi başlatır (masaüstü açılışında).</summary>
        public void Start()
        {
            _ = RefreshConnectionAsync();
            _timer = new Timer(_ => _ = TickAsync(), null, TimeSpan.FromMinutes(1), CheckInterval);
        }

        /// <summary>Bağlantı durumunu yeniden okur (ağa çıkmaz).</summary>
        public async Task RefreshConnectionAsync()
        {
            try
            {
                _connection = await _drive.GetConnectionStateAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Drive bağlantı durumu okunamadı: {ex.Message}");
            }
            StatusChanged?.Invoke();
        }

        /// <summary>Google hesabını bağlar (tarayıcı açılır) ve hemen ilk yedeği başlatır.</summary>
        public async Task<string?> ConnectAsync(CancellationToken cancellationToken)
        {
            var email = await _drive.ConnectAsync(cancellationToken);
            lock (_stateLock)
            {
                _state.AccountEmail = email;
                _state.NeedsReconnect = false;
                _state.LastError = null;
                _state.LastErrorUtc = null;
            }
            SaveState();
            _connection = DriveConnectionState.Connected;
            StatusChanged?.Invoke();

            _ = Task.Run(() => BackupNowAsync(force: true));
            return email;
        }

        /// <summary>Bağlantıyı keser. Drive'daki yedekler silinmez.</summary>
        public async Task DisconnectAsync()
        {
            await _drive.DisconnectAsync();
            lock (_stateLock)
            {
                _state.AccountEmail = null;
                _state.NeedsReconnect = false;
                _state.LastError = null;
                _state.LastErrorUtc = null;
            }
            SaveState();
            _connection = DriveConnectionState.NotConnected;
            StatusChanged?.Invoke();
        }

        /// <summary>
        /// Hemen yedekler. <paramref name="force"/> false ise veri buluttakiyle aynıysa yüklemez.
        /// </summary>
        public async Task<CloudBackupResult> BackupNowAsync(bool force = false, CancellationToken cancellationToken = default)
        {
            if (!await _runLock.WaitAsync(0, cancellationToken))
                return new(CloudBackupOutcome.Busy, "Yedekleme zaten sürüyor.");

            var snapshotPath = Path.Combine(_environment.CacheDirectory, $"bulut-yedek-{Guid.NewGuid():N}.db3");
            SetRunning(true);
            try
            {
                _connection = await _drive.GetConnectionStateAsync();
                if (_connection != DriveConnectionState.Connected)
                    return new(CloudBackupOutcome.NotConnected, _connection == DriveConnectionState.NotConfigured
                        ? "Google Drive bağlantısı bu derlemede tanımlı değil."
                        : "Google Drive hesabı bağlı değil. Ayarlar'dan bağlayın.");

                // Kurulumu tamamlanmamış (boş) veritabanı Drive'a yüklenmez: yeniden kurulumda Drive'daki "son yedek"
                // boş veriyle ezilmesin (telefon da onu indirirdi).
                if (!await _databaseService.IsSetupCompletedAsync())
                    return new(CloudBackupOutcome.Unchanged, "Kurulum tamamlanmadı; yedek alınmadı.");

                Directory.CreateDirectory(_environment.CacheDirectory);
                if (!await _databaseService.CreateBackupCopyAsync(snapshotPath))
                    throw new IOException("Veritabanının anlık kopyası alınamadı.");
                var md5 = ComputeMd5(snapshotPath);

                string? folderId, latestFileId;
                lock (_stateLock)
                {
                    if (!force && md5 == _state.LastUploadedMd5)
                    {
                        _state.LastCheckUtc = DateTime.UtcNow;
                        _state.LastError = null;
                        _state.LastErrorUtc = null;
                        SaveStateLocked();
                        return new(CloudBackupOutcome.Unchanged, "Değişiklik yok; buluttaki yedek güncel.");
                    }
                    folderId = _state.FolderId;
                    latestFileId = _state.LatestFileId;
                }

                folderId = await _drive.EnsureBackupFolderAsync(folderId, cancellationToken);
                var takenAt = DateTime.Now;
                var uploaded = await _drive.UploadSnapshotAsync(folderId, snapshotPath, md5, takenAt, cancellationToken);

                // Telefonun okuyacağı dosya ve saklama kuralı: başarısız olsalar da yedek alınmış sayılır
                try { latestFileId = await _drive.UpdateLatestAsync(folderId, latestFileId, snapshotPath, cancellationToken); }
                catch (Exception ex) when (ex is not OperationCanceledException) { Console.WriteLine($"Son yedek dosyası güncellenemedi: {ex.Message}"); }
                try { await _drive.ApplyRetentionAsync(folderId, KeepRecent, KeepDailyDays, KeepMonthlyMonths, cancellationToken); }
                catch (Exception ex) when (ex is not OperationCanceledException) { Console.WriteLine($"Eski bulut yedekleri temizlenemedi: {ex.Message}"); }

                string? email;
                lock (_stateLock) email = _state.AccountEmail;
                if (string.IsNullOrEmpty(email))
                {
                    try { email = await _drive.GetAccountEmailAsync(cancellationToken); } catch { /* sadece bilgi */ }
                }

                lock (_stateLock)
                {
                    _state.FolderId = folderId;
                    _state.LatestFileId = latestFileId;
                    _state.LastUploadedMd5 = md5;
                    _state.LastSuccessUtc = _state.LastCheckUtc = DateTime.UtcNow;
                    _state.LastFileName = uploaded.FileName;
                    _state.AccountEmail = email;
                    _state.LastError = null;
                    _state.LastErrorUtc = null;
                    _state.NeedsReconnect = false;
                    SaveStateLocked();
                }
                return new(CloudBackupOutcome.Uploaded, $"Google Drive'a yedeklendi ({uploaded.FileName}).");
            }
            catch (Exception ex) when (IsAuthFailure(ex))
            {
                _drive.ResetConnection();
                RecordError("Google oturumu artık geçerli değil. Ayarlar'dan Google Drive'a yeniden bağlanın.", needsReconnect: true);
                return new(CloudBackupOutcome.Failed, Status.LastError!);
            }
            catch (GoogleDriveService.DriveNotConnectedException ex)
            {
                return new(CloudBackupOutcome.NotConnected, ex.Message);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new(CloudBackupOutcome.Failed, "Yedekleme yarıda kesildi.");
            }
            catch (Exception ex)
            {
                var message = ex is HttpRequestException or TaskCanceledException
                    ? "İnternete bağlanılamadı; yedek daha sonra yeniden denenecek."
                    : $"Bulut yedeği alınamadı: {ex.Message}";
                RecordError(message, needsReconnect: false);
                return new(CloudBackupOutcome.Failed, message);
            }
            finally
            {
                try { if (File.Exists(snapshotPath)) File.Delete(snapshotPath); } catch { /* geçici dosya */ }
                SetRunning(false);
                _runLock.Release();
            }
        }

        /// <summary>Kapanışta: değişiklik varsa yükler, en fazla <paramref name="timeout"/> bekler.</summary>
        public async Task FlushOnExitAsync(TimeSpan timeout)
        {
            _timer?.Dispose();
            _timer = null;
            if (_connection != DriveConnectionState.Connected || _state.NeedsReconnect)
                return;

            using var cts = new CancellationTokenSource(timeout);
            try
            {
                await BackupNowAsync(force: false, cts.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Kapanış yedeği alınamadı: {ex.Message}");
            }
        }

        private async Task TickAsync()
        {
            try
            {
                if (!IsDue())
                    return;
                await BackupNowAsync(force: false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Otomatik bulut yedeği hata: {ex.Message}");
            }
        }

        private bool IsDue()
        {
            if (_connection != DriveConnectionState.Connected)
            {
                // Bağlantı başka bir yerde kurulmuş olabilir (ör. Ayarlar'dan): bir sonraki turda tekrar bak
                _ = RefreshConnectionAsync();
                return false;
            }

            lock (_stateLock)
            {
                if (_state.NeedsReconnect)
                    return false;

                var now = DateTime.UtcNow;
                var lastVerified = Max(_state.LastSuccessUtc, _state.LastCheckUtc);
                if (_state.LastErrorUtc is { } error && (lastVerified == null || error > lastVerified))
                    return now - error >= RetryAfterError;
                return lastVerified == null || now - lastVerified >= BackupInterval;
            }
        }

        private static bool IsAuthFailure(Exception ex) =>
            ex is TokenResponseException
            || ex.InnerException is TokenResponseException
            || ex is Google.GoogleApiException { HttpStatusCode: System.Net.HttpStatusCode.Unauthorized };

        private void RecordError(string message, bool needsReconnect)
        {
            lock (_stateLock)
            {
                _state.LastError = message;
                _state.LastErrorUtc = DateTime.UtcNow;
                _state.NeedsReconnect |= needsReconnect;
                SaveStateLocked();
            }
            Console.WriteLine($"Bulut yedeği: {message}");
        }

        private void SetRunning(bool running)
        {
            _isRunning = running;
            StatusChanged?.Invoke();
        }

        private static string ComputeMd5(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(MD5.HashData(stream)).ToLowerInvariant();
        }

        private static DateTime? Max(DateTime? a, DateTime? b) => a == null ? b : b == null ? a : (a > b ? a : b);

        // ---- Durum dosyası ----

        private sealed class State
        {
            public DateTime? LastSuccessUtc { get; set; }
            public DateTime? LastCheckUtc { get; set; }
            public string? LastUploadedMd5 { get; set; }
            public string? LastError { get; set; }
            public DateTime? LastErrorUtc { get; set; }
            public bool NeedsReconnect { get; set; }
            public string? AccountEmail { get; set; }
            public string? FolderId { get; set; }
            public string? LatestFileId { get; set; }
            public string? LastFileName { get; set; }
        }

        private State LoadState()
        {
            try
            {
                if (File.Exists(StatePath))
                    return JsonSerializer.Deserialize<State>(File.ReadAllText(StatePath)) ?? new State();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"cloud-backup.json okunamadı: {ex.Message}");
            }
            return new State();
        }

        private void SaveState()
        {
            lock (_stateLock)
                SaveStateLocked();
        }

        private void SaveStateLocked()
        {
            try
            {
                File.WriteAllText(StatePath, JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"cloud-backup.json yazılamadı: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
            _timer = null;
        }
    }
}
