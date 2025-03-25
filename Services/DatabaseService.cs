using SQLite;
using Kuyumcu.Models;
using System.Collections.ObjectModel;
using System;

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

        public async Task<List<Customer>> GetCustomersAsync(int page, int pageSize, string sortField, SortDirection sortDirection)
        {
            return await GetCustomersPaginationAsync(page, pageSize, sortField, sortDirection, null);
        }

        public async Task<List<Customer>> GetCustomersPaginationAsync(int page, int pageSize, string sortField, SortDirection? sortDirection, string searchTerm)
        {
            await InitializeAsync();
            
            // Default sorting is by Name
            var query = _database.Table<Customer>().Where(c => !c.IsDeleted);
            
            // Apply search filter if provided
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                // Use LIKE operator for case-insensitive search
                query = query.Where(c => 
                    c.Name.ToLower().Contains(searchTerm) || 
                    (c.PhoneNumber != null && c.PhoneNumber.ToLower().Contains(searchTerm)));
            }
            
            // Apply sorting
            if (!string.IsNullOrEmpty(sortField))
            {
                switch (sortField.ToLower())
                {
                    case "name":
                        query = sortDirection == SortDirection.Ascending
                            ? query.OrderBy(c => c.Name)
                            : query.OrderByDescending(c => c.Name);
                        break;
                    case "phone":
                        query = sortDirection == SortDirection.Ascending
                            ? query.OrderBy(c => c.PhoneNumber)
                            : query.OrderByDescending(c => c.PhoneNumber);
                        break;
                    default:
                        query = query.OrderBy(c => c.Name);
                        break;
                }
            }
            else
            {
                query = query.OrderBy(c => c.Name);
            }
            
            // Apply pagination - SQLite.Net PCL pagination
            return await query
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetCustomerCountAsync()
        {
            return await GetCustomerCountAsync(null);
        }

        public async Task<int> GetCustomerCountAsync(string searchTerm)
        {
            await InitializeAsync();
            var query = _database.Table<Customer>().Where(c => !c.IsDeleted);
            
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                query = query.Where(c => 
                    c.Name.ToLower().Contains(searchTerm) || 
                    (c.PhoneNumber != null && c.PhoneNumber.ToLower().Contains(searchTerm)));
            }
            
            return await query.CountAsync();
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
            return await _database.Table<Transaction>().Where(c => !c.IsDeleted).ToListAsync();
        }

        public async Task<List<Transaction>> GetTransactionsAsync(int page, int pageSize, string sortField, SortDirection? sortDirection)
        {
            return await GetTransactionsPaginationAsync(page, pageSize, sortField, sortDirection, null);
        }

        public async Task<List<Transaction>> GetTransactionsPaginationAsync(int page, int pageSize, string sortField, SortDirection? sortDirection, string searchTerm, TransactionType? filterType = null)
        {
            await InitializeAsync();
            
            // Base query for transactions
            var query = _database.Table<Transaction>().Where(c => !c.IsDeleted); ;
            
            // Apply filter by type if requested
            if (filterType.HasValue)
            {
                query = query.Where(t => t.Type == filterType.Value);
            }
            
            // Apply search filter if provided
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                // Search by description
                query = query.Where(t => 
                    (t.Description != null && t.Description.ToLower().Contains(searchTerm)));
                
                // Note: Ideally we'd search by customer name too, but that requires a join
                // which might be complicated with SQLite.NET PCL. We'll handle that in the UI.
            }
            
            // Apply sorting
            if (!string.IsNullOrEmpty(sortField))
            {
                switch (sortField.ToLower())
                {
                    case "date":
                        query = sortDirection == SortDirection.Ascending
                            ? query.OrderBy(t => t.Date)
                            : query.OrderByDescending(t => t.Date);
                        break;
                    case "amount":
                        query = sortDirection == SortDirection.Ascending
                            ? query.OrderBy(t => t.Amount)
                            : query.OrderByDescending(t => t.Amount);
                        break;
                    case "type":
                        query = sortDirection == SortDirection.Ascending
                            ? query.OrderBy(t => t.Type)
                            : query.OrderByDescending(t => t.Type);
                        break;
                    case "currency":
                        query = sortDirection == SortDirection.Ascending
                            ? query.OrderBy(t => t.CurrencyType)
                            : query.OrderByDescending(t => t.CurrencyType);
                        break;
                    default:
                        // Default sorting is by date descending
                        query = query.OrderByDescending(t => t.Date);
                        break;
                }
            }
            else
            {
                // Default sorting is by date descending
                query = query.OrderByDescending(t => t.Date);
            }
            
            // Apply pagination - SQLite.Net PCL pagination
            return await query
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTransactionCountAsync(string searchTerm = null, TransactionType? filterType = null)
        {
            await InitializeAsync();
            var query = _database.Table<Transaction>().Where(c => !c.IsDeleted);
            
            // Apply filter by type if requested
            if (filterType.HasValue)
            {
                query = query.Where(t => t.Type == filterType.Value);
            }
            
            // Apply search filter if provided
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                query = query.Where(t => 
                    (t.Description != null && t.Description.ToLower().Contains(searchTerm)));
            }
            
            return await query.CountAsync();
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

    public enum SortDirection
    {
        Ascending,
        Descending
    }
}
