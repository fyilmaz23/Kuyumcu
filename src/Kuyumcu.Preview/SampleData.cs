using Kuyumcu.Models;
using Kuyumcu.Services;

namespace Kuyumcu.Preview;

/// <summary>Önizleme ilk açıldığında gerçekçi örnek müşteri, işlem ve vitrin stoğu oluşturur.</summary>
public static class SampleData
{
    public static async Task SeedAsync(DatabaseService db, LicenseService license, StockService stock, string labelPrinterName)
    {
        await db.UpdateDatabaseSchemaAsync();
        await SeedCustomersAsync(db, license);
        await SeedStockAsync(db, stock, labelPrinterName);
    }

    private static async Task SeedCustomersAsync(DatabaseService db, LicenseService license)
    {
        if ((await db.GetCustomersAsync()).Count > 0)
            return;

        // Önce deneme süresini başlat: kurulumu tamamlanmış bir veritabanı "eski kullanıcı" sayılıp
        // deneme süresi bitmiş olarak başlatılır (güncelleme yapan müşteriler için kasıtlı davranış).
        await license.InitializeTrialAsync();
        await db.SaveSettingsAsync(new AppSettings { BusinessName = "Örnek Kuyumculuk", IsSetupCompleted = true });

        var random = new Random(42);
        string[] names =
        {
            "Ahmet Yılmaz", "Ayşe Demir", "Mehmet Kaya", "Fatma Şahin", "Mustafa Çelik", "Emine Arslan",
            "Hüseyin Doğan", "Zeynep Koç", "İbrahim Kurt", "Elif Özdemir", "Hasan Aydın", "Hatice Öztürk"
        };
        var currencies = new[]
        {
            (CurrencyType.TurkishLira, 500m, 60000m), (CurrencyType.Dollar, 50m, 3000m), (CurrencyType.Euro, 50m, 2000m),
            (CurrencyType.Gold22K, 2m, 60m), (CurrencyType.Gold24K, 1m, 40m), (CurrencyType.QuarterGold, 1m, 10m),
            (CurrencyType.HalfGold, 1m, 4m), (CurrencyType.FullGold, 1m, 3m)
        };

        foreach (var name in names)
        {
            var phone = "5" + random.Next(30, 56) + random.Next(1000000, 9999999);
            var customerId = await db.SaveCustomerAsync(new Customer { Name = name, PhoneNumber = phone });
            var customer = (await db.GetCustomersAsync()).First(c => c.Name == name);

            var count = random.Next(3, 12);
            for (var i = 0; i < count; i++)
            {
                var (currency, min, max) = currencies[random.Next(currencies.Length)];
                var amount = Math.Round(min + (decimal)random.NextDouble() * (max - min), currency is CurrencyType.Gold22K or CurrencyType.Gold24K ? 2 : 0);
                var type = random.NextDouble() < 0.55 ? TransactionType.CustomerDebt : TransactionType.StoreDebt;
                await db.SaveTransactionAsync(new Transaction
                {
                    CustomerId = customer.Id,
                    Amount = amount,
                    CurrencyType = currency,
                    Type = type,
                    Date = DateTime.Now.AddDays(-random.Next(0, 170)).AddMinutes(-random.Next(0, 600)),
                    Description = type == TransactionType.CustomerDebt ? "Veresiye satış" : "Ödeme",
                    IsDeposit = type == TransactionType.StoreDebt && random.NextDouble() < 0.15
                });
            }
        }
    }

