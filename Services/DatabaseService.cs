using SQLite;
using Kuyumcu.Models;
using System.Collections.ObjectModel;
using System;
using System.Globalization;

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
            await _database.CreateTableAsync<AppSettings>();

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
                await _database.CreateTableAsync<AppSettings>();

                _initialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateDatabaseSchemaAsync hata: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Customer tablosundaki Name alanının büyük/küçük harf duyarsız olması için tabloyu günceller
        /// </summary>
        public async Task<bool> UpdateCustomerTableForCaseInsensitiveName()
        {
            try
            {
                await InitializeAsync();

                // Mevcut Customer verilerini al
                var existingCustomers = await _database.Table<Customer>().ToListAsync();

                // SQLite'da doğrudan kolonu değiştirme imkanı yok, yeni tablo oluşturup veri aktarmamız gerekiyor
                await _database.ExecuteAsync("CREATE TABLE CustomerNew (" +
                    "Id INTEGER PRIMARY KEY AUTOINCREMENT," +
                    "Name TEXT COLLATE NOCASE NOT NULL," +
                    "PhoneNumber TEXT," +
                    "IsDeleted INTEGER NOT NULL DEFAULT 0)");

                // Eski tablodan yeni tabloya verileri kopyala
                foreach (var customer in existingCustomers)
                {
                    await _database.ExecuteAsync(
                        "INSERT INTO CustomerNew (Id, Name, PhoneNumber, IsDeleted) VALUES (?, ?, ?, ?)",
                        customer.Id, customer.Name, customer.PhoneNumber, customer.IsDeleted ? 1 : 0);
                }

                // Transaction tablolarında Foreign Key kullanılmadığı için bu işlemleri yapabiliriz

                // Eski tabloyu sil
                await _database.ExecuteAsync("DROP TABLE Customer");

                // Yeni tabloyu rename et
                await _database.ExecuteAsync("ALTER TABLE CustomerNew RENAME TO Customer");

                // Yeniden initialize edelim
                _initialized = false;
                await InitializeAsync();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateCustomerTableForCaseInsensitiveName hata: {ex.Message}");
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

                // Veritabanı dosya yolunu alalım
                var databasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kuyumcu.db3");

                // Dosyayı bağlantı açıkken kopyalayabiliriz (FileShare.ReadWrite ile)
                byte[] fileBytes;
                using (FileStream fs = new FileStream(databasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    fileBytes = new byte[fs.Length];
                    await fs.ReadAsync(fileBytes, 0, fileBytes.Length);
                }

                // Okunan verileri hedef dosyaya yazalım
                using (FileStream fs = new FileStream(targetPath, FileMode.Create))
                {
                    await fs.WriteAsync(fileBytes, 0, fileBytes.Length);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Veritabani yedek kopyasi olusturulurken hata olustu: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Yedek dosyasindan veritabanini geri yukler. Sadece Customer, Transaction ve QuickEntry verileri aktarilir, ayarlariniz korunur.
        /// </summary>
        public async Task<(bool Success, string Message)> RestoreDatabaseAsync(string backupFilePath)
        {
            try
            {
                if (!File.Exists(backupFilePath))
                    return (false, "Yedek dosyasi bulunamadi.");

                // Validate the backup file is a valid SQLite database
                var validationResult = await ValidateBackupFileAsync(backupFilePath);
                if (!validationResult.IsValid)
                    return (false, validationResult.Message);

                // Read data from backup database
                List<Customer> backupCustomers;
                List<Transaction> backupTransactions;

                try
                {
                    var backupDb = new SQLiteAsyncConnection(backupFilePath);
                    backupCustomers = await backupDb.Table<Customer>().ToListAsync();
                    
                    // Transaction table might not exist in very old backups, handle safely
                    try { backupTransactions = await backupDb.Table<Transaction>().ToListAsync(); }
                    catch { backupTransactions = new List<Transaction>(); }
                    
                    await backupDb.CloseAsync();
                }
                catch (Exception ex)
                {
                    return (false, $"Yedek dosyasindan veri okunamadi: {ex.Message}");
                }

                await InitializeAsync();

                // Start a transaction in current DB to safely replace data
                await _database.RunInTransactionAsync(tran =>
                {
                    // Delete existing data
                    tran.DeleteAll<Customer>();
                    tran.DeleteAll<Transaction>();

                    // Insert backup data
                    if (backupCustomers.Any()) tran.InsertAll(backupCustomers);
                    if (backupTransactions.Any()) tran.InsertAll(backupTransactions);
                });

                return (true, "Veritabani basariyla geri yuklendi. Uygulama ayarlariniz korundu.");
            }
            catch (Exception ex)
            {
                return (false, $"Geri yukleme sirasinda hata olustu: {ex.Message}");
            }
        }

        private async Task<(bool IsValid, string Message)> ValidateBackupFileAsync(string filePath)
        {
            try
            {
                var testDb = new SQLiteAsyncConnection(filePath);
                var tableInfo = await testDb.QueryAsync<TableInfoResult>(
                    "SELECT name FROM sqlite_master WHERE type='table' AND name='Customer'");
                await testDb.CloseAsync();

                if (tableInfo == null || tableInfo.Count == 0)
                    return (false, "Gecersiz yedek dosyasi: 'Customer' tablosu bulunamadi.");

                return (true, "");
            }
            catch
            {
                return (false, "Gecersiz dosya formati. Lutfen gecerli bir .db3 yedek dosyasi secin.");
            }
        }

        private class TableInfoResult
        {
            [Column("name")]
            public string Name { get; set; }
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
                // Get all customers first to perform proper Turkish-aware search
                var allCustomers = await query.ToListAsync();

                // Create Turkish culture-aware comparison for search
                var turkishCulture = new CultureInfo("tr-TR");
                var searchTermTurkish = searchTerm.ToLower(turkishCulture);

                // Filter with culture-aware comparison
                var filteredCustomers = allCustomers.Where(c =>
                    c.Name.ToLower(turkishCulture).Contains(searchTermTurkish) ||
                    (c.PhoneNumber != null && c.PhoneNumber.ToLower(turkishCulture).Contains(searchTermTurkish)))
                    .ToList();

                // Apply sorting to the filtered results
                var sortedAndFilteredCustomers = ApplySorting(filteredCustomers, sortField, sortDirection);

                // Apply pagination to the in-memory collection
                return sortedAndFilteredCustomers
                    .Skip(page * pageSize)
                    .Take(pageSize)
                    .ToList();
            }

            // If no search term, we can let SQLite handle sorting and pagination
            // Get all customers to sort them properly with Turkish culture
            var customers = await query.ToListAsync();
            var sortedCustomers = ApplySorting(customers, sortField, sortDirection);

            // Apply pagination to the in-memory collection
            return sortedCustomers
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToList();
        }

        public async Task<int> GetCustomerCountAsync()
        {
            return await GetCustomerCountAsync(null);
        }

        public async Task<int> GetCustomerCountAsync(string searchTerm)
        {
            await InitializeAsync();

            var query = _database.Table<Customer>().Where(c => !c.IsDeleted);

            // Apply search filter if provided
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                // Get all customers to perform proper Turkish culture search
                var allCustomers = await query.ToListAsync();

                // Create Turkish culture-aware comparison for search
                var turkishCulture = new CultureInfo("tr-TR");
                var searchTermTurkish = searchTerm.ToLower(turkishCulture);

                // Count with culture-aware comparison
                return allCustomers.Count(c =>
                    c.Name.ToLower(turkishCulture).Contains(searchTermTurkish) ||
                    (c.PhoneNumber != null && c.PhoneNumber.ToLower(turkishCulture).Contains(searchTermTurkish)));
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

        /// <summary>
        /// İsme göre müşteri arar (büyük/küçük harf duyarsız)
        /// </summary>
        /// <param name="name">Aranacak müşteri ismi</param>
        /// <returns>Bulunan müşteri, yoksa null</returns>
        public async Task<Customer> GetCustomerByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            await InitializeAsync();

            // Create Turkish culture-aware comparison for search
            var turkishCulture = new CultureInfo("tr-TR");
            string nameLower = name.ToLower(turkishCulture);

            // Get all customers and filter in memory for proper Turkish culture-aware comparison
            var customers = await _database.Table<Customer>().Where(c => !c.IsDeleted).ToListAsync();
            return customers.FirstOrDefault(c =>
                string.Compare(c.Name.ToLower(turkishCulture), nameLower, StringComparison.CurrentCultureIgnoreCase) == 0);
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

        /// <summary>
        /// En çok işlem yapılan para birimini bulur
        /// </summary>
        /// <returns>En aktif para birimi ve işlem sayısı</returns>
        public async Task<(CurrencyType CurrencyType, int TransactionCount)> GetMostActiveCurrencyTypeAsync()
        {
            await InitializeAsync();

            // Tüm işlemleri al
            var transactions = await GetTransactionsAsync();

            // Para birimine göre grupla ve en fazla işlem yapılanı bul
            var mostActiveCurrency = transactions
                .Where(t => !t.IsDeleted && !t.IsDeposit)
                .GroupBy(t => t.CurrencyType)
                .Select(g => new { CurrencyType = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .FirstOrDefault();

            if (mostActiveCurrency == null)
            {
                // Eğer hiç işlem yoksa varsayılan olarak TL döndür
                return (CurrencyType.TurkishLira, 0);
            }

            return (mostActiveCurrency.CurrencyType, mostActiveCurrency.Count);
        }

        /// <summary>
        /// Borçlu müşteri sayısını hesaplar. Bir müşteri herhangi bir para biriminde borçluysa (giden > gelen) borçlu sayılır
        /// </summary>
        /// <returns>Borçlu müşteri sayısı</returns>
        public async Task<int> GetIndebtedCustomersCountAsync()
        {
            await InitializeAsync();

            // Tüm müşterileri al
            var customers = await GetCustomersAsync();
            // Tüm işlemleri al
            var transactions = await GetTransactionsAsync();

            int indebtedCount = 0;

            foreach (var customer in customers)
            {
                // Bu müşterinin işlemlerini al
                var customerTransactions = transactions
                    .Where(t => !t.IsDeleted && !t.IsDeposit && t.CustomerId == customer.Id)
                    .ToList();

                if (!customerTransactions.Any())
                    continue;

                // Her para birimi için ayrı kontrol et
                var currencyGroups = customerTransactions
                    .GroupBy(t => t.CurrencyType)
                    .ToList();

                foreach (var group in currencyGroups)
                {
                    var outgoing = group.Where(t => t.Type == TransactionType.CustomerDebt).Sum(t => t.Amount);
                    var incoming = group.Where(t => t.Type == TransactionType.StoreDebt).Sum(t => t.Amount);

                    // Eğer bu para biriminde borçluysa (giden > gelen) sayıyı artır ve sonraki müşteriye geç
                    if (outgoing > incoming)
                    {
                        indebtedCount++;
                        break; // Bu müşteriyi saydık, bir sonraki müşteriye geç
                    }
                }
            }

            return indebtedCount;
        }

        public async Task<List<Transaction>> GetTransactionsAsync(int page, int pageSize, string sortField, SortDirection? sortDirection)
        {
            return await GetTransactionsPaginationAsync(page, pageSize, sortField, sortDirection, null);
        }

        public async Task<List<Transaction>> GetTransactionsPaginationAsync(int page, int pageSize, string sortField, SortDirection? sortDirection, string searchTerm, TransactionType? filterType = null)
        {
            await InitializeAsync();

            // Base query for transactions
            var query = _database.Table<Transaction>().Where(c => !c.IsDeleted && !c.IsDeposit); ;

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
            var query = _database.Table<Transaction>().Where(c => !c.IsDeleted && !c.IsDeposit);

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

        /// <summary>
        /// Müşteriye ait işlemleri belirli bir para birimine göre ve sayfalanarak getirir
        /// </summary>
        /// <param name="customerId">Müşteri ID</param>
        /// <param name="currencyType">Para birimi</param>
        /// <param name="skip">Atlanacak işlem sayısı</param>
        /// <param name="take">Alınacak maksimum işlem sayısı</param>
        /// <returns>İşlem listesi</returns>
        public async Task<List<Transaction>> GetCustomerTransactionsByCurrencyPaginatedAsync(int customerId, CurrencyType currencyType, int skip, int take)
        {
            await InitializeAsync();
            return await _database.Table<Transaction>()
                .Where(t => t.CustomerId == customerId && !t.IsDeleted && t.CurrencyType == currencyType)
                .OrderByDescending(t => t.Date)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
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


        // Customer navigation methods
        /// <summary>
        /// Belirtilen müşteri ID'sine göre bir önceki müşterinin ID'sini döndürür
        /// </summary>
        /// <param name="currentId">Mevcut müşteri ID'si</param>
        /// <returns>Bir önceki müşteri ID'si veya müşteri yoksa null</returns>
        public async Task<int?> GetPreviousCustomerIdAsync(int currentId)
        {
            await InitializeAsync();
            var currentCustomer = await GetCustomerAsync(currentId);
            if (currentCustomer == null) return null;

            try
            {
                // Tüm silinmemiş müşterileri hafızaya al
                var allCustomers = await _database.Table<Customer>()
                    .Where(c => !c.IsDeleted)
                    .ToListAsync();
                    
                // Türkçe kültürü için karşılaştırıcı oluştur
                var turkishCulture = new CultureInfo("tr-TR");
                    
                // Müşterileri isme göre sırala
                var sortedCustomers = allCustomers
                    .OrderBy(c => c.Name, StringComparer.Create(turkishCulture, ignoreCase: true))
                    .ToList();
                    
                // Mevcut müşterinin index'ini bul
                int currentIndex = sortedCustomers.FindIndex(c => c.Id == currentId);
                if (currentIndex <= 0) return null; // İlk müşteri ya da bulunamadıysa
                    
                // Bir önceki müşteriyi döndür
                return sortedCustomers[currentIndex - 1].Id;
            }
            catch (Exception e)
            {
                Console.WriteLine($"GetPreviousCustomerIdAsync hata: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Belirtilen müşteri ID'sine göre bir sonraki müşterinin ID'sini döndürür
        /// </summary>
        /// <param name="currentId">Mevcut müşteri ID'si</param>
        /// <returns>Bir sonraki müşteri ID'si veya müşteri yoksa null</returns>
        public async Task<int?> GetNextCustomerIdAsync(int currentId)
        {
            await InitializeAsync();
            var currentCustomer = await GetCustomerAsync(currentId);
            if (currentCustomer == null) return null;

            try
            {
                // Tüm silinmemiş müşterileri hafızaya al
                var allCustomers = await _database.Table<Customer>()
                    .Where(c => !c.IsDeleted)
                    .ToListAsync();
                    
                // Türkçe kültürü için karşılaştırıcı oluştur
                var turkishCulture = new CultureInfo("tr-TR");
                    
                // Müşterileri isme göre sırala
                var sortedCustomers = allCustomers
                    .OrderBy(c => c.Name, StringComparer.Create(turkishCulture, ignoreCase: true))
                    .ToList();
                    
                // Mevcut müşterinin index'ini bul
                int currentIndex = sortedCustomers.FindIndex(c => c.Id == currentId);
                if (currentIndex < 0 || currentIndex >= sortedCustomers.Count - 1) return null; // Son müşteri ya da bulunamadıysa
                    
                // Bir sonraki müşteriyi döndür
                return sortedCustomers[currentIndex + 1].Id;
            }
            catch (Exception e)
            {
                Console.WriteLine($"GetNextCustomerIdAsync hata: {e.Message}");
                return null;
            }
        }

        // Helper method to sort customers with Turkish culture support
        private List<Customer> ApplySorting(List<Customer> customers, string sortField, SortDirection? sortDirection)
        {
            // Create Turkish culture comparison for proper alphabetical sorting
            var turkishCulture = new CultureInfo("tr-TR");
            var turkishComparer = StringComparer.Create(turkishCulture, ignoreCase: true);

            // Apply culture-aware sorting
            if (!string.IsNullOrEmpty(sortField))
            {
                switch (sortField.ToLower())
                {
                    case "name":
                        return sortDirection == SortDirection.Ascending
                            ? customers.OrderBy(c => c.Name, turkishComparer).ToList()
                            : customers.OrderByDescending(c => c.Name, turkishComparer).ToList();
                    case "phone":
                        return sortDirection == SortDirection.Ascending
                            ? customers.OrderBy(c => c.PhoneNumber).ToList()
                            : customers.OrderByDescending(c => c.PhoneNumber).ToList();
                    default:
                        return customers.OrderBy(c => c.Name, turkishComparer).ToList();
                }
            }
            else
            {
                return customers.OrderBy(c => c.Name, turkishComparer).ToList();
            }
        }

        /// <summary>
        /// Tüm para birimleri için gelen ve giden toplam bakiyeleri hesaplar
        /// </summary>
        public async Task<CurrencyBalanceSummaryResult> GetCurrencyBalanceSummaryAsync()
        {
            await InitializeAsync();

            var result = new CurrencyBalanceSummaryResult();
            var customers = await GetCustomersAsync();
            var allTransactions = await GetTransactionsAsync();

            // Her müşteri için her para birimindeki net durumu hesapla
            var customerBalances = new List<CustomerBalance>();

            foreach (var customer in customers)
            {
                var customerTransactions = allTransactions
                    .Where(t => !t.IsDeleted && t.CustomerId == customer.Id && (!t.IsDeposit || t.IsDeposit == false))
                    .ToList();

                var currencyGroups = customerTransactions
                    .GroupBy(t => t.CurrencyType)
                    .ToList();

                foreach (var group in currencyGroups)
                {
                    var outgoing = group.Where(t => t.Type == TransactionType.CustomerDebt).Sum(t => t.Amount);
                    var incoming = group.Where(t => t.Type == TransactionType.StoreDebt).Sum(t => t.Amount);
                    var netBalance = outgoing - incoming;

                    if (netBalance != 0)
                    {
                        customerBalances.Add(new CustomerBalance
                        {
                            CustomerId = customer.Id,
                            CustomerName = customer.Name,
                            CurrencyType = group.Key,
                            NetBalance = netBalance
                        });
                    }
                }
            }

            // Toplamları hesapla - NetBalance < 0 ise Gelen (müşterinin borcu var), > 0 ise Giden (mağaza borçlu)
            result.ToplamGelenTL = customerBalances.Where(cb => cb.CurrencyType == CurrencyType.TurkishLira && cb.NetBalance < 0).Sum(cb => Math.Abs(cb.NetBalance));
            result.ToplamGidenTL = customerBalances.Where(cb => cb.CurrencyType == CurrencyType.TurkishLira && cb.NetBalance > 0).Sum(cb => cb.NetBalance);

            result.ToplamGelenDolar = customerBalances.Where(cb => cb.CurrencyType == CurrencyType.Dollar && cb.NetBalance < 0).Sum(cb => Math.Abs(cb.NetBalance));
            result.ToplamGidenDolar = customerBalances.Where(cb => cb.CurrencyType == CurrencyType.Dollar && cb.NetBalance > 0).Sum(cb => cb.NetBalance);

            result.ToplamGelenEuro = customerBalances.Where(cb => cb.CurrencyType == CurrencyType.Euro && cb.NetBalance < 0).Sum(cb => Math.Abs(cb.NetBalance));
            result.ToplamGidenEuro = customerBalances.Where(cb => cb.CurrencyType == CurrencyType.Euro && cb.NetBalance > 0).Sum(cb => cb.NetBalance);

            result.ToplamGelenSterlin = customerBalances.Where(cb => cb.CurrencyType == CurrencyType.Sterlin && cb.NetBalance < 0).Sum(cb => Math.Abs(cb.NetBalance));
            result.ToplamGidenSterlin = customerBalances.Where(cb => cb.CurrencyType == CurrencyType.Sterlin && cb.NetBalance > 0).Sum(cb => cb.NetBalance);

            result.ToplamGelenRiyal = customerBalances.Where(cb => cb.CurrencyType == CurrencyType.Riyal && cb.NetBalance < 0).Sum(cb => Math.Abs(cb.NetBalance));
            result.ToplamGidenRiyal = customerBalances.Where(cb => cb.CurrencyType == CurrencyType.Riyal && cb.NetBalance > 0).Sum(cb => cb.NetBalance);

            result.ToplamGelen14Ayar = customerBalances.Where(cb => cb.CurrencyType == CurrencyType.Gold14K && cb.NetBalance < 0).Sum(cb => Math.Abs(cb.NetBalance));
            result.ToplamGiden14Ayar = customerBalances.Where(cb => cb.CurrencyType == CurrencyType.Gold14K && cb.NetBalance > 0).Sum(cb => cb.NetBalance);

            result.ToplamGelen22Ayar = customerBalances.Where(cb => cb.CurrencyType == CurrencyType.Gold22K && cb.NetBalance < 0).Sum(cb => Math.Abs(cb.NetBalance));
            result.ToplamGiden22Ayar = customerBalances.Where(cb => cb.CurrencyType == CurrencyType.Gold22K && cb.NetBalance > 0).Sum(cb => cb.NetBalance);

            result.ToplamGelen24Ayar = customerBalances.Where(cb => cb.CurrencyType == CurrencyType.Gold24K && cb.NetBalance < 0).Sum(cb => Math.Abs(cb.NetBalance));
            result.ToplamGiden24Ayar = customerBalances.Where(cb => cb.CurrencyType == CurrencyType.Gold24K && cb.NetBalance > 0).Sum(cb => cb.NetBalance);

            result.ToplamGelenCeyrekAltin = customerBalances.Where(cb => cb.CurrencyType == CurrencyType.QuarterGold && cb.NetBalance < 0).Sum(cb => Math.Abs(cb.NetBalance));
            result.ToplamGidenCeyrekAltin = customerBalances.Where(cb => cb.CurrencyType == CurrencyType.QuarterGold && cb.NetBalance > 0).Sum(cb => cb.NetBalance);

            result.ToplamGelenYarimAltin = customerBalances.Where(cb => cb.CurrencyType == CurrencyType.HalfGold && cb.NetBalance < 0).Sum(cb => Math.Abs(cb.NetBalance));
            result.ToplamGidenYarimAltin = customerBalances.Where(cb => cb.CurrencyType == CurrencyType.HalfGold && cb.NetBalance > 0).Sum(cb => cb.NetBalance);

            result.ToplamGelenTamAltin = customerBalances.Where(cb => cb.CurrencyType == CurrencyType.FullGold && cb.NetBalance < 0).Sum(cb => Math.Abs(cb.NetBalance));
            result.ToplamGidenTamAltin = customerBalances.Where(cb => cb.CurrencyType == CurrencyType.FullGold && cb.NetBalance > 0).Sum(cb => cb.NetBalance);

            result.ToplamGelenZiynetAltin = customerBalances.Where(cb => cb.CurrencyType == CurrencyType.Ziynet && cb.NetBalance < 0).Sum(cb => Math.Abs(cb.NetBalance));
            result.ToplamGidenZiynetAltin = customerBalances.Where(cb => cb.CurrencyType == CurrencyType.Ziynet && cb.NetBalance > 0).Sum(cb => cb.NetBalance);

            return result;
        }

        private class CustomerBalance
        {
            public int CustomerId { get; set; }
            public string CustomerName { get; set; }
            public CurrencyType CurrencyType { get; set; }
            public decimal NetBalance { get; set; }
        }

        // AppSettings methods
        public async Task<AppSettings> GetSettingsAsync()
        {
            await InitializeAsync();
            var settings = await _database.Table<AppSettings>().FirstOrDefaultAsync();
            return settings ?? new AppSettings();
        }

        public async Task SaveSettingsAsync(AppSettings settings)
        {
            await InitializeAsync();
            if (settings.Id != 0)
                await _database.UpdateAsync(settings);
            else
                await _database.InsertAsync(settings);
        }

        public async Task<bool> IsSetupCompletedAsync()
        {
            await InitializeAsync();
            var settings = await _database.Table<AppSettings>().FirstOrDefaultAsync();
            return settings?.IsSetupCompleted ?? false;
        }
    }

    public class CurrencyBalanceSummaryResult
    {
        public decimal ToplamGelenTL { get; set; }
        public decimal ToplamGidenTL { get; set; }
        public decimal ToplamGelenDolar { get; set; }
        public decimal ToplamGidenDolar { get; set; }
        public decimal ToplamGelenEuro { get; set; }
        public decimal ToplamGidenEuro { get; set; }
        public decimal ToplamGelenSterlin { get; set; }
        public decimal ToplamGidenSterlin { get; set; }
        public decimal ToplamGelenRiyal { get; set; }
        public decimal ToplamGidenRiyal { get; set; }
        public decimal ToplamGelen14Ayar { get; set; }
        public decimal ToplamGiden14Ayar { get; set; }
        public decimal ToplamGelen22Ayar { get; set; }
        public decimal ToplamGiden22Ayar { get; set; }
        public decimal ToplamGelen24Ayar { get; set; }
        public decimal ToplamGiden24Ayar { get; set; }
        public decimal ToplamGelenCeyrekAltin { get; set; }
        public decimal ToplamGidenCeyrekAltin { get; set; }
        public decimal ToplamGelenYarimAltin { get; set; }
        public decimal ToplamGidenYarimAltin { get; set; }
        public decimal ToplamGelenTamAltin { get; set; }
        public decimal ToplamGidenTamAltin { get; set; }
        public decimal ToplamGelenZiynetAltin { get; set; }
        public decimal ToplamGidenZiynetAltin { get; set; }
    }

    public enum SortDirection
    {
        Ascending,
        Descending
    }
}
