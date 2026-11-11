using SQLite;
using Kuyumcu.Models;
using Kuyumcu.Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace Kuyumcu.Services
{
    public class DatabaseImportService
    {
        private readonly IFilePickerService _filePicker;
        private readonly IAppEnvironment _environment;
        private string _importedDatabasePath;
        private bool _isImportedDatabaseOpen = false;

        public bool HasImportedDatabase => _isImportedDatabaseOpen;
        public string ImportedDatabasePath => _importedDatabasePath;

        public DatabaseImportService(IFilePickerService filePicker, IAppEnvironment environment)
        {
            _filePicker = filePicker;
            _environment = environment;
        }

        /// <summary>
        /// Kullanıcının seçtiği .db3 dosyasını import eder
        /// </summary>
        /// <returns>İşlem başarılı ise true, değilse false</returns>
        public async Task<bool> ImportDatabaseAsync()
        {
            try
            {
                // Eğer hali hazırda açık bir veritabanı varsa kapatalım
                await CloseImportedDatabaseAsync();
                
                // Açık bağlantı olmaması için bir süre bekle
                await Task.Delay(500);

                // Kullanıcıdan bir .db3 dosyası seçmesini isteyelim
                var picked = await _filePicker.PickFileAsync("Yedek dosyasını seçin (.db3)", ".db3");
                if (picked == null)
                    return false; // Kullanıcı işlemi iptal etti

                // Seçilen dosyayı uygulamanın geçici klasörüne kopyalayalım
                var destinationPath = Path.Combine(_environment.CacheDirectory, $"imported_{Path.GetFileName(picked.FileName)}");
                File.Copy(picked.FullPath, destinationPath, overwrite: true);

                try
                {
                    // Yeni yaklaşım: Bağlantıyı test et ve hemen kapat
                    using (var testConnection = new SQLiteConnection(destinationPath, SQLiteOpenFlags.ReadOnly))
                    {
                        // Basit bir test sorgusu çalıştır
                        var result = testConnection.ExecuteScalar<int>("SELECT 1");
                        if (result != 1)
                        {
                            throw new Exception("Veritabanı bağlantı testi başarısız");
                        }
                    }
                    
                    _importedDatabasePath = destinationPath;
                    _isImportedDatabaseOpen = true;
                    
                    return true;
                }
                catch (SQLiteException sqlEx)
                {
                    Console.WriteLine($"SQLite bağlantı hatası: {sqlEx.Message}");
                    await CloseImportedDatabaseAsync();
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Veritabanı import hatası: {ex.Message}");
                await CloseImportedDatabaseAsync();
                return false;
            }
        }

        /// <summary>
        /// Import edilen veritabanını kapatır
        /// </summary>
        public async Task CloseImportedDatabaseAsync()
        {
            // Geçici dosyayı sil
            if (!string.IsNullOrEmpty(_importedDatabasePath) && File.Exists(_importedDatabasePath))
            {
                try
                {
                    // Bazen dosya hala kullanımda olabilir, bu yüzden birkaç deneme yapalım
                    for (int i = 0; i < 3; i++)
                    {
                        try
                        {
                            File.Delete(_importedDatabasePath);
                            break; // Başarılı silme
                        }
                        catch
                        {
                            // Kısa bir süre bekle ve tekrar dene
                            await Task.Delay(200);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Geçici dosya silme hatası: {ex.Message}");
                }
            }

            _importedDatabasePath = null;
            _isImportedDatabaseOpen = false;
            
            // GC'yi tetikleyerek herhangi bir kaynağın serbest kalmasını sağlayalım
            GC.Collect();
            GC.WaitForPendingFinalizers();
            
            // Tüm işlemlerin tamamlanması için bir süre bekleyelim
            await Task.Delay(500);
        }

        /// <summary>
        /// İmport edilen veritabanındaki müşterileri getirir
        /// </summary>
        public async Task<List<Customer>> GetImportedCustomersAsync()
        {
            if (!_isImportedDatabaseOpen || string.IsNullOrEmpty(_importedDatabasePath))
                return new List<Customer>();

            try
            {
                // Her istek için yeni bağlantı oluştur
                using (var db = new SQLiteConnection(_importedDatabasePath, SQLiteOpenFlags.ReadOnly))
                {
                    // Tablo olup olmadığını kontrol et
                    var tableInfo = db.Query<TableInfo>("SELECT name FROM sqlite_master WHERE type='table' AND name='Customer'");
                    if (tableInfo == null || !tableInfo.Any())
                        return new List<Customer>();

                    // Verileri al
                    return db.Table<Customer>()
                        .Where(x => !x.IsDeleted)
                        .OrderBy(c => c.Name)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"İmport edilen müşteri verilerini okuma hatası: {ex.Message}");
                return new List<Customer>();
            }
        }

        /// <summary>
        /// İmport edilen veritabanındaki işlemleri getirir
        /// </summary>
        public async Task<List<Transaction>> GetImportedTransactionsAsync()
        {
            if (!_isImportedDatabaseOpen || string.IsNullOrEmpty(_importedDatabasePath))
                return new List<Transaction>();

            try
            {
                // Her istek için yeni bağlantı oluştur
                using (var db = new SQLiteConnection(_importedDatabasePath, SQLiteOpenFlags.ReadOnly))
                {
                    // Tablo olup olmadığını kontrol et
                    var tableInfo = db.Query<TableInfo>("SELECT name FROM sqlite_master WHERE type='table' AND name='Transaction'");
                    if (tableInfo == null || !tableInfo.Any())
                        return new List<Transaction>();

                    // Verileri al
                    return db.Table<Transaction>()
                        .Where(x => !x.IsDeleted)
                        .OrderByDescending(t => t.Date)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"İmport edilen işlem verilerini okuma hatası: {ex.Message}");
                return new List<Transaction>();
            }
        }

        /// <summary>
        /// Import edilen veritabanında belirli bir müşteriye ait işlemleri getirir
        /// </summary>
        public async Task<List<Transaction>> GetImportedTransactionsByCustomerIdAsync(int customerId)
        {
            if (!_isImportedDatabaseOpen || string.IsNullOrEmpty(_importedDatabasePath))
                return new List<Transaction>();

            try
            {
                // Her istek için yeni bağlantı oluştur
                using (var db = new SQLiteConnection(_importedDatabasePath, SQLiteOpenFlags.ReadOnly))
                {
                    // Tablo olup olmadığını kontrol et
                    var tableInfo = db.Query<TableInfo>("SELECT name FROM sqlite_master WHERE type='table' AND name='Transaction'");
                    if (tableInfo == null || !tableInfo.Any())
                        return new List<Transaction>();

                    // Verileri al
                    return db.Table<Transaction>()
                        .Where(x => !x.IsDeleted && x.CustomerId == customerId)
                        .OrderByDescending(t => t.Date)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"İmport edilen müşteri işlemlerini okuma hatası: {ex.Message}");
                return new List<Transaction>();
            }
        }

        /// <summary>
        /// İçe aktarılan veritabanındaki işletme ayarlarını getirir (işletme adı, altın fiyatı ayarları).
        /// </summary>
        public Task<AppSettings> GetImportedSettingsAsync()
        {
            if (!_isImportedDatabaseOpen || string.IsNullOrEmpty(_importedDatabasePath))
                return Task.FromResult(new AppSettings());

            try
            {
                using var db = new SQLiteConnection(_importedDatabasePath, SQLiteOpenFlags.ReadOnly);
                var tableInfo = db.Query<TableInfo>("SELECT name FROM sqlite_master WHERE type='table' AND name='AppSettings'");
                if (tableInfo == null || !tableInfo.Any())
                    return Task.FromResult(new AppSettings());

                // Eski yedeklerde yeni kolonlar bulunmayabilir; sadece var olanları oku
                var columns = db.Query<TableInfo>("SELECT name FROM pragma_table_info('AppSettings')").Select(c => c.name).ToHashSet();
                var settings = new AppSettings();
                var row = db.Query<AppSettings>("SELECT " + string.Join(",", columns) + " FROM AppSettings LIMIT 1").FirstOrDefault();
                return Task.FromResult(row ?? settings);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"İçe aktarılan ayarları okuma hatası: {ex.Message}");
                return Task.FromResult(new AppSettings());
            }
        }
    }

    // SQLite meta verisini sorgulamak için yardımcı sınıf
    public class TableInfo
    {
        public string name { get; set; }
    }
}
