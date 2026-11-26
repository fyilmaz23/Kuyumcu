using Kuyumcu.Platform;
using Microsoft.Extensions.Logging;

namespace Kuyumcu.Services
{
    /// <summary>
    /// Bilgisayar üzerinde günlük otomatik yerel yedek alır (internet gerektirmez; günde en fazla bir otomatik yedek).
    /// Yedekler <see cref="IAppEnvironment.BackupDirectory"/> klasörüne yazılır — son <see cref="DaysToKeep"/> gün saklanır.
    /// </summary>
    public class BackupService : IDisposable
    {
        public const int DaysToKeep = 30;

        /// <summary>Yaşına bakılmaksızın saklanan en yeni yedek sayısı.</summary>
        public const int MinimumToKeep = 10;
        private const string FilePrefix = "kuyumcu_backup_";
        private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

        private readonly DatabaseService _databaseService;
        private readonly ILogger<BackupService>? _logger;
        private readonly string _backupDirectory;
        private Timer? _backupTimer;

        public BackupService(DatabaseService databaseService, IAppEnvironment environment, ILogger<BackupService>? logger = null)
        {
            _databaseService = databaseService;
            _logger = logger;
            _backupDirectory = environment.BackupDirectory;
        }

        public string BackupDirectory => _backupDirectory;

        /// <summary>
        /// Servisi başlatır: açılıştan 1 dk sonra ve ardından her 24 saatte bir otomatik yedek dener
        /// (<see cref="BackupIfDueAsync"/>: o gün yedek alınmışsa atlanır).
        /// </summary>
        public void Start()
        {
            Directory.CreateDirectory(_backupDirectory);
            _backupTimer = new Timer(_ => _ = BackupIfDueAsync(), null, TimeSpan.FromMinutes(1), Interval);
            _logger?.LogInformation("Yerel yedekleme servisi başlatıldı: {Dir}", _backupDirectory);
        }

        /// <summary>
        /// Otomatik yedek: bugün alınmış bir yedek (otomatik ya da elle) varsa atlanır, yoksa <see cref="BackupNowAsync"/>.
        /// Uygulama günde birkaç kez açılınca Belgeler'de aynı güne ait çok sayıda dosya birikmesin diye.
        /// "Şimdi Yedekle" ve geri yükleme öncesi güvenlik yedeği bunu kullanmaz, her zaman yedek alır.
        /// </summary>
        public async Task<string?> BackupIfDueAsync()
        {
            try
            {
                var today = DateTime.Today;
                if (GetBackupsList().Any(f => GetBackupTime(f).Date == today))
                {
                    _logger?.LogInformation("Bugün yedek zaten alınmış, otomatik yerel yedek atlandı");
                    return null;
                }
            }
            catch (Exception ex)
            {
                // Klasör okunamadıysa yedeği atlamak yerine almayı dene
                _logger?.LogWarning(ex, "Mevcut yedekler okunamadı");
            }
            return await BackupNowAsync();
        }

        public void Stop()
        {
            _backupTimer?.Dispose();
            _backupTimer = null;
        }

        /// <summary>
        /// Hemen bir yerel yedek alır. Başarılıysa yedek dosyasının yolunu döndürür.
        /// </summary>
        public async Task<string?> BackupNowAsync()
        {
            try
            {
                if (!File.Exists(_databaseService.DatabasePath))
                {
                    _logger?.LogWarning("Veritabanı dosyası bulunamadı: {Path}", _databaseService.DatabasePath);
                    return null;
                }

                // Kurulumu tamamlanmamış (boş) veritabanı yedeklenmez: yeniden kurulumda en yeni yedek boş olmasın ve
                // "önceki yedeğiniz bulundu" seçeneği gerçek veriyi göstersin.
                if (!await _databaseService.IsSetupCompletedAsync())
                {
                    _logger?.LogInformation("Kurulum tamamlanmadı, yerel yedek atlandı");
                    return null;
                }

                Directory.CreateDirectory(_backupDirectory);
                var backupFilePath = Path.Combine(_backupDirectory, $"{FilePrefix}{DateTime.Now:yyyyMMdd_HHmmss}.db3");

                if (!await _databaseService.CreateBackupCopyAsync(backupFilePath))
                    return null;

                _logger?.LogInformation("Yerel yedek alındı: {Path}", backupFilePath);
                CleanupOldBackups();
                return backupFilePath;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Yerel yedekleme sırasında hata oluştu");
                return null;
            }
        }

        /// <summary>
        /// Mevcut yerel yedekleri en yeniden eskiye döndürür.
        /// </summary>
        public List<string> GetBackupsList()
        {
            if (!Directory.Exists(_backupDirectory))
                return new List<string>();

            return Directory.GetFiles(_backupDirectory, $"{FilePrefix}*.db3")
                .OrderByDescending(f => f)
                .ToList();
        }

        /// <summary>
        /// Yedeğin alındığı zaman: dosya adındaki tarih (kopyalanınca/OneDrive eşitleyince değişmez), okunamazsa dosya zamanı.
        /// </summary>
        public static DateTime GetBackupTime(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (name.StartsWith(FilePrefix, StringComparison.Ordinal) &&
                DateTime.TryParseExact(name[FilePrefix.Length..], "yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var time))
                return time;
            return File.GetLastWriteTime(path);
        }

        private void CleanupOldBackups()
        {
            try
            {
                // En yeni MinimumToKeep yedek, ne kadar eski olursa olsun silinmez (uygulama uzun süre açılmadıysa
                // ya da yeniden kurulduysa elde hiç yedek kalmaması olmasın). Dosya adı tarih içerdiği için ada göre sıralanır.
                var cutoff = DateTime.Now.AddDays(-DaysToKeep);
                var candidates = Directory.GetFiles(_backupDirectory, $"{FilePrefix}*.db3")
                    .OrderByDescending(f => Path.GetFileName(f), StringComparer.Ordinal)
                    .Skip(MinimumToKeep);
                foreach (var file in candidates)
                {
                    // Yaş dosya adındaki tarihten: oluşturma zamanı kopyalanınca/OneDrive eşitleyince değişir
                    // (Linux'ta da güvenilir değil)
                    if (GetBackupTime(file) < cutoff)
                        File.Delete(file);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Eski yedekler temizlenemedi");
            }
        }

        public void Dispose() => Stop();
    }
}
