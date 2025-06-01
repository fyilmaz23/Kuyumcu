using System;

namespace Kuyumcu.Models
{
    public class GoldPriceDto
    {
        public string Type { get; set; }
        public decimal BuyPrice { get; set; }
        public decimal SellPrice { get; set; }
        public DateTime UpdateTime { get; set; }

        public GoldPriceDto(string type, decimal buyPrice, decimal sellPrice)
        {
            Type = type;
            BuyPrice = buyPrice;
            SellPrice = sellPrice;
            UpdateTime = DateTime.Now;
        }
    }
}
