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
    public async Task Kasa_veresiye_satisi_musteri_ve_islemi_harekette_saklar()
    {
        var ceyrek = (await _stock.GetBulkProductsAsync()).First(b => b.Name == "Çeyrek");
        await _stock.AddBulkMovementAsync(ceyrek.Id, StockMovementType.BulkIn, 5);
        await _stock.AddBulkMovementAsync(ceyrek.Id, StockMovementType.BulkOut, 2, "Veresiye satış: Ali", customerId: 7, transactionId: 42, price: 21_000m);

        var sale = (await _stock.GetBulkMovementsAsync(ceyrek.Id)).First();
        Assert.Equal(-2, sale.AmountDelta);
        Assert.Equal(7, sale.CustomerId);
        Assert.Equal(42, sale.TransactionId);
        Assert.Equal(21_000m, sale.Price);
        Assert.Equal("Veresiye satış: Ali", sale.Description);

        // Müşteri bağlantısı sadece çıkışta
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _stock.AddBulkMovementAsync(ceyrek.Id, StockMovementType.BulkIn, 1, customerId: 7));
    }

    [Fact]
    public async Task Kasa_veresiye_satisi_iade_alinir_bir_kez()
    {
        var gram = (await _stock.GetBulkProductsAsync()).First(b => b.Unit == BulkUnit.Gram);
        await _stock.AddBulkMovementAsync(gram.Id, StockMovementType.BulkIn, 20_000);
        await _stock.AddBulkMovementAsync(gram.Id, StockMovementType.BulkOut, 2_500, "Veresiye satış: Ali", customerId: 7, transactionId: 42);
        var sale = (await _stock.GetBulkMovementsAsync(gram.Id)).First();

        var after = await _stock.ReturnBulkSaleAsync(sale.Id, "bozuk");
        Assert.Equal(20_000, after.Amount);

        var ret = (await _stock.GetBulkMovementsAsync(gram.Id)).First();
        Assert.Equal(StockMovementType.BulkReturn, ret.Type);
        Assert.Equal(2_500, ret.AmountDelta);
        Assert.Equal(sale.Id, ret.RelatedMovementId);
        Assert.Equal(7, ret.CustomerId);
        Assert.Equal(42, ret.TransactionId);
        Assert.Equal("İade - bozuk", ret.Description);

        // İkinci kez iade alınamaz
        await Assert.ThrowsAsync<StockException>(() => _stock.ReturnBulkSaleAsync(sale.Id));
        Assert.Equal(20_000, (await _stock.GetBulkProductsAsync()).First(b => b.Id == gram.Id).Amount);
    }

    [Fact]
    public async Task Musteriye_bagli_olmayan_cikis_iade_alinamaz()
    {
        var ceyrek = (await _stock.GetBulkProductsAsync()).First(b => b.Name == "Çeyrek");
        await _stock.AddBulkMovementAsync(ceyrek.Id, StockMovementType.BulkIn, 5);
        await _stock.AddBulkMovementAsync(ceyrek.Id, StockMovementType.BulkOut, 1);
        var plainOut = (await _stock.GetBulkMovementsAsync(ceyrek.Id)).First();

        await Assert.ThrowsAsync<StockException>(() => _stock.ReturnBulkSaleAsync(plainOut.Id));
        await Assert.ThrowsAsync<StockException>(() => _stock.ReturnBulkSaleAsync(999_999));
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

    [Fact]
    public async Task Barkod_modulu_yoksa_yeni_urun_satis_kasa_ve_sayim_yapilamaz()
    {
        var license = new LicenseService(_db, new MemorySecureStore(), _env);
        await license.InitializeTrialAsync(); // deneme sürümü: barkod modülü kapalı
        var gated = new StockService(_db, license);
        var gatedCount = new StockCountService(_db, gated);

        var existing = await NewItem(); // denetimsiz servisle önceden girilmiş ürün
        var ceyrek = (await _stock.GetBulkProductsAsync()).First(b => b.Name == "Çeyrek");

        await Assert.ThrowsAsync<StockException>(() =>
            gated.CreateItemAsync(new StockItemDraft { CategoryId = _bilezik, Karat = 22, WeightMg = 5_000 }));
        await Assert.ThrowsAsync<StockException>(() => gated.MarkSoldAsync(existing.Id, null, null, null));
        await Assert.ThrowsAsync<StockException>(() => gated.AddBulkMovementAsync(ceyrek.Id, StockMovementType.BulkIn, 1));
        await Assert.ThrowsAsync<StockException>(() => gatedCount.StartAsync(0));

        // Okuma serbest; kayıtlar değişmedi
        Assert.Single(await gated.GetItemsAsync());
        Assert.Equal(StockItemStatus.InStock, (await gated.GetItemAsync(existing.Id))!.Status);
    }

    // ───────── Defter → Kasa (LedgerCash) ─────────

    private async Task<BulkProduct> Linked(CurrencyType c) =>
        (await _stock.GetBulkProductsAsync(true)).Single(p => p.LedgerCurrency == (int)c);

    private async Task<int> NewCustomer()
    {
        var c = new Customer { Name = "Kasa Testi " + Guid.NewGuid().ToString("N")[..6] };
        await _db.SaveCustomerAsync(c);
        return c.Id;
    }

    private static Transaction Tx(int customerId, TransactionType type, decimal amount, CurrencyType currency, bool cash = true, bool deposit = false) =>
        new() { CustomerId = customerId, Type = type, Amount = amount, CurrencyType = currency, Date = DateTime.Now, AffectsCash = cash, IsDeposit = deposit };

    [Fact]
    public async Task Her_defter_cinsine_tek_kasa_urunu_baglanir()
    {
        var products = await _stock.GetBulkProductsAsync(true);
        foreach (var c in Enum.GetValues<CurrencyType>())
            Assert.Single(products, p => p.LedgerCurrency == (int)c);

        // Kurulumdaki ürünler bağlanır, kopyası açılmaz
        Assert.Equal("Çeyrek", (await Linked(CurrencyType.QuarterGold)).Name);
        Assert.Equal("24 Ayar Gram Altın", (await Linked(CurrencyType.Gold24K)).Name);
        Assert.Single(products, p => p.Name == "Çeyrek");
        Assert.Null(products.Single(p => p.Name == "Ata Lira").LedgerCurrency);
        Assert.Equal(BulkUnit.Money, (await Linked(CurrencyType.Euro)).Unit);

        // İkinci çalıştırma bir şey değiştirmez
        await _db.UpdateDatabaseSchemaAsync();
        Assert.Equal(products.Count, (await _stock.GetBulkProductsAsync(true)).Count);
    }

    [Fact]
    public async Task Gelen_kasaya_girer_giden_cikar_emanet_ve_kapali_islenmez()
    {
        var customer = await NewCustomer();
        await _db.SaveTransactionAsync(Tx(customer, TransactionType.StoreDebt, 2, CurrencyType.QuarterGold));
        await _db.SaveTransactionAsync(Tx(customer, TransactionType.CustomerDebt, 150.5m, CurrencyType.Euro));
        await _db.SaveTransactionAsync(Tx(customer, TransactionType.StoreDebt, 12.345m, CurrencyType.Gold22K));
        await _db.SaveTransactionAsync(Tx(customer, TransactionType.StoreDebt, 5, CurrencyType.QuarterGold, deposit: true));
        await _db.SaveTransactionAsync(Tx(customer, TransactionType.StoreDebt, 7, CurrencyType.QuarterGold, cash: false));

        Assert.Equal(2, (await Linked(CurrencyType.QuarterGold)).Amount);
        var euro = await Linked(CurrencyType.Euro);
        Assert.Equal(-15_050, euro.Amount); // kuruş; kasa eksiye düşebilir
        Assert.Equal("-150,50 €", StockService.FormatBulkAmount(euro, euro.Amount));
        Assert.Equal(12_345, (await Linked(CurrencyType.Gold22K)).Amount);

        var history = await _stock.GetBulkMovementsAsync(euro.Id);
        var m = Assert.Single(history);
        Assert.True(m.FromLedger);
        Assert.Equal(StockMovementType.BulkOut, m.Type);
        Assert.Equal(customer, m.CustomerId);
        await Assert.ThrowsAsync<StockException>(() => _stock.ReturnBulkSaleAsync(m.Id));
    }

    [Fact]
    public async Task Duzenleme_ve_silme_kasada_duzeltme_hareketi_yazar()
    {
        var customer = await NewCustomer();
        var t = Tx(customer, TransactionType.StoreDebt, 2, CurrencyType.QuarterGold);
        await _db.SaveTransactionAsync(t);

        t.Amount = 3;
        await _db.SaveTransactionAsync(t);
        Assert.Equal(3, (await Linked(CurrencyType.QuarterGold)).Amount);

        t.CurrencyType = CurrencyType.HalfGold; // cins değişti: çeyrekten çıkar, yarıma girer
        await _db.SaveTransactionAsync(t);
        Assert.Equal(0, (await Linked(CurrencyType.QuarterGold)).Amount);
        Assert.Equal(3, (await Linked(CurrencyType.HalfGold)).Amount);

        t.Description = "sadece açıklama"; // kasaya etkisi değişmeyen düzenleme hareket yazmaz
        await _db.SaveTransactionAsync(t);
        var halfId = (await Linked(CurrencyType.HalfGold)).Id;
        Assert.Single(await _stock.GetBulkMovementsAsync(halfId));

        await _db.DeleteTransactionAsync(t);
        Assert.Equal(0, (await Linked(CurrencyType.HalfGold)).Amount);
        Assert.Equal(2, (await _stock.GetBulkMovementsAsync(halfId)).Count);
    }

    [Fact]
    public async Task Sifirlama_kasaya_islenir_ve_silinen_urunun_cinsi_islenmez()
    {
        var customer = await NewCustomer();
        var settle = CustomerLedger.Settlement(customer, CurrencyType.Dollar, 200, false, DateTime.Now, "Hesap sıfırlandı")!;
        settle.AffectsCash = true;
        await _db.AddTransactionsAsync([settle]);
        Assert.Equal(20_000, (await Linked(CurrencyType.Dollar)).Amount); // 200 $ Giden bakiye → 200 $ Gelen

        var riyal = await Linked(CurrencyType.Riyal);
        await _stock.DeleteBulkProductAsync(riyal);
        await _db.SaveTransactionAsync(Tx(customer, TransactionType.StoreDebt, 50, CurrencyType.Riyal));
        Assert.Equal(0, (await Linked(CurrencyType.Riyal)).Amount);
    }

    private sealed class MemorySecureStore : ISecureStore
    {
        private readonly Dictionary<string, string> _values = [];
        public Task<string?> GetAsync(string key) => Task.FromResult(_values.TryGetValue(key, out var v) ? v : null);
        public Task SetAsync(string key, string value) { _values[key] = value; return Task.CompletedTask; }
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
        public bool IsMobile => false;
        public string GetDeviceFingerprint() => "test";

        public void Dispose()
        {
            try { Directory.Delete(AppDataDirectory, true); } catch { /* SQLite dosyası kilitli kalabilir */ }
        }
    }
}
