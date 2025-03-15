using SQLite;

namespace Kuyumcu.Models
{
    public enum TransactionType
    {
        CustomerDebt,    // Customer owes money to the jewelry store
        StoreDebt        // Jewelry store owes money to the customer
    }

    public class Transaction
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        
        [Indexed]
        public int CustomerId { get; set; }
        
        public decimal Amount { get; set; }
        
        public TransactionType Type { get; set; }
        
        public CurrencyType CurrencyType { get; set; } = CurrencyType.TurkishLira;
        
        public DateTime Date { get; set; }
        
        [MaxLength(255)]
        public string? Description { get; set; }

        public string GetFormattedAmount()
        {
            return $"{Amount} {CurrencyType.GetSymbol()}";
        }
    }
}
