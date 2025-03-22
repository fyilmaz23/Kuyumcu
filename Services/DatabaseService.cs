using SQLite;
using Kuyumcu.Models;
using System.Collections.ObjectModel;

namespace Kuyumcu.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection _database;
        private bool _initialized = false;

        public DatabaseService()
        {
        }

        private async Task InitializeAsync()
        {
            if (_initialized)
                return;

            var databasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kuyumcu.db3");
            _database = new SQLiteAsyncConnection(databasePath);

            await _database.CreateTableAsync<Customer>();
            await _database.CreateTableAsync<Transaction>();
            await _database.CreateTableAsync<QuickEntry>();

            _initialized = true;
        }
        
        /// <summary>
        /// Veritabanı şemasını günceller. Bu metot sadece şema değişikliklerinde kullanılmalıdır.
        /// </summary>
        public async Task<bool> UpdateDatabaseSchemaAsync()
        {
            try
            {
                // Veritabanını yeniden oluşturmadan önce bağlantıyı kapatıp açalım
                _initialized = false;
                
                var databasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kuyumcu.db3");
                _database = new SQLiteAsyncConnection(databasePath);
                
                // Tabloları oluştur veya güncelle
                await _database.CreateTableAsync<Customer>();
                await _database.CreateTableAsync<Transaction>();
                await _database.CreateTableAsync<QuickEntry>();
                
                _initialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Veritabanı şeması güncellenirken hata oluştu: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Veritabanının bir kopyasını belirtilen dosya yoluna oluşturur.
        /// </summary>
        /// <param name="targetPath">Yedekleme yapılacak hedef dosya yolu</param>
        public async Task<bool> CreateBackupCopyAsync(string targetPath)
        {
            try
            {
                await InitializeAsync();
                
                // Veritabanı bağlantısını kapatmak için tüm işlemleri tamamlayalım
                _database.GetConnection().Close();
                _initialized = false;
                
                // Veritabanı dosya yolunu alalım
                var databasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kuyumcu.db3");
                
                try
                {
                    // Dosyayı açmadan önce kısa bir bekleme süresi ekleyelim
                    await Task.Delay(500);
                    
                    // Dosya kullanımda olabilir, bu yüzden mümkün olduğunca kısa süre için açıp okuyalım
                    byte[] fileBytes;
                    using (FileStream fs = new FileStream(databasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        fileBytes = new byte[fs.Length];
                        await fs.ReadAsync(fileBytes, 0, fileBytes.Length);
                    }
                    
                    // Şimdi okunan verileri hedef dosyaya yazalım
                    using (FileStream fs = new FileStream(targetPath, FileMode.Create))
                    {
                        await fs.WriteAsync(fileBytes, 0, fileBytes.Length);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Dosya kopyalama hatası: {ex.Message}");
                    return false;
                }
                
                // Veritabanını yeniden bağlayalım
                await InitializeAsync();
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Veritabanı yedek kopyası oluşturulurken hata oluştu: {ex.Message}");
                
                // Herhangi bir hata durumunda veritabanını yeniden bağlamaya çalışalım
                try
                {
                    _initialized = false;
                    await InitializeAsync();
                }
                catch
                {
                    // Yeniden bağlanma işlemi hata verirse sessizce devam edelim
                }
                
                return false;
            }
        }

        // Customer methods
        public async Task<List<Customer>> GetCustomersAsync()
        {
            await InitializeAsync();
            return await _database.Table<Customer>()
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<Customer> GetCustomerAsync(int id)
        {
            await InitializeAsync();
            return await _database.Table<Customer>()
                .Where(c => c.Id == id && !c.IsDeleted)
                .FirstOrDefaultAsync();
        }

        public async Task<int> SaveCustomerAsync(Customer customer)
        {
            await InitializeAsync();
            if (customer.Id != 0)
                return await _database.UpdateAsync(customer);
            else
                return await _database.InsertAsync(customer);
        }

        public async Task<int> DeleteCustomerAsync(Customer customer)
        {
            await InitializeAsync();
            
            // Soft delete customer
            customer.IsDeleted = true;
            await _database.UpdateAsync(customer);
            
            // Soft delete all related transactions
            var customerTransactions = await _database.Table<Transaction>()
                .Where(t => t.CustomerId == customer.Id && !t.IsDeleted)
                .ToListAsync();
                
            foreach (var transaction in customerTransactions)
            {
                transaction.IsDeleted = true;
                await _database.UpdateAsync(transaction);
            }
            
            return 1; // Return success
        }

        // Transaction methods
        public async Task<List<Transaction>> GetTransactionsAsync()
        {
            await InitializeAsync();
            return await _database.Table<Transaction>()
                .Where(t => !t.IsDeleted)
                .OrderByDescending(t => t.Date)
                .ToListAsync();
        }

        public async Task<List<Transaction>> GetCustomerTransactionsAsync(int customerId)
        {
            await InitializeAsync();
            return await _database.Table<Transaction>()
                .Where(t => t.CustomerId == customerId && !t.IsDeleted)
                .OrderByDescending(t => t.Date)
                .ToListAsync();
        }

        public async Task<decimal> GetCustomerBalanceAsync(int customerId)
        {
            await InitializeAsync();
            var transactions = await GetCustomerTransactionsAsync(customerId);
            decimal balance = 0;

            foreach (var transaction in transactions)
            {
                if (transaction.CurrencyType == CurrencyType.TurkishLira)
                {
                    if (transaction.Type == TransactionType.CustomerDebt)
                        balance += transaction.Amount;
                    else
                        balance -= transaction.Amount;
                }
            }

            return balance;
        }

        public async Task<Transaction> GetTransactionAsync(int id)
        {
            await InitializeAsync();
            return await _database.Table<Transaction>()
                .Where(t => t.Id == id && !t.IsDeleted)
                .FirstOrDefaultAsync();
        }

        public async Task<int> SaveTransactionAsync(Transaction transaction)
        {
            await InitializeAsync();
            if (transaction.Id != 0)
                return await _database.UpdateAsync(transaction);
            else
                return await _database.InsertAsync(transaction);
        }

        public async Task<int> DeleteTransactionAsync(Transaction transaction)
        {
            await InitializeAsync();
            
            // Soft delete transaction
            transaction.IsDeleted = true;
            return await _database.UpdateAsync(transaction);
        }

        // QuickEntry methods
        public async Task<List<QuickEntry>> GetQuickEntriesAsync()
        {
            await InitializeAsync();
            return await _database.Table<QuickEntry>().OrderByDescending(e => e.CreatedDate).ToListAsync();
        }

        public async Task<QuickEntry> GetQuickEntryAsync(int id)
        {
            await InitializeAsync();
            return await _database.Table<QuickEntry>().Where(e => e.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<QuickEntry>> SearchQuickEntriesAsync(string searchTerm)
        {
            await InitializeAsync();
            return await _database.Table<QuickEntry>()
                .Where(e => e.FullName.Contains(searchTerm) || e.TcKimlikNo.Contains(searchTerm))
                .OrderByDescending(e => e.CreatedDate)
                .ToListAsync();
        }

        public async Task<int> SaveQuickEntryAsync(QuickEntry entry)
        {
            await InitializeAsync();
            if (entry.Id != 0)
                return await _database.UpdateAsync(entry);
            else
                return await _database.InsertAsync(entry);
        }

        // Alias for SaveQuickEntryAsync for clarity when updating
        public async Task<int> UpdateQuickEntryAsync(QuickEntry entry)
        {
            return await SaveQuickEntryAsync(entry);
        }

        public async Task<int> DeleteQuickEntryAsync(QuickEntry entry)
        {
            await InitializeAsync();
            return await _database.DeleteAsync(entry);
        }
    }
}
