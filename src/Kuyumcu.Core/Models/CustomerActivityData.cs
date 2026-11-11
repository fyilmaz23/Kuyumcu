namespace Kuyumcu.Models
{
    /// <summary>
    /// Müşteri aktivite verileri için kullanılan DTO sınıfı
    /// </summary>
    public class CustomerActivityData
    {
        /// <summary>
        /// Müşteri ID
        /// </summary>
        public int CustomerId { get; set; }

        /// <summary>
        /// Müşteri Adı
        /// </summary>
        public string CustomerName { get; set; } = "Bilinmeyen Müşteri";

        /// <summary>
        /// İşlem sayısı
        /// </summary>
        public int TransactionCount { get; set; }
    }
}
