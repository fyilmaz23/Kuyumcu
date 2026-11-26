using Kuyumcu.Models;

namespace Kuyumcu.Stock;

/// <summary>
/// Etiketli ürünün satış fiyatı hesabı (kullanıcı kararı, 26.11.2026). Dükkânda yapıldığı gibi:
/// <c>net gram × fiyat listesindeki aynı ayarın satış fiyatı + net gram × işçilik (TL/gr)</c>.
/// Örnek: 20 gr 22 ayar bilezik, listede 22 Ayar satış 5.990 → 20 × 5.990 = 119.800 TL (+ işçilik).
/// Listede ürünün ayarı yoksa (8, 18, 21 ayar) 24 ayar satış × ayar milyemi kullanılır ve ekranda bu yazılır.
/// Net gram = brüt − taş (taş girilmemişse etiketteki gram).
/// </summary>
public static class SalePrice
{
    /// <param name="Gram">Hesaba giren (net) gram.</param>
    /// <param name="UnitPrice">Gram fiyatı (TL).</param>
    /// <param name="Source">Gram fiyatının nereden geldiği (ekranda gösterilir).</param>
    /// <param name="FromList">Gram fiyatı listede ürünün kendi ayarından mı geldi?</param>
    /// <param name="Labor">İşçilik toplamı (TL).</param>
    /// <param name="Total">Son fiyat (TL, tam sayıya yuvarlanmış).</param>
    public sealed record Quote(decimal Gram, decimal UnitPrice, string Source, bool FromList, decimal LaborPerGram, decimal Labor, decimal Total)
    {
        /// <summary>Gram × gram fiyatı (işçiliksiz).</summary>
        public decimal GoldValue => Total - Labor;
    }

    /// <summary>Fiyat listesinde "22 Ayar" gibi ayar satırı (gram altın, çeyrek vb. değil).</summary>
    public static GoldPriceDto? FindKaratRow(IReadOnlyList<GoldPriceDto>? prices, int karat)
    {
        if (prices == null)
            return null;
        var name = $"{karat} Ayar";
        return prices.FirstOrDefault(p => p.Type.Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
               ?? prices.FirstOrDefault(p => p.Type.Trim().StartsWith(name + " ", StringComparison.OrdinalIgnoreCase)
                                             && !p.Type.Contains("gram", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Ürünün gram fiyatını listeden bulur. Ayarın satırı yoksa 24 ayar satış × milyem. Bulunamazsa null.
    /// </summary>
    public static (decimal Price, string Source, bool FromList)? UnitPriceFor(int karat, int milyem, IReadOnlyList<GoldPriceDto>? prices)
    {
        var row = FindKaratRow(prices, karat);
        if (row is { SellPrice: > 0 })
            return (row.SellPrice, $"{row.Type.Trim()} satış", true);

        var has = FindKaratRow(prices, 24);
        if (has is { SellPrice: > 0 } && milyem > 0)
        {
            var price = Math.Round(has.SellPrice * milyem / 1000m, 2, MidpointRounding.AwayFromZero);
            return (price, $"listede {karat} ayar yok: 24 Ayar satış × {milyem / 1000m:0.000}", false);
        }
        return null;
    }

    /// <summary>Gram fiyatı ve işçilikle son fiyat. <paramref name="unitPrice"/> 0 ise null.</summary>
    public static Quote? Calculate(int netWeightMg, decimal unitPrice, string source, bool fromList, decimal laborPerGram)
    {
        if (netWeightMg <= 0 || unitPrice <= 0)
            return null;
        var gram = netWeightMg / 1000m;
        var labor = Math.Round(gram * Math.Max(0, laborPerGram), 2, MidpointRounding.AwayFromZero);
        var total = Math.Round(gram * unitPrice + labor, 0, MidpointRounding.AwayFromZero);
        return new Quote(gram, unitPrice, source, fromList, Math.Max(0, laborPerGram), labor, total);
    }

    /// <summary>Listeden gram fiyatını bulup hesaplar; fiyat yoksa null.</summary>
    public static Quote? Calculate(int netWeightMg, int karat, int milyem, decimal laborPerGram, IReadOnlyList<GoldPriceDto>? prices) =>
        UnitPriceFor(karat, milyem, prices) is { } unit
            ? Calculate(netWeightMg, unit.Price, unit.Source, unit.FromList, laborPerGram)
            : null;

    /// <summary>
    /// Veresiye satışta borcun 22 ayar gram karşılığı = satış fiyatı ÷ listedeki 22 ayar satış (2 ondalık).
    /// 22 ayar ürünü işçiliksiz satınca borç ürünün kendi gramı olur.
    /// </summary>
    public static decimal? To22KGram(decimal price, IReadOnlyList<GoldPriceDto>? prices)
    {
        var row = FindKaratRow(prices, 22);
        if (price <= 0 || row is not { SellPrice: > 0 })
            return null;
        return Math.Round(price / row.SellPrice, 2, MidpointRounding.AwayFromZero);
    }
}
