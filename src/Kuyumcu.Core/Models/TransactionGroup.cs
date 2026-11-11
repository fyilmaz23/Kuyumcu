using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kuyumcu.Models
{
    public class TransactionGroup
    {
        public CurrencyType Key { get; set; }
        public List<Transaction> Transactions { get; set; }
        public decimal Balance { get; set; }

        /// <summary>
        /// Her işlemden sonra oluşan kümülatif kalan (çalışan bakiye).
        /// Anahtar: işlem Id'si, Değer: o işlem dahil edildikten sonraki kalan.
        /// Değer, grubun Balance'ı ile aynı işaret kuralını kullanır:
        /// pozitif => Giden (müşteri borçlu), negatif => Gelen (dükkan borçlu).
        /// Kalan, pagination'dan bağımsız olarak grubun tüm geçmişi üzerinden hesaplanır.
        /// </summary>
        public Dictionary<int, decimal> RunningBalances { get; set; } = new Dictionary<int, decimal>();
    }
}
