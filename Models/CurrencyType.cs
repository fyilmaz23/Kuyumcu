using System;

namespace Kuyumcu.Models
{
    public enum CurrencyType
    {
        TurkishLira,
        Dollar,
        Euro,
        Sterlin,
        Riyal,
        Gold14K,       // 14 Ayar Altın (gr)
        Gold22K,       // 22 Ayar Altın (gr)
        Gold24K,       // 24 Ayar Altın (gr)
        QuarterGold,   // Çeyrek Altın (adet)
        HalfGold,      // Yarım Altın (adet)
        FullGold       // Tam Altın (adet)
    }

    public static class CurrencyTypeExtensions
    {
        public static string GetDisplayName(this CurrencyType currencyType)
        {
            return currencyType switch
            {
                CurrencyType.TurkishLira => "TL",
                CurrencyType.Dollar => "Dolar",
                CurrencyType.Euro => "Euro",
                CurrencyType.Sterlin => "Sterlin",
                CurrencyType.Riyal => "Riyal",
                CurrencyType.Gold14K => "14 Ayar",
                CurrencyType.Gold22K => "22 Ayar",
                CurrencyType.Gold24K => "24 Ayar",
                CurrencyType.QuarterGold => "Çeyrek Altın",
                CurrencyType.HalfGold => "Yarım Altın",
                CurrencyType.FullGold => "Tam Altın",
                _ => currencyType.ToString()
            };
        }

        public static string GetSymbol(this CurrencyType currencyType)
        {
            return currencyType switch
            {
                CurrencyType.TurkishLira => "₺",
                CurrencyType.Dollar => "$",
                CurrencyType.Euro => "€",
                CurrencyType.Sterlin => "£",
                CurrencyType.Riyal => "﷼",
                CurrencyType.Gold14K => "gr",
                CurrencyType.Gold22K => "gr",
                CurrencyType.Gold24K => "gr",
                CurrencyType.QuarterGold => "adet",
                CurrencyType.HalfGold => "adet",
                CurrencyType.FullGold => "adet",
                _ => ""
            };
        }
    }
}
