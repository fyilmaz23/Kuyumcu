using Kuyumcu.Models;
using Kuyumcu.Services;
using Kuyumcu.Stock;

namespace Kuyumcu.UI.Services;

/// <summary>Okutma alanı ne bekliyor: ürün kodu mu, gram mı.</summary>
public enum ScanInputMode
{
    Code,
    Decimal
}

/// <summary>
/// Stok ekranlarında ayar, kategori ve konum adlarını çözmek için bir kerelik yüklenen sözlük.
/// Android'de yedekten okunan listelerle de kurulabilir.
/// </summary>
public sealed class StockLookup
{
    public StockLookup(List<KaratDefinition> karats, List<StockCategory> categories, List<StockLocation> locations)
    {
        AllKarats = karats;
        AllCategories = categories;
        AllLocations = locations;
    }

    public List<KaratDefinition> AllKarats { get; }
    public List<StockCategory> AllCategories { get; }
    public List<StockLocation> AllLocations { get; }

    /// <summary>Seçim listeleri için (silinmemiş).</summary>
    public IEnumerable<KaratDefinition> Karats => AllKarats.Where(k => !k.IsDeleted).OrderByDescending(k => k.Karat);
    public IEnumerable<StockCategory> Categories => AllCategories.Where(c => !c.IsDeleted);
    public IEnumerable<StockLocation> Locations => AllLocations.Where(l => !l.IsDeleted);

    public static async Task<StockLookup> LoadAsync(StockService stock) =>
        new(await stock.GetKaratsAsync(true), await stock.GetCategoriesAsync(true), await stock.GetLocationsAsync(true));

    public StockCategory? Category(int id) => AllCategories.FirstOrDefault(c => c.Id == id);
    public string CategoryName(int id) => Category(id)?.Name ?? "—";
    public string LocationName(int id) => AllLocations.FirstOrDefault(l => l.Id == id)?.Name ?? "—";
    public int Milyem(int karat) => AllKarats.FirstOrDefault(k => k.Karat == karat)?.Milyem ?? StockCalculator.DefaultMilyem(karat);
    public int LaborMilyem(int categoryId) => Category(categoryId)?.LaborMilyem ?? 0;

    /// <summary>"Bilezik · Burgu"</summary>
    public string Title(StockItem item) =>
        string.IsNullOrWhiteSpace(item.ModelName) ? CategoryName(item.CategoryId) : $"{CategoryName(item.CategoryId)} · {item.ModelName}";
}

/// <summary>Okutma alanında Enter'a basıldı. Giriş geçersizse işleyen sayfa <see cref="Accepted"/> = false yapar.</summary>
public sealed class ScanSubmitEventArgs(string value)
{
    public string Value { get; } = value;
    public bool Accepted { get; set; } = true;
}