    /// <summary>Örnek vitrin stoğu: kategorilere dağılmış etiketli ürünler, birkaç satış, bir kayıp ve kasa miktarları.</summary>
    private static async Task SeedStockAsync(DatabaseService db, StockService stock, string labelPrinterName)
    {
        if ((await stock.GetItemsAsync(new StockItemQuery { Status = null }, includeDeleted: true)).Count > 0)
            return;

        var settings = await stock.GetLabelSettingsAsync();
        settings.PrinterName = labelPrinterName;
        await stock.SaveLabelSettingsAsync(settings);

        var categories = await stock.GetCategoriesAsync();
        var locations = await stock.GetLocationsAsync();
        int Cat(string name) => categories.First(c => c.Name == name).Id;

        // (kategori, ayar, model, gram aralığı, adet)
        var plan = new (string Category, int Karat, string? Model, int MinMg, int MaxMg, int Count)[]
        {
            ("Bilezik", 22, "Burgu", 9_500, 21_000, 9),
            ("Bilezik", 22, "Hasır", 14_000, 32_000, 5),
            ("Bilezik", 14, "Taşlı", 6_000, 12_000, 3),
            ("Kolye", 14, null, 2_200, 8_500, 8),
            ("Yüzük", 14, "Tektaş", 1_400, 3_800, 6),
            ("Yüzük", 22, "Alyans", 3_000, 6_500, 5),
            ("Künye", 22, null, 7_000, 15_000, 3),
            ("Zincir", 14, "Forse", 3_500, 11_000, 5),
            ("Küpe", 14, null, 1_000, 3_200, 4),
        };

        var random = new Random(7);
        var conn = new SQLite.SQLiteAsyncConnection(db.DatabasePath);
        var created = new List<StockItem>();
        foreach (var p in plan)
        {
            for (var i = 0; i < p.Count; i++)
            {
                var mg = random.Next(p.MinMg, p.MaxMg) / 10 * 10;
                var stone = p.Model is "Taşlı" or "Tektaş" ? random.Next(80, 450) : 0;
                var item = await stock.CreateItemAsync(new StockItemDraft
                {
                    CategoryId = Cat(p.Category),
                    Karat = p.Karat,
                    ModelName = p.Model,
                    WeightMg = mg,
                    StoneWeightMg = stone,
                    LocationId = locations[random.Next(locations.Count)].Id,
                    SupplierName = random.NextDouble() < 0.4 ? "Kapalıçarşı Toptan" : null
                });
                var date = DateTime.Now.AddDays(-random.Next(1, 320)).AddMinutes(-random.Next(0, 600));
                await conn.ExecuteAsync("UPDATE StockItem SET CreatedAt = ?, LabelPrintCount = 1, LastLabelPrintedAt = ? WHERE Id = ?", date, date, item.Id);
                await conn.ExecuteAsync("UPDATE StockMovement SET Date = ? WHERE ItemId = ?", date, item.Id);
                created.Add(item);
            }
        }

        // Son günlerde birkaç satış
        foreach (var item in created.Where((_, i) => i % 7 == 3).Take(6))
        {
            await stock.MarkSoldAsync(item.Id, Math.Round(item.WeightMg / 1000m * 5_900m / 10) * 10, null, null);
            var soldAt = DateTime.Now.AddMinutes(-random.Next(20, 60 * 50));
            await conn.ExecuteAsync("UPDATE StockItem SET SoldAt = ? WHERE Id = ?", soldAt, item.Id);
            await conn.ExecuteAsync("UPDATE StockMovement SET Date = ? WHERE ItemId = ? AND Type = ?", soldAt, item.Id, (int)StockMovementType.Sale);
        }
        await stock.MarkLostAsync(created[^2].Id, null, "Örnek: sayımda bulunamadı");
        await conn.CloseAsync();

        var bulk = await stock.GetBulkProductsAsync();
        foreach (var (name, amount) in new[] { ("Çeyrek", 24L), ("Yarım", 8L), ("Tam", 5L), ("Ata Lira", 3L), ("24 Ayar Gram Altın", 57_500L) })
        {
            var product = bulk.FirstOrDefault(b => b.Name == name);
            if (product != null)
                await stock.AddBulkMovementAsync(product.Id, StockMovementType.BulkIn, amount, "Açılış miktarı");
        }
    }
}
