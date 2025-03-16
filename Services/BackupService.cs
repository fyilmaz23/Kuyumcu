using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Kuyumcu.Services
{
    public class BackupService
    {
        private readonly ILogger<BackupService> _logger;
        private readonly string _databasePath;
        private readonly string _backupDirectory;
        private Timer _backupTimer;
        private const int ONE_DAY_MS = 24 * 60 * 60 * 1000; // 24 hours in milliseconds

        public BackupService(ILogger<BackupService> logger = null)
        {
            _logger = logger;
            _databasePath = Path.Combine(FileSystem.AppDataDirectory, "kuyumcu.db");
            _backupDirectory = Path.Combine(FileSystem.AppDataDirectory, "Backups");
        }

        /// <summary>
        /// Başlat hizmeti ve zamanlayıcıyı ayarla
        /// </summary>
        public void Start()
        {
            _logger?.LogInformation("Yedekleme servisi başlatılıyor...");

            // Yedekleme dizininin var olduğundan emin ol
            if (!Directory.Exists(_backupDirectory))
            {
                Directory.CreateDirectory(_backupDirectory);
                _logger?.LogInformation($"Yedekleme dizini oluşturuldu: {_backupDirectory}");
            }

            // İlk yedeklemeyi hemen çalıştır, sonra günlük olarak
            BackupDatabase();

            // Günlük zamanlayıcıyı ayarla (şu andan itibaren 24 saat)
            _backupTimer = new Timer(
                callback: _ => BackupDatabase(),
                state: null,
                dueTime: ONE_DAY_MS,  // İlk çalışma için 24 saat bekle
                period: ONE_DAY_MS    // Sonra her 24 saatte bir tekrarla
            );

            _logger?.LogInformation("Yedekleme zamanlayıcısı ayarlandı. Veritabanı günlük olarak yedeklenecek.");
        }

        /// <summary>
        /// Hizmeti durdur ve kaynakları temizle
        /// </summary>
        public void Stop()
        {
            _logger?.LogInformation("Yedekleme servisi durduruluyor...");
            _backupTimer?.Dispose();
            _backupTimer = null;
            _logger?.LogInformation("Yedekleme servisi durduruldu.");
        }

        /// <summary>
        /// Talep üzerine veritabanını manuel olarak yedekle
        /// </summary>
        public void BackupNow()
        {
            BackupDatabase();
        }

        /// <summary>
        /// Veritabanını yedekleme işlemini gerçekleştir
        /// </summary>
        private void BackupDatabase()
        {
            try
            {
                if (!File.Exists(_databasePath))
                {
                    _logger?.LogWarning($"Veritabanı dosyası bulunamadı: {_databasePath}");
                    return;
                }

                // Tarih damgalı yedek dosya adı oluştur
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupFileName = $"kuyumcu_backup_{timestamp}.db";
                string backupFilePath = Path.Combine(_backupDirectory, backupFileName);

                // Veritabanı dosyasını kopyala
                File.Copy(_databasePath, backupFilePath, overwrite: true);

                _logger?.LogInformation($"Veritabanı başarıyla yedeklendi: {backupFilePath}");

                // İsteğe bağlı: Eski yedekleri temizle (örneğin, 30 günden eski)
                //CleanupOldBackups(30);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Veritabanı yedekleme işlemi sırasında hata oluştu");
            }
        }

        /// <summary>
        /// Belirtilen gün sayısından daha eski yedekleri temizle
        /// </summary>
        private void CleanupOldBackups(int daysToKeep)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                var backupFiles = Directory.GetFiles(_backupDirectory, "kuyumcu_backup_*.db");

                foreach (var file in backupFiles)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        fileInfo.Delete();
                        _logger?.LogInformation($"Eski yedek silindi: {file}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Eski yedekleri temizlerken hata oluştu");
            }
        }

        /// <summary>
        /// Tüm mevcut yedeklerin listesini döndür
        /// </summary>
        public List<string> GetBackupsList()
        {
            if (!Directory.Exists(_backupDirectory))
            {
                return new List<string>();
            }

            return Directory.GetFiles(_backupDirectory, "kuyumcu_backup_*.db")
                .OrderByDescending(f => f)
                .ToList();
        }

        /// <summary>
        /// Belirtilen yedek dosyasından veritabanını geri yükle
        /// </summary>
        public bool RestoreDatabase(string backupFilePath)
        {
            try
            {
                if (!File.Exists(backupFilePath))
                {
                    _logger?.LogWarning($"Belirtilen yedek dosyası bulunamadı: {backupFilePath}");
                    return false;
                }

                // Mevcut veritabanının yedeğini al
                string currentBackup = Path.Combine(_backupDirectory, $"before_restore_{DateTime.Now:yyyyMMdd_HHmmss}.db");
                File.Copy(_databasePath, currentBackup, overwrite: true);

                // Seçilen yedeği geri yükle
                File.Copy(backupFilePath, _databasePath, overwrite: true);

                _logger?.LogInformation($"Veritabanı başarıyla geri yüklendi: {backupFilePath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Veritabanı geri yükleme işlemi sırasında hata oluştu");
                return false;
            }
        }
    }
}
