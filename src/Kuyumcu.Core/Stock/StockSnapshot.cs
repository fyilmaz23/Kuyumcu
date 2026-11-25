using Kuyumcu.Models;
using SQLite;

namespace Kuyumcu.Stock;

/// <summary>
/// Yedekteki vitrin stoğunun ve kasa ürünlerinin belleğe alınmış, salt okunur görüntüsü (Android uygulaması).
/// Stok tabloları olmayan eski yedeklerde <see cref="Empty"/> kullanılır.
/// </summary>
public sealed class StockSnapshot
{
    private readonly Dictionary<int, StockItem> _itemsById;
    private readonly Dictionary<int, StockCategory> _categories;
    private readonly Dictionary<int, StockLocation> _locations;
    private readonly Dictionary<int, int> _milyems;
    private readonly ILookup<int, StockMovement> _movementsByItem;

    public StockSnapshot(
        IEnumerable<StockItem> items,
        IEnumerable<BulkProduct> bulkProducts,
        IEnumerable<StockCategory> categories,
        IEnumerable<StockLocation> locations,
        IEnumerable<KaratDefinition> karats,
        IEnumerable<StockMovement> movements)
    {
        Items = items.Where(i => !i.IsDeleted)
            .OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id)
            .ToList();
        BulkProducts = bulkProducts.Where(b => !b.IsDeleted)
            .OrderBy(b => b.SortOrder).ThenBy(b => b.Id)
            .ToList();
        Karats = karats.ToList();

        _itemsById = Items.ToDictionary(i => i.Id);
        _categories = categories.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());
        _locations = locations.GroupBy(l => l.Id).ToDictionary(g => g.Key, g => g.First());
        _milyems = Karats.Where(k => !k.IsDeleted).GroupBy(k => k.Karat).ToDictionary(g => g.Key, g => g.First().Milyem);
        _movementsByItem = movements.Where(m => m.ItemId > 0)
            .OrderByDescending(m => m.Date).ThenByDescending(m => m.Id)
            .ToLookup(m => m.ItemId);

        InStockTotals = StockCalculator.Calculate(Items.Where(i => i.Status == StockItemStatus.InStock), Karats);
        BulkTotals = BulkProducts.Select(b => StockCalculator.CalculateBulk(b, Karats)).ToList();
    }

    public static readonly StockSnapshot Empty = new([], [], [], [], [], []);

    /// <summary>Silinmemiş etiketli ürünler (her durumda), yeniden eskiye.</summary>
    public IReadOnlyList<StockItem> Items { get; }

    /// <summary>Silinmemiş kasa ürünleri, sıralı.</summary>
    public IReadOnlyList<BulkProduct> BulkProducts { get; }

    public IReadOnlyList<KaratDefinition> Karats { get; }

    /// <summary>Vitrindeki ürünlerin ayar bazında toplamı.</summary>
    public StockTotals InStockTotals { get; }

    /// <summary>Kasa ürünlerinin ağırlık ve has karşılıkları (<see cref="BulkProducts"/> ile aynı sırada).</summary>
    public IReadOnlyList<BulkTotal> BulkTotals { get; }

    /// <summary>
    /// Gösterilecek stok var mı? Kasa ürünleri kurulumda varsayılan olarak (miktarı 0) eklendiği için
    /// sadece miktarı olanlar sayılır.
    /// </summary>
    public bool HasAny => Items.Count > 0 || BulkProducts.Any(b => b.Amount != 0);

    public StockItem? GetItem(int id) => _itemsById.GetValueOrDefault(id);

    public IEnumerable<StockMovement> GetMovements(int itemId) => _movementsByItem[itemId];

    public string CategoryName(int id) => _categories.GetValueOrDefault(id)?.Name ?? "—";

    public string LocationName(int id) => _locations.GetValueOrDefault(id)?.Name ?? "—";

    public int Milyem(int karat) => _milyems.TryGetValue(karat, out var m) ? m : StockCalculator.DefaultMilyem(karat);

    /// <summary>Has karşılığı (mg), net ağırlıktan.</summary>
    public long HasMg(StockItem item) => StockCalculator.HasMg(item.NetWeightMg, Milyem(item.Karat));

    /// <summary>"Bilezik · Burgu"</summary>
    public string Title(StockItem item) =>
        string.IsNullOrWhiteSpace(item.ModelName) ? CategoryName(item.CategoryId) : $"{CategoryName(item.CategoryId)} · {item.ModelName}";

    /// <summary>
    /// Yedek veritabanından okur. Stok tabloları yoksa (eski sürümün yedeği) <see cref="Empty"/> döner.
    /// </summary>
    public static StockSnapshot Load(SQLiteConnection db, ISet<string> tables)
    {
        if (!tables.Contains("StockItem") && !tables.Contains("BulkProduct"))
            return Empty;

        List<T> Read<T>(string table) where T : new() =>
            tables.Contains(table) ? db.Query<T>($"SELECT * FROM \"{table}\"") : [];

        return new StockSnapshot(
            Read<StockItem>("StockItem"),
            Read<BulkProduct>("BulkProduct"),
            Read<StockCategory>("StockCategory"),
            Read<StockLocation>("StockLocation"),
            Read<KaratDefinition>("KaratDefinition"),
            Read<StockMovement>("StockMovement"));
    }
}
