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
    }
}
