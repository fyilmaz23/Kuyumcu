using SQLite;
using Kuyumcu.Models;
using System.Collections.ObjectModel;

namespace Kuyumcu.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection _database;
        private bool _isInitialized = false;

        public DatabaseService()
        {
        }

        private async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            var databasePath = Path.Combine(FileSystem.AppDataDirectory, "kuyumcu.db");
            _database = new SQLiteAsyncConnection(databasePath);

            await _database.CreateTableAsync<Customer>();
            await _database.CreateTableAsync<Transaction>();

            _isInitialized = true;
        }

        // Customer methods
        public async Task<List<Customer>> GetCustomersAsync()
        {
            await InitializeAsync();
            return await _database.Table<Customer>().OrderBy(c => c.Name).ToListAsync();
        }

        public async Task<Customer> GetCustomerAsync(int id)
        {
            await InitializeAsync();
            return await _database.Table<Customer>().Where(c => c.Id == id).FirstOrDefaultAsync();
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
            return await _database.DeleteAsync(customer);
        }

        // Transaction methods
        public async Task<List<Transaction>> GetTransactionsAsync()
        {
            await InitializeAsync();
            return await _database.Table<Transaction>().OrderByDescending(t => t.Date).ToListAsync();
        }

        public async Task<List<Transaction>> GetCustomerTransactionsAsync(int customerId)
        {
            await InitializeAsync();
            return await _database.Table<Transaction>()
                .Where(t => t.CustomerId == customerId)
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
            return await _database.Table<Transaction>().Where(t => t.Id == id).FirstOrDefaultAsync();
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
            return await _database.DeleteAsync(transaction);
        }
    }
}
