using Kuyumcu.Models;
using Kuyumcu.Platform;
using Kuyumcu.Services;
using Kuyumcu.Stock;
using SQLite;

namespace Kuyumcu.Tests;

/// <summary>Geçici klasörde gerçek SQLite veritabanıyla stok servisi testleri.</summary>
[Collection("Veritabanı")]
public sealed class StockServiceTests : IAsyncLifetime
{
    private readonly TestEnvironment _env = new();
    private DatabaseService _db = null!;
    private StockService _stock = null!;
    private StockCountService _count = null!;
    private int _bilezik;

    public async Task InitializeAsync()
    {
        _db = new DatabaseService(_env);
        await _db.UpdateDatabaseSchemaAsync();
        _stock = new StockService(_db);
        _count = new StockCountService(_db, _stock);
        _bilezik = (await _stock.GetCategoriesAsync()).First(c => c.Name == "Bilezik").Id;
    }

    public async Task DisposeAsync()
    {
        SQLiteAsyncConnection.ResetPool();
        await Task.Delay(50);
        _env.Dispose();
    }

    private Task<StockItem> NewItem(int mg = 10_000, int karat = 22, int locationId = 0) =>
        _stock.CreateItemAsync(new StockItemDraft { CategoryId = _bilezik, Karat = karat, WeightMg = mg, LocationId = locationId });

    [Fact]
    public async Task Varsayilan_tanimlar_eklenir()
    {
        var karats = await _stock.GetKaratsAsync();
        Assert.Equal(916, karats.Single(k => k.Karat == 22).Milyem);
        Assert.Equal(585, karats.Single(k => k.Karat == 14).Milyem);
        Assert.Contains(await _stock.GetLocationsAsync(), l => l.Name == "Vitrin 1");
        Assert.Contains(await _stock.GetBulkProductsAsync(), b => b.Name == "Çeyrek");
        Assert.Equal(72, (await _stock.GetLabelSettingsAsync()).WidthMm);
    }

    [Fact]
    public async Task Kodlar_sirali_ve_silinen_kod_yeniden_kullanilmaz()
    {
        var a = await NewItem();
        var b = await NewItem();
        Assert.Equal(ProductCode.Format(1), a.Code);
        Assert.Equal(ProductCode.Format(2), b.Code);

        await _stock.DeleteItemAsync(b.Id, "Yanlış giriş");
        var c = await NewItem();
        Assert.Equal(ProductCode.Format(3), c.Code);

        var movements = await _stock.GetItemMovementsAsync(b.Id);
        Assert.Equal(new[] { StockMovementType.Delete, StockMovementType.Entry }, movements.Select(m => m.Type));
        Assert.Equal("Yanlış giriş", movements[0].Description);

        // Silinen ürün okutulunca bulunur ama silinmiş görünür
        Assert.True((await _stock.GetItemByCodeAsync(b.Code))!.IsDeleted);
    }

    [Fact]
    public async Task Gecersiz_gram_ve_tas_reddedilir()
    {
        await Assert.ThrowsAsync<StockException>(() => NewItem(mg: 0));
        await Assert.ThrowsAsync<StockException>(() =>
            _stock.CreateItemAsync(new StockItemDraft { CategoryId = _bilezik, Karat = 22, WeightMg = 1000, StoneWeightMg = 1000 }));
    }

    [Fact]
    public async Task Satis_iade_ve_hareketler()
    {
        var item = await NewItem();
        await _stock.MarkSoldAsync(item.Id, 60_000m, null, null);
        await Assert.ThrowsAsync<StockException>(() => _stock.MarkSoldAsync(item.Id, null, null, null));

        var sold = (await _stock.GetItemAsync(item.Id))!;
        Assert.Equal(StockItemStatus.Sold, sold.Status);
        Assert.Equal(60_000m, sold.SalePrice);

        await _stock.ReturnAsync(item.Id, "Müşteri beğenmedi");
        var back = (await _stock.GetItemAsync(item.Id))!;
        Assert.Equal(StockItemStatus.InStock, back.Status);
        Assert.Null(back.SalePrice);
        Assert.Equal(item.Code, back.Code);

        var types = (await _stock.GetItemMovementsAsync(item.Id)).Select(m => m.Type).ToList();
        Assert.Equal(new[] { StockMovementType.Return, StockMovementType.Sale, StockMovementType.Entry }, types);
    }

    [Fact]
    public async Task Duzeltme_degisen_alanlari_yazar()
    {
        var item = await NewItem(mg: 4_250);
        var edit = (await _stock.GetItemAsync(item.Id))!;
        edit.WeightMg = 4_300;
        edit.Karat = 14;
        await _stock.UpdateItemAsync(edit);

        var m = (await _stock.GetItemMovementsAsync(item.Id)).First();
        Assert.Equal(StockMovementType.Edit, m.Type);
        Assert.Contains("Gram: 4,25 → 4,30", m.Details);
        Assert.Contains("Ayar: 22 → 14", m.Details);

        // Değişiklik yoksa hareket yazılmaz
        await _stock.UpdateItemAsync((await _stock.GetItemAsync(item.Id))!);
        Assert.Equal(2, (await _stock.GetItemMovementsAsync(item.Id)).Count);
    }

    [Fact]
    public async Task Toplamlar_sadece_vitrindekileri_sayar()
    {
        var a = await NewItem(mg: 10_000);
        await NewItem(mg: 5_000, karat: 14);
        var sold = await NewItem(mg: 7_000);
        await _stock.MarkSoldAsync(sold.Id, null, null, null);

        var totals = await _stock.GetTotalsAsync();
        Assert.Equal(2, totals.Count);
        Assert.Equal(15_000, totals.WeightMg);
        Assert.Equal(9_160 + 2_925, totals.HasMg);
    }

