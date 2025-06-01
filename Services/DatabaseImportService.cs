using SQLite;
using Kuyumcu.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace Kuyumcu.Services
{
    public class DatabaseImportService
    {
        private readonly IFileImportService _fileImportService;
        private string _importedDatabasePath;
        private bool _isImportedDatabaseOpen = false;

        public bool HasImportedDatabase => _isImportedDatabaseOpen;
        public string ImportedDatabasePath => _importedDatabasePath;

        public DatabaseImportService(IFileImportService fileImportService)
        {
            _fileImportService = fileImportService;
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
                var sourcePath = await _fileImportService.PickDatabaseFileAsync();
                if (string.IsNullOrEmpty(sourcePath))
                    return false; // Kullanıcı işlemi iptal etti

                // Seçilen dosyayı uygulama klasörüne kopyalayalım
                var tempFileName = $"imported_{Path.GetFileName(sourcePath)}";
                var destinationPath = await _fileImportService.CopyFileToAppFolderAsync(sourcePath, tempFileName);
                
                if (string.IsNullOrEmpty(destinationPath))
                    return false;

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
    }

    // SQLite meta verisini sorgulamak için yardımcı sınıf
    public class TableInfo
    {
        public string name { get; set; }
    }
}
