using Kuyumcu.Platform;
using Kuyumcu.Services;
using Microsoft.Extensions.Logging;

namespace Kuyumcu.Droid.Services;

public enum SyncOutcome
{
    /// <summary>Yeni yedek indirildi.</summary>
    Updated,
    /// <summary>Drive'daki yedek telefondakiyle aynı.</summary>
    UpToDate,
    /// <summary>Drive'da bu uygulamanın yedeği yok.</summary>
    NoBackupOnDrive,
    /// <summary>Google hesabıyla bağlanmak gerekiyor.</summary>
    NeedsConnection,
    /// <summary>İnternet yok.</summary>
    Offline,
    /// <summary>Kullanıcı vazgeçti.</summary>
    Cancelled,
    Failed,
}

public sealed record SyncResult(SyncOutcome Outcome, string Message, DateTimeOffset At)
{
    public bool IsError => Outcome is SyncOutcome.Failed or SyncOutcome.NoBackupOnDrive or SyncOutcome.NeedsConnection;
}

/// <summary>
/// Drive'daki son yedeği telefona getirir. Açılışta ve uygulamaya her dönüşte sessizce yeni yedek var mı bakar;
/// "Yenile" ile elle de tetiklenir. İnternet yoksa telefondaki son yedek gösterilmeye devam eder.
/// </summary>
public sealed class BackupSyncService
{
    private const string ConnectedKey = "google_connected";
    private const string EmailKey = "google_email";

    /// <summary>Uygulamaya dönüşte en fazla bu sıklıkla otomatik kontrol yapılır.</summary>
    private static readonly TimeSpan AutoCheckInterval = TimeSpan.FromMinutes(2);

    private readonly IGoogleDriveAuthorizer _authorizer;
    private readonly DriveLatestBackupClient _drive;
    private readonly BackupStore _store;
    private readonly ILogger<BackupSyncService> _logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private DateTimeOffset _lastAutoCheck = DateTimeOffset.MinValue;

    public BackupSyncService(IGoogleDriveAuthorizer authorizer, DriveLatestBackupClient drive, BackupStore store, ILogger<BackupSyncService> logger)
    {
        _authorizer = authorizer;
        _drive = drive;
        _store = store;
        _logger = logger;
    }

    /// <summary>Kullanıcı Google hesabıyla bağlandı mı (bu telefonda)?</summary>
    public bool IsConnected => Preferences.Default.Get(ConnectedKey, false);

    public string? AccountEmail => Preferences.Default.Get<string?>(EmailKey, null);

    /// <summary>Bağlıydı ama son denemede Google izni geçersiz çıktı: kullanıcı yeniden bağlanmalı.</summary>
    public bool NeedsReconnect => IsConnected && LastResult?.Outcome == SyncOutcome.NeedsConnection;

    public bool IsBusy { get; private set; }

    /// <summary>Son eşitlemenin sonucu (ekranda bilgi/hata göstermek için).</summary>
    public SyncResult? LastResult { get; private set; }

    public event Action? Changed;

    /// <summary>Google hesabı seçtirip izin alır ve son yedeği indirir.</summary>
    public Task<SyncResult> ConnectAsync() => RefreshAsync(interactive: true, chooseAccount: true);

    /// <summary>
    /// Açılışta / uygulamaya dönüşte: bağlıysa ve son kontrolün üzerinden yeterince zaman geçtiyse sessizce bakar.
    /// </summary>
    public async Task AutoRefreshAsync()
    {
        await _store.EnsureLoadedAsync();
        if (!IsConnected || IsBusy || DateTimeOffset.Now - _lastAutoCheck < AutoCheckInterval)
            return;

        _lastAutoCheck = DateTimeOffset.Now;
        await RefreshAsync(interactive: false);
    }

