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

        if (await db.Table<LabelPrinterSettings>().CountAsync() == 0)
            await db.InsertAsync(new LabelPrinterSettings());

        if (await db.Table<StockCodeCounter>().CountAsync() == 0)
            await db.InsertAsync(new StockCodeCounter());
    }
}
