using Kuyumcu.Models;
using Kuyumcu.Platform;
using Kuyumcu.Services;
using Kuyumcu.Stock;
using SQLite;

namespace Kuyumcu.Tests;

/// <summary>Android'in yedekten vitrin stoğunu okuması (<see cref="BackupSnapshot.Stock"/>).</summary>
[Collection("Veritabanı")]
public sealed class BackupSnapshotStockTests : IDisposable
{
    private readonly string _folder =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "kuyumcu-test-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose()
    {
        SQLiteAsyncConnection.ResetPool();
        try { Directory.Delete(_folder, true); } catch { /* SQLite dosyası kilitli kalabilir */ }
    }

    [Fact]
    public async Task Uygulamanin_veritabanindaki_stok_masaustuyle_ayni_toplanir()
    {
        var env = new FolderEnvironment(_folder);
        var db = new DatabaseService(env);
        await db.UpdateDatabaseSchemaAsync();
        var stock = new StockService(db);
        var bilezik = (await stock.GetCategoriesAsync()).First(c => c.Name == "Bilezik").Id;

        var a = await stock.CreateItemAsync(new StockItemDraft { CategoryId = bilezik, Karat = 22, WeightMg = 10_250, ModelName = "Burgu" });
        var b = await stock.CreateItemAsync(new StockItemDraft { CategoryId = bilezik, Karat = 14, WeightMg = 4_000, StoneWeightMg = 500 });
        var c = await stock.CreateItemAsync(new StockItemDraft { CategoryId = bilezik, Karat = 22, WeightMg = 7_000 });
        var d = await stock.CreateItemAsync(new StockItemDraft { CategoryId = bilezik, Karat = 22, WeightMg = 3_000 });
        await stock.MarkSoldAsync(c.Id, 25_000m, null, null);
        await stock.DeleteItemAsync(d.Id, "Yanlış giriş");
        var ceyrek = (await stock.GetBulkProductsAsync()).First(p => p.Name == "Çeyrek");
        await stock.AddBulkMovementAsync(ceyrek.Id, StockMovementType.BulkIn, 12);

        var expected = await stock.GetTotalsAsync();
        var snapshot = BackupSnapshot.Load(db.DatabasePath).Stock;

        Assert.True(snapshot.HasAny);
        Assert.Equal(3, snapshot.Items.Count); // silinen ürün yok
        Assert.DoesNotContain(snapshot.Items, i => i.Id == d.Id);
        Assert.Equal(expected.Count, snapshot.InStockTotals.Count);
        Assert.Equal(expected.WeightMg, snapshot.InStockTotals.WeightMg);
        Assert.Equal(expected.HasMg, snapshot.InStockTotals.HasMg);

        Assert.Equal("Bilezik · Burgu", snapshot.Title(snapshot.GetItem(a.Id)!));
        Assert.Equal(StockCalculator.HasMg(3_500, 585), snapshot.HasMg(snapshot.GetItem(b.Id)!));
        Assert.Equal(StockItemStatus.Sold, snapshot.GetItem(c.Id)!.Status);
        Assert.Equal(new[] { StockMovementType.Sale, StockMovementType.Entry }, snapshot.GetMovements(c.Id).Select(m => m.Type));

        var bulk = snapshot.BulkTotals.Single(t => t.Product.Name == "Çeyrek");
        Assert.Equal(12, bulk.Product.Amount);
        Assert.Equal(12 * 1754, bulk.WeightMg);
    }

    [Fact]
    public async Task Stok_kullanilmamissa_gosterilecek_stok_yok()
    {
        // Kurulumda kasa ürünleri miktarı 0 olarak eklenir; bunlar "stok var" sayılmaz
        var db = new DatabaseService(new FolderEnvironment(_folder));
        await db.UpdateDatabaseSchemaAsync();

        var snapshot = BackupSnapshot.Load(db.DatabasePath).Stock;

        Assert.False(snapshot.HasAny);
        Assert.NotEmpty(snapshot.BulkProducts);
    }

    [Fact]
    public void Stok_tablosu_olmayan_eski_yedek_okunur()
    {
        var path = Path.Combine(_folder, "eski.db3");
        using (var db = new SQLiteConnection(path))
        {
            db.CreateTable<Customer>();
            db.CreateTable<Transaction>();
            db.Insert(new Customer { Name = "Ali" });
        }

        var snapshot = BackupSnapshot.Load(path);

        Assert.Single(snapshot.Customers);
        Assert.False(snapshot.Stock.HasAny);
        Assert.Empty(snapshot.Stock.Items);
    }

    [Fact]
    public void Ornek_verilerde_vitrin_ve_kasa_vardir()
    {
        var path = Path.Combine(_folder, "ornek.db3");
        DemoBackup.Create(path);

        var stock = BackupSnapshot.Load(path).Stock;

        Assert.True(stock.HasAny);
        Assert.Equal(36, stock.Items.Count);
        Assert.Equal(6, stock.Items.Count(i => i.Status == StockItemStatus.Sold));
        Assert.Equal(30, stock.InStockTotals.Count);
        Assert.All(stock.Items, i => Assert.True(ProductCode.IsValid(i.Code)));
        Assert.All(stock.Items.Where(i => i.SoldAt != null), i => Assert.True(i.SoldAt <= DateTime.Now));
        Assert.Contains(stock.BulkProducts, p => p.Amount > 0);
    }

    private sealed class FolderEnvironment(string folder) : IAppEnvironment
    {
        public string AppDataDirectory => folder;
        public string BackupDirectory => Path.Combine(folder, "backups");
        public string CacheDirectory => folder;
        public string PicturesDirectory => folder;
        public bool IsMobile => false;
        public string GetDeviceFingerprint() => "test";
    }
}