    [Fact]
    public async Task Kasa_giris_cikis_ve_duzeltme()
    {
        var ceyrek = (await _stock.GetBulkProductsAsync()).First(b => b.Name == "Çeyrek");
        await _stock.AddBulkMovementAsync(ceyrek.Id, StockMovementType.BulkIn, 10);
        await _stock.AddBulkMovementAsync(ceyrek.Id, StockMovementType.BulkOut, 3);
        await Assert.ThrowsAsync<StockException>(() => _stock.AddBulkMovementAsync(ceyrek.Id, StockMovementType.BulkOut, 8));
        var adjusted = await _stock.AddBulkMovementAsync(ceyrek.Id, StockMovementType.BulkAdjust, 6, "Sayım");

        Assert.Equal(6, adjusted.Amount);
        var deltas = (await _stock.GetBulkMovementsAsync(ceyrek.Id)).Select(m => m.AmountDelta).ToList();
        Assert.Equal(new long[] { -1, -3, 10 }, deltas);
    }

    [Fact]
    public async Task Sayim_eksik_fazla_tekrar_ve_kayip_adimi()
    {
        var a = await NewItem();
        var b = await NewItem();
        var sold = await NewItem();
        await _stock.MarkSoldAsync(sold.Id, null, null, null);

        var session = await _count.StartAsync(0);
        Assert.Equal(2, session.ExpectedCount);
        await Assert.ThrowsAsync<StockException>(() => _count.StartAsync(0)); // ikinci açık sayım olmaz

        Assert.Equal(CountScanKind.Found, (await _count.ScanAsync(session.Id, a.Code)).Kind);
        Assert.Equal(CountScanKind.Duplicate, (await _count.ScanAsync(session.Id, a.Code)).Kind);
        Assert.Equal(CountScanKind.NotInStock, (await _count.ScanAsync(session.Id, sold.Code)).Kind);
        Assert.Equal(CountScanKind.Unknown, (await _count.ScanAsync(session.Id, ProductCode.Format(999))).Kind);
        Assert.Equal(CountScanKind.Invalid, (await _count.ScanAsync(session.Id, "abc")).Kind);
        Assert.Equal(CountScanKind.Invalid, (await _count.ScanAsync(session.Id, ProductCode.TestCode)).Kind); // test etiketi sayılmaz

        var done = await _count.CompleteAsync(session.Id);
        Assert.Equal(1, done.FoundCount);
        Assert.Equal(1, done.MissingCount);
        Assert.Equal(2, done.ExtraCount);

        // Bitirmek ürünü kayıp yapmaz; ayrı adım gerekir
        Assert.Equal(StockItemStatus.InStock, (await _stock.GetItemAsync(b.Id))!.Status);

        var missing = (await _count.GetLinesAsync(session.Id)).Single(l => l.Result == StockCountResult.Missing);
        Assert.Equal(b.Code, missing.Code);
        Assert.Equal(1, await _count.MarkMissingAsLostAsync(session.Id, new[] { missing.Id }));

        var lost = (await _stock.GetItemAsync(b.Id))!;
        Assert.Equal(StockItemStatus.Lost, lost.Status);
        var move = (await _stock.GetItemMovementsAsync(b.Id)).First();
        Assert.Equal(StockMovementType.Lost, move.Type);
        Assert.Equal(session.Id, move.CountSessionId);
    }

    [Fact]
    public async Task Yedek_stok_tablolarini_icerir_ve_geri_yukleme_kodu_geri_sarmaz()
    {
        await NewItem();
        var backup = Path.Combine(_env.AppDataDirectory, "yedek.db3");
        Assert.True(await _db.CreateBackupCopyAsync(backup));

        using (var copy = new SQLiteConnection(backup, SQLiteOpenFlags.ReadOnly))
        {
            Assert.Equal(1, copy.Table<StockItem>().Count());
            Assert.Equal(1, copy.Table<StockMovement>().Count());
            Assert.True(copy.Table<StockCategory>().Count() > 0);
        }

        // Yedekten sonra iki etiket daha basıldı
        await NewItem();
        var last = await NewItem();
        Assert.Equal(ProductCode.Format(3), last.Code);

        var settings = await _stock.GetLabelSettingsAsync();
        settings.PrinterName = "TSC TE210";
        await _stock.SaveLabelSettingsAsync(settings);

        var (ok, message) = await _db.RestoreDatabaseAsync(backup);
        Assert.True(ok, message);

        Assert.Single(await _stock.GetItemsAsync());
        var next = await NewItem();
        Assert.Equal(ProductCode.Format(4), next.Code); // 2 ve 3 bir daha verilmez
        Assert.Equal("TSC TE210", (await _stock.GetLabelSettingsAsync()).PrinterName); // cihaz ayarı korunur
    }

    private sealed class TestEnvironment : IAppEnvironment, IDisposable
    {
        public TestEnvironment()
        {
            AppDataDirectory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "kuyumcu-test-" + Guid.NewGuid().ToString("N"))).FullName;
        }

        public string AppDataDirectory { get; }
        public string BackupDirectory => Path.Combine(AppDataDirectory, "backups");
        public string CacheDirectory => AppDataDirectory;
        public string PicturesDirectory => AppDataDirectory;
        public bool IsReadOnlyClient => false;
        public bool IsMobile => false;
        public string GetDeviceFingerprint() => "test";

        public void Dispose()
        {
            try { Directory.Delete(AppDataDirectory, true); } catch { /* SQLite dosyası kilitli kalabilir */ }
        }
    }
}
