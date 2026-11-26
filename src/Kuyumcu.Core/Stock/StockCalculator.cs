using Kuyumcu.Models;

namespace Kuyumcu.Stock;

/// <summary>Bir ayarın toplamı.</summary>
public sealed record KaratTotal(int Karat, int Milyem, int Count, long WeightMg, long NetWeightMg, long HasMg)
{
    public decimal Gram => WeightMg / 1000m;
    public decimal NetGram => NetWeightMg / 1000m;
    public decimal HasGram => HasMg / 1000m;
}

/// <summary>Stok özeti: ayar bazında adet, brüt gram, net gram ve has karşılığı.</summary>
public sealed record StockTotals(IReadOnlyList<KaratTotal> ByKarat)
{
    public int Count => ByKarat.Sum(k => k.Count);
    public long WeightMg => ByKarat.Sum(k => k.WeightMg);
    public long NetWeightMg => ByKarat.Sum(k => k.NetWeightMg);
    public long HasMg => ByKarat.Sum(k => k.HasMg);

    public static readonly StockTotals Empty = new(Array.Empty<KaratTotal>());
}

/// <summary>Kasa ürününün mevcut durumu.</summary>
public sealed record BulkTotal(BulkProduct Product, int Milyem, long WeightMg, long HasMg);

/// <summary>Sayım karşılaştırması: beklenen ürünler ile okutulan kodlar.</summary>
public sealed record CountComparison(
    IReadOnlyList<StockItem> Found,
    IReadOnlyList<StockItem> Missing,
    IReadOnlyList<string> Extra);

/// <summary>Stok hesapları (platformdan ve veritabanından bağımsız; birim testli).</summary>
public static class StockCalculator
{
    /// <summary>Has karşılığı (mg) = net ağırlık × milyem / 1000, en yakın mg'a yuvarlanır.</summary>
    public static long HasMg(long netWeightMg, int milyem) =>
        (long)Math.Round(netWeightMg * (decimal)milyem / 1000m, MidpointRounding.AwayFromZero);

    /// <summary>Verilen ürünlerin ayar bazında toplamı (filtreleme çağırana aittir, genelde sadece vitrindekiler).</summary>
    public static StockTotals Calculate(IEnumerable<StockItem> items, IEnumerable<KaratDefinition> karats)
    {
        var milyems = karats.GroupBy(k => k.Karat).ToDictionary(g => g.Key, g => g.First().Milyem);

        var byKarat = items
            .GroupBy(i => i.Karat)
            .Select(g =>
            {
                var milyem = milyems.TryGetValue(g.Key, out var m) ? m : DefaultMilyem(g.Key);
                long weight = g.Sum(i => (long)i.WeightMg);
                long net = g.Sum(i => (long)i.NetWeightMg);
                return new KaratTotal(g.Key, milyem, g.Count(), weight, net, HasMg(net, milyem));
            })
            .OrderByDescending(k => k.Karat)
            .ToList();

        return new StockTotals(byKarat);
    }

    /// <summary>Kasa ürününün toplam ağırlığı ve has karşılığı.</summary>
    public static BulkTotal CalculateBulk(BulkProduct product, IEnumerable<KaratDefinition> karats)
    {
        var milyem = karats.FirstOrDefault(k => k.Karat == product.Karat)?.Milyem ?? DefaultMilyem(product.Karat);
        // Nakit/döviz has ve grama girmez
        var weight = product.Unit switch { BulkUnit.Gram => product.Amount, BulkUnit.Piece => product.Amount * product.PieceWeightMg, _ => 0 };
        return new BulkTotal(product, milyem, weight, HasMg(weight, milyem));
    }

    /// <summary>Tanımı olmayan ayar için yaklaşık milyem (ayar / 24).</summary>
    public static int DefaultMilyem(int karat) => (int)Math.Round(karat * 1000m / 24m);

    /// <summary>
    /// Satış fiyatı önerisi (TL) = net gram × (ayar milyemi + işçilik milyemi) × has fiyatı.
    /// En yakın 10 TL'ye yuvarlanır; ekranda değiştirilebilir.
    /// </summary>
    public static decimal SuggestSalePrice(int netWeightMg, int milyem, int laborMilyem, decimal hasPrice)
    {
        if (netWeightMg <= 0 || hasPrice <= 0)
            return 0;
        var price = netWeightMg / 1000m * (milyem + laborMilyem) / 1000m * hasPrice;
        return Math.Round(price / 10m, MidpointRounding.AwayFromZero) * 10m;
    }

    /// <summary>
    /// Veresiye satışta borcun 22 ayar gram karşılığı = net gram × (milyem + işçilik) / 22 ayar milyemi (2 ondalık).
    /// </summary>
    public static decimal Suggest22KGram(int netWeightMg, int milyem, int laborMilyem, int milyem22)
    {
        if (netWeightMg <= 0 || milyem22 <= 0)
            return 0;
        return Math.Round(netWeightMg / 1000m * (milyem + laborMilyem) / milyem22, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Sayım sonucu: okutulan ve beklenen, eksik (okutulmamış) ve fazla (beklenmeyen kod).</summary>
    public static CountComparison CompareCount(IEnumerable<StockItem> expected, IEnumerable<string> scannedCodes)
    {
        var scanned = new HashSet<string>(scannedCodes, StringComparer.Ordinal);
        var expectedList = expected.ToList();
        var expectedCodes = new HashSet<string>(expectedList.Select(i => i.Code), StringComparer.Ordinal);

        var found = expectedList.Where(i => scanned.Contains(i.Code)).ToList();
        var missing = expectedList.Where(i => !scanned.Contains(i.Code)).ToList();
        var extra = scanned.Where(c => !expectedCodes.Contains(c)).OrderBy(c => c, StringComparer.Ordinal).ToList();
        return new CountComparison(found, missing, extra);
    }
}