    /// <summary>Drive'daki son yedeği kontrol eder; yeniyse indirir.</summary>
    /// <param name="interactive">Gerekirse Google hesap/izin ekranı açılsın mı?</param>
    /// <param name="chooseAccount">Hesap seçici her durumda açılsın mı? (Bağlan düğmesi)</param>
    public async Task<SyncResult> RefreshAsync(bool interactive, bool chooseAccount = false)
    {
        if (!await _syncLock.WaitAsync(0))
            return LastResult ?? Result(SyncOutcome.UpToDate, "Kontrol ediliyor…");

        SetBusy(true);
        string? tempPath = null;
        try
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return Finish(SyncOutcome.Offline, "İnternet yok. Telefondaki son yedek gösteriliyor.");

            string token;
            try
            {
                token = await _authorizer.GetAccessTokenAsync(interactive, chooseAccount);
            }
            catch (GoogleConsentRequiredException)
            {
                // İzin geri alınmış ya da hiç verilmemiş
                return Finish(SyncOutcome.NeedsConnection, "Google hesabıyla yeniden bağlanmanız gerekiyor.");
            }

            DriveBackupInfo? latest;
            try
            {
                latest = await _drive.FindLatestAsync(token);
            }
            catch (DriveAuthorizationException)
            {
                // Google Play Hizmetleri'nin önbelleğindeki belirteç geçersiz (ör. izin web'den geri alındı):
                // belirteci sil, sessizce yenisini iste ve bir kez daha dene. Yine olmazsa yeniden bağlanmak gerekir.
                await _authorizer.InvalidateTokenAsync(token);
                token = await _authorizer.GetAccessTokenAsync(interactive);
                latest = await _drive.FindLatestAsync(token);
            }

            Preferences.Default.Set(ConnectedKey, true);
            // Hesap seçildiyse farklı bir hesap olabilir: e-postayı yeniden al
            if ((chooseAccount || AccountEmail == null) && await _drive.GetAccountEmailAsync(token) is { } email)
                Preferences.Default.Set(EmailKey, email);

            if (latest == null)
            {
                return Finish(SyncOutcome.NoBackupOnDrive,
                    "Drive'da yedek bulunamadı. Bilgisayardaki Kuyumcu Defteri'nde Ayarlar > Google Drive Yedekleme bağlı mı ve " +
                    "ilk yedek alındı mı, kontrol edin. Telefonda da aynı Google hesabını seçmelisiniz.");
            }

            var meta = _store.Meta;
            var now = DateTimeOffset.Now;
            if (_store.HasData && meta is { IsFromDrive: true } &&
                meta.DriveFileId == latest.FileId &&
                ((latest.Md5Checksum != null && meta.Md5Checksum == latest.Md5Checksum) ||
                 (latest.Md5Checksum == null && meta.BackupTime == latest.ModifiedTime)))
            {
                _store.MarkChecked(now);
                return Finish(SyncOutcome.UpToDate, "Telefondaki yedek güncel.");
            }

            tempPath = _store.NewTempPath();
            await _drive.DownloadAsync(token, latest.FileId, tempPath);
            await _store.ReplaceAsync(tempPath, new BackupMeta
            {
                Source = "drive",
                DriveFileId = latest.FileId,
                BackupTime = latest.ModifiedTime,
                Md5Checksum = latest.Md5Checksum,
                FileName = latest.Name,
                DownloadedAt = now,
                LastCheckedAt = now,
            });
            tempPath = null; // taşındı

            return Finish(SyncOutcome.Updated, "Yeni yedek indirildi.");
        }
        catch (OperationCanceledException)
        {
            return Finish(SyncOutcome.Cancelled, "Bağlantı iptal edildi.");
        }
        catch (GoogleConsentRequiredException)
        {
            return Finish(SyncOutcome.NeedsConnection, "Google hesabıyla yeniden bağlanmanız gerekiyor.");
        }
        catch (GoogleAuthorizationFailedException ex)
        {
            _logger.LogWarning(ex, "Google yetkilendirme hatası");
            return Finish(SyncOutcome.Failed, ex.Message);
        }
        catch (DriveAuthorizationException ex)
        {
            return Finish(SyncOutcome.NeedsConnection, ex.Message + " Yeniden bağlanın.");
        }
        catch (InvalidDataException ex)
        {
            return Finish(SyncOutcome.Failed, "İndirilen yedek okunamadı: " + ex.Message);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Drive isteği başarısız");
            return Finish(SyncOutcome.Failed, ex.StatusCode == null ? "Google Drive'a ulaşılamadı. İnterneti kontrol edin." : ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yedek eşitlenemedi");
            return Finish(SyncOutcome.Failed, "Yedek alınamadı: " + ex.Message);
        }
        finally
        {
            if (tempPath != null)
                TryDelete(tempPath);
            SetBusy(false);
            _syncLock.Release();
        }
    }

