using Kuyumcu.Platform;
using Microsoft.Extensions.Logging;

namespace Kuyumcu.Services
{
    /// <summary>
    /// Bilgisayar üzerinde günlük otomatik yerel yedek alır (internet gerektirmez).
    /// Yedekler <see cref="IAppEnvironment.BackupDirectory"/> klasörüne yazılır — son <see cref="DaysToKeep"/> gün saklanır.
    /// </summary>
    public class BackupService : IDisposable
    {
        public const int DaysToKeep = 30;
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
        /// Servisi başlatır: açılışta bir yedek alır, ardından her 24 saatte bir tekrarlar.
        /// </summary>
        public void Start()
        {
            Directory.CreateDirectory(_backupDirectory);
            _backupTimer = new Timer(_ => _ = BackupNowAsync(), null, TimeSpan.FromMinutes(1), Interval);
            _logger?.LogInformation("Yerel yedekleme servisi başlatıldı: {Dir}", _backupDirectory);
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

        private void CleanupOldBackups()
        {
            try
            {
                var cutoff = DateTime.Now.AddDays(-DaysToKeep);
                foreach (var file in Directory.GetFiles(_backupDirectory, $"{FilePrefix}*.db3"))
                {
                    if (File.GetCreationTime(file) < cutoff)
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
