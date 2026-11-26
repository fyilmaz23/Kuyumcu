using System.Globalization;
using Kuyumcu.Models;
using Kuyumcu.Services;

namespace Kuyumcu.Stock;

/// <summary>
/// Kasa ürününün veresiye satışı: müşteri defterine yazılacak borcun cinsi ve miktarı.
/// Borç cinsi her satışta seçilir (kullanıcı kararı, 26.11.2026):
/// - ürünün kendi cinsi (Çeyrek → Çeyrek Altın adet, Yarım, Tam, gramla tutulan 14/22/24 ayar → o ayarın gramı),
/// - TL (fiyat önerisi canlı fiyat listesinden),
/// - 22 ayar gram karşılığı (etiketli ürün satışındaki gibi: ağırlık × milyem / 22 ayar milyemi).
/// Platformdan ve veritabanından bağımsız; birim testli.
/// </summary>
public static class BulkSale
{
    private static readonly CultureInfo Turkish = new("tr-TR");

    /// <summary>
    /// Ürünün defterdeki kendi cinsi; yoksa null (ör. Ata Lira: defterde ayrı cinsi yok → TL ya da 22 ayar seçilir).
    /// Adetliler ada göre tanınır (çeyrek / yarım / tam); gramla tutulanlar ayarına göre.
    /// </summary>
    public static CurrencyType? OwnCurrency(BulkProduct product)
    {
        // Defter cinsine bağlı ürün (bkz. LedgerCash) o cinstir
        if (product.LedgerCurrencyType is { } linked)
            return linked;
        if (product.Unit == BulkUnit.Money)
            return null;
        return NameCurrency(product);
    }

    /// <summary>Ürün adından ya da gram ürünün ayarından tahmin edilen cins (bağlantı kurulurken de kullanılır).</summary>
    public static CurrencyType? NameCurrency(BulkProduct product)
    {
        if (product.Unit == BulkUnit.Gram)
        {
            return product.Karat switch
            {
                24 => CurrencyType.Gold24K,
                22 => CurrencyType.Gold22K,
                14 => CurrencyType.Gold14K,
                _ => null
            };
        }

        var words = Words(product.Name);
        if (words.Contains("ziynet"))
            return CurrencyType.Ziynet;
        if (words.Contains("çeyrek"))
            return CurrencyType.QuarterGold;
        if (words.Contains("yarım"))
            return CurrencyType.HalfGold;
        if (words.Contains("tam"))
            return CurrencyType.FullGold;
        return null;
    }

    /// <summary>Satışta seçilebilecek borç cinsleri: kendi cinsi (varsa) önce, sonra TL ve 22 ayar gram.</summary>
    public static IReadOnlyList<CurrencyType> CreditCurrencies(BulkProduct product)
    {
        var list = new List<CurrencyType>();
        if (OwnCurrency(product) is { } own)
            list.Add(own);
        foreach (var c in new[] { CurrencyType.TurkishLira, CurrencyType.Gold22K })
            if (!list.Contains(c))
                list.Add(c);
        return list;
    }

    /// <summary>Satılan miktarın ağırlığı (mg). <paramref name="amount"/>: adetliyse adet, gramlıysa mg.</summary>
    public static long WeightMg(BulkProduct product, long amount) =>
        product.Unit == BulkUnit.Gram ? amount : amount * product.PieceWeightMg;

    /// <summary>
    /// Seçilen cinste önerilen borç (defter gibi 2 ondalık). Kendi cinsi: adet ya da gram. 22 ayar: ağırlık × milyem / 22 ayar milyemi.
    /// TL: <see cref="SuggestPrice"/>. Hesaplanamıyorsa 0 (elle girilir).
    /// </summary>
    public static decimal SuggestCredit(BulkProduct product, long amount, CurrencyType currency, int milyem, int milyem22,
        IReadOnlyList<GoldPriceDto>? prices)
    {
        if (amount <= 0)
            return 0;
        if (currency == OwnCurrency(product))
            return product.Unit == BulkUnit.Gram ? Math.Round(amount / 1000m, 2, MidpointRounding.AwayFromZero) : amount;
        if (currency == CurrencyType.Gold22K)
            return StockCalculator.Suggest22KGram((int)Math.Min(int.MaxValue, WeightMg(product, amount)), milyem, 0, milyem22);
        if (currency == CurrencyType.TurkishLira)
            return SuggestPrice(product, amount, milyem, prices);
        return 0;
    }

    /// <summary>
    /// TL fiyat önerisi. Fiyat listesinde ürünün satırı varsa (Çeyrek, Yarım, "Tam (Ata)", "Gram Altın") miktar × satış;
    /// yoksa has karşılığı × 24 ayar satış. En yakın 10 TL'ye yuvarlanır; ekranda değiştirilebilir.
    /// </summary>
    public static decimal SuggestPrice(BulkProduct product, long amount, int milyem, IReadOnlyList<GoldPriceDto>? prices)
    {
        if (amount <= 0 || prices is not { Count: > 0 })
            return 0;

        decimal total;
        var row = FindPriceRow(product, prices);
        if (row is { SellPrice: > 0 })
        {
            total = product.Unit == BulkUnit.Gram ? amount / 1000m * row.SellPrice : amount * row.SellPrice;
        }
        else
        {
            var has = prices.FirstOrDefault(p => p.Type.Trim().Equals("24 Ayar", StringComparison.OrdinalIgnoreCase));
            if (has is not { SellPrice: > 0 })
                return 0;
            total = StockCalculator.HasMg(WeightMg(product, amount), milyem) / 1000m * has.SellPrice;
        }
        return Math.Round(total / 10m, MidpointRounding.AwayFromZero) * 10m;
    }

    /// <summary>Fiyat listesinde ürüne karşılık gelen satır (ada göre); bulunamazsa null.</summary>
    public static GoldPriceDto? FindPriceRow(BulkProduct product, IReadOnlyList<GoldPriceDto> prices)
    {
        if (product.Unit == BulkUnit.Gram)
            return product.Karat == 24 ? prices.FirstOrDefault(p => Words(p.Type).Contains("gram")) : SalePrice.FindKaratRow(prices, product.Karat);
        if (product.Unit == BulkUnit.Money)
            return null;

        var words = Words(product.Name);
        string? key = words.Contains("çeyrek") ? "çeyrek"
            : words.Contains("yarım") ? "yarım"
            : words.Contains("tam") ? "tam"
            : words.Contains("ata") ? "ata"
            : words.Contains("beşli") ? "beşli"
            : null;
        return key == null ? null : prices.FirstOrDefault(p => Words(p.Type).Contains(key));
    }

    /// <summary>Defter açıklaması: "Kasa satışı: 2 adet Çeyrek".</summary>
    public static string Description(BulkProduct product, long amount, string? note)
    {
        var text = $"Kasa satışı: {StockService.FormatBulkAmount(product, amount)} {product.Name}";
        if (!string.IsNullOrWhiteSpace(note))
            text += " - " + note.Trim();
        return text.Length > 255 ? text[..255] : text;
    }

    // Türkçe küçük harfe çevirip harf/rakam dışı her karakterden böler: "Tam (Ata)" → { "tam", "ata" }
    private static HashSet<string> Words(string? text)
    {
        var words = new HashSet<string>(StringComparer.Ordinal);
        var lower = (text ?? string.Empty).ToLower(Turkish);
        var start = 0;
        for (var i = 0; i <= lower.Length; i++)
        {
            if (i < lower.Length && char.IsLetterOrDigit(lower[i]))
                continue;
            if (i > start)
                words.Add(lower[start..i]);
            start = i + 1;
        }
        return words;
    }
}
