using SQLite;
using System.ComponentModel.DataAnnotations;
using MaxLengthAttribute = System.ComponentModel.DataAnnotations.MaxLengthAttribute;

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

        [Required(ErrorMessage = "Tutar alanı zorunludur")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Tutar sıfırdan büyük olmalıdır")]
        public decimal Amount { get; set; }
        
        public TransactionType Type { get; set; }
        
        public CurrencyType CurrencyType { get; set; } = CurrencyType.TurkishLira;
        
        public DateTime Date { get; set; }
        
        [MaxLength(255)]
        public string? Description { get; set; }
        
        /// <summary>
        /// Kaydın silinip silinmediğini belirtir (Soft delete için)
        /// </summary>
        public bool IsDeleted { get; set; } = false;

        /// <summary>
        /// Emanet olarak işaretlenmişse true olur (Sadece Gelen işlemler için)
        /// </summary>
        public bool IsDeposit { get; set; } = false;
        
        /// <summary>
        /// İşlemin gizli olup olmadığını belirtir. Gizli işlemler sadece görünürlük açıkken gösterilir.
        /// </summary>
        public bool IsHidden { get; set; } = false;

        public string GetFormattedAmount()
        {
            return $"{Amount} {CurrencyType.GetSymbol()}";
        }
    }
}