    /// <summary>
    /// Yedek yolu: Drive bağlantısı kurulamıyorsa .db3 dosyasını dosya seçiciyle açar
    /// (ör. Drive uygulamasından ya da WhatsApp'tan gelen yedek).
    /// </summary>
    public async Task<SyncResult> ImportFromFileAsync()
    {
        if (!await _syncLock.WaitAsync(0))
            return LastResult ?? Result(SyncOutcome.Failed, "Başka bir işlem sürüyor.");

        string? tempPath = null;
        try
        {
            var picked = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Yedek dosyasını seçin (.db3)" });
            if (picked == null)
                return Finish(SyncOutcome.Cancelled, "Dosya seçilmedi.");

            if (!picked.FileName.EndsWith(".db3", StringComparison.OrdinalIgnoreCase))
                return Finish(SyncOutcome.Failed, "Lütfen .db3 uzantılı Kuyumcu Defteri yedeğini seçin.");

            SetBusy(true);
            tempPath = _store.NewTempPath();
            await using (var source = await picked.OpenReadAsync())
            await using (var target = File.Create(tempPath))
                await source.CopyToAsync(target);

            var now = DateTimeOffset.Now;
            await _store.ReplaceAsync(tempPath, new BackupMeta
            {
                Source = "file",
                BackupTime = GuessBackupTime(picked.FileName),
                FileName = picked.FileName,
                DownloadedAt = now,
            });
            tempPath = null;

            return Finish(SyncOutcome.Updated, "Yedek dosyası açıldı.");
        }
        catch (InvalidDataException ex)
        {
            return Finish(SyncOutcome.Failed, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yedek dosyası açılamadı");
            return Finish(SyncOutcome.Failed, "Dosya açılamadı: " + ex.Message);
        }
        finally
        {
            if (tempPath != null)
                TryDelete(tempPath);
            SetBusy(false);
            _syncLock.Release();
        }
    }

    /// <summary>
    /// Örnek verili bir yedek açar (bilgisayar kurulumu olmadan denemek ve Google Play incelemesi için).
    /// Gerçek yedek indirildiğinde ya da seçildiğinde yerine geçer.
    /// </summary>
    public async Task<SyncResult> LoadDemoAsync()
    {
        if (!await _syncLock.WaitAsync(0))
            return LastResult ?? Result(SyncOutcome.Failed, "Başka bir işlem sürüyor.");

        string? tempPath = null;
        try
        {
            SetBusy(true);
            tempPath = _store.NewTempPath();
            await Task.Run(() => DemoBackup.Create(tempPath));
            var now = DateTimeOffset.Now;
            await _store.ReplaceAsync(tempPath, new BackupMeta
            {
                Source = "demo",
                BackupTime = now,
                FileName = "Örnek veriler",
                DownloadedAt = now,
            });
            tempPath = null;
            return Finish(SyncOutcome.Updated, "Örnek veriler açıldı.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Örnek veriler oluşturulamadı");
            return Finish(SyncOutcome.Failed, "Örnek veriler açılamadı: " + ex.Message);
        }
        finally
        {
            if (tempPath != null)
                TryDelete(tempPath);
            SetBusy(false);
            _syncLock.Release();
        }
    }

    /// <summary>Google bağlantısını keser (izni Google tarafında da geri alır). Telefondaki yedek silinmez.</summary>
    public async Task DisconnectAsync()
    {
        try
        {
            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                // İzni Google Play Hizmetleri üzerinden geri al: hem sunucudaki izin hem de cihazdaki hesap
                // eşleşmesi silinir; sonraki bağlanmada hesap yeniden sorulur.
                await _authorizer.SignOutAsync();
            }
        }
        catch (Exception ex)
        {
            // İzin zaten yoksa ya da internet yoksa yerel bağlantı yine de kesilir
            _logger.LogInformation(ex, "Drive izni geri alınamadı");
        }

        Preferences.Default.Remove(ConnectedKey);
        Preferences.Default.Remove(EmailKey);
        LastResult = null;
        Changed?.Invoke();
    }

    /// <summary>Dosya adı "KuyumcuDefteri_2026-11-24_1742.db3" biçimindeyse yedek zamanını adından okur.</summary>
    private static DateTimeOffset? GuessBackupTime(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        if (name.StartsWith(ProductInfo.DriveSnapshotPrefix, StringComparison.OrdinalIgnoreCase) &&
            DateTime.TryParseExact(name[ProductInfo.DriveSnapshotPrefix.Length..], "yyyy-MM-dd_HHmm",
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeLocal, out var time))
            return new DateTimeOffset(time);
        return null;
    }

    private SyncResult Finish(SyncOutcome outcome, string message)
    {
        LastResult = Result(outcome, message);
        return LastResult;
    }

    private static SyncResult Result(SyncOutcome outcome, string message) => new(outcome, message, DateTimeOffset.Now);

    private void SetBusy(bool busy)
    {
        IsBusy = busy;
        Changed?.Invoke();
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* yok say */ }
    }
}
