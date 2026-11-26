using Kuyumcu.Models;
using SQLite;

namespace Kuyumcu.Stock;

/// <summary>
/// Stok tablolarının ilk açılıştaki varsayılan kayıtları. Tablo boşsa eklenir; kullanıcı sonradan değiştirebilir.
/// Silinen kayıtlar (IsDeleted) satır olarak durduğu için varsayılanlar bir daha eklenmez.
/// </summary>
public static class StockDefaults
{
    /// <summary>Ayar → milyem (binde).</summary>
    public static readonly (int Karat, int Milyem)[] Karats =
    {
        (8, 333), (14, 585), (18, 750), (21, 875), (22, 916), (24, 995)
    };

    public static readonly (string Name, string Short, int Karat)[] Categories =
    {
        ("Bilezik", "BLZ", 22), ("Kolye", "KLY", 14), ("Yüzük", "YZK", 14), ("Künye", "KNY", 22),
        ("Zincir", "ZNC", 14), ("Küpe", "KPE", 14), ("Bileklik", "BLK", 14), ("Set", "SET", 22)
    };

    public static readonly string[] Locations = { "Vitrin 1", "Vitrin 2", "Kasa" };

    public static readonly (string Name, BulkUnit Unit, int Karat, int PieceMg)[] BulkProducts =
    {
        ("Çeyrek", BulkUnit.Piece, 22, 1754),
        ("Yarım", BulkUnit.Piece, 22, 3508),
        ("Tam", BulkUnit.Piece, 22, 7016),
        ("Ata Lira", BulkUnit.Piece, 22, 7216),
        ("24 Ayar Gram Altın", BulkUnit.Gram, 24, 0)
    };

    /// <summary>Defter cinsi → kasa ürünü yoksa açılacak ürün (ad, birim, ayar, adet ağırlığı).</summary>
    public static (string Name, BulkUnit Unit, int Karat, int PieceMg) LedgerProductFor(CurrencyType currency) => currency switch
    {
        CurrencyType.QuarterGold => ("Çeyrek", BulkUnit.Piece, 22, 1754),
        CurrencyType.HalfGold => ("Yarım", BulkUnit.Piece, 22, 3508),
        CurrencyType.FullGold => ("Tam", BulkUnit.Piece, 22, 7016),
        CurrencyType.Ziynet => ("Ziynet", BulkUnit.Piece, 22, 1754),
        CurrencyType.Gold24K => ("24 Ayar Gram Altın", BulkUnit.Gram, 24, 0),
        CurrencyType.Gold22K => ("22 Ayar Gram", BulkUnit.Gram, 22, 0),
        CurrencyType.Gold14K => ("14 Ayar Gram", BulkUnit.Gram, 14, 0),
        _ => (currency.GetDisplayName(), BulkUnit.Money, 0, 0), // TL, Dolar, Euro, Sterlin, Riyal
    };

    /// <summary>
    /// Her defter cinsine bir kasa ürünü bağlar (bkz. <see cref="LedgerCash"/>). Bağlantısı olan cinse dokunulmaz
    /// (bağlı ürün silinmiş olsa bile: kullanıcı o cinsi kasada tutmak istemiyor demektir). Yoksa aynı cinsteki bağsız
    /// ürün (ör. kurulumdaki "Çeyrek") bağlanır, o da yoksa yeni ürün açılır. Her açılışta çalışır; ikinci kez bir şey yapmaz.
    /// </summary>
    public static async Task EnsureLedgerProductsAsync(SQLiteAsyncConnection db)
    {
        var products = await db.Table<BulkProduct>().ToListAsync();
        var order = products.Count == 0 ? 0 : products.Max(p => p.SortOrder) + 1;
        foreach (var currency in Enum.GetValues<CurrencyType>())
        {
            if (products.Any(p => p.LedgerCurrency == (int)currency))
                continue;

            var match = products.FirstOrDefault(p => !p.IsDeleted && p.LedgerCurrency == null && p.Unit != BulkUnit.Money
                                                     && BulkSale.NameCurrency(p) == currency);
            if (match != null)
            {
                match.LedgerCurrency = (int)currency;
                await db.UpdateAsync(match);
                continue;
            }

            var d = LedgerProductFor(currency);
            var name = products.Any(p => !p.IsDeleted && string.Equals(p.Name, d.Name, StringComparison.CurrentCultureIgnoreCase))
                ? $"{d.Name} (defter)" : d.Name;
            var product = new BulkProduct
            {
                Name = name, Unit = d.Unit, Karat = d.Karat, PieceWeightMg = d.PieceMg,
                SortOrder = order++, LedgerCurrency = (int)currency,
            };
            await db.InsertAsync(product);
            products.Add(product);
        }
    }

    public static async Task SeedAsync(SQLiteAsyncConnection db)
    {
        if (await db.Table<KaratDefinition>().CountAsync() == 0)
            await db.InsertAllAsync(Karats.Select(k => new KaratDefinition { Karat = k.Karat, Milyem = k.Milyem }));

        if (await db.Table<StockCategory>().CountAsync() == 0)
            await db.InsertAllAsync(Categories.Select((c, i) => new StockCategory
            {
                Name = c.Name, ShortName = c.Short, DefaultKarat = c.Karat, SortOrder = i
            }));

        if (await db.Table<StockLocation>().CountAsync() == 0)
            await db.InsertAllAsync(Locations.Select((l, i) => new StockLocation { Name = l, SortOrder = i }));

        if (await db.Table<BulkProduct>().CountAsync() == 0)
            await db.InsertAllAsync(BulkProducts.Select((b, i) => new BulkProduct
            {
                Name = b.Name, Unit = b.Unit, Karat = b.Karat, PieceWeightMg = b.PieceMg, SortOrder = i
            }));

        await EnsureLedgerProductsAsync(db);

        if (await db.Table<LabelPrinterSettings>().CountAsync() == 0)
            await db.InsertAsync(new LabelPrinterSettings());

        if (await db.Table<StockCodeCounter>().CountAsync() == 0)
            await db.InsertAsync(new StockCodeCounter());
    }
}
