using Kuyumcu.Models;
using Kuyumcu.Stock;
using SQLite;

namespace Kuyumcu.Services;

/// <summary>
/// Örnek verili bir yedek dosyası üretir. Android'de "Örnek verilerle dene" için kullanılır: uygulamayı
/// bilgisayar kurulumu olmadan tanıtmaya ve Google Play incelemesine (uygulama erişimi) yarar.
/// Gerçek müşteri verisi içermez.
/// </summary>
public static class DemoBackup
{
    public const string BusinessName = "Örnek Kuyumculuk";

    public static void Create(string path)
    {
        if (File.Exists(path))
            File.Delete(path);

        using var db = new SQLiteConnection(path);
        db.CreateTable<Customer>();
        db.CreateTable<Transaction>();
        db.CreateTable<AppSettings>();
        db.Insert(new AppSettings { BusinessName = BusinessName, IsSetupCompleted = true });

        var random = new Random(42);
        string[] names =
        {
            "Ahmet Yılmaz", "Ayşe Demir", "Mehmet Kaya", "Fatma Şahin", "Mustafa Çelik", "Emine Arslan",
            "Hüseyin Doğan", "Zeynep Koç", "İbrahim Kurt", "Elif Özdemir", "Hasan Aydın", "Hatice Öztürk",
        };
        var currencies = new[]
        {
            (CurrencyType.TurkishLira, 500m, 60000m), (CurrencyType.Dollar, 50m, 3000m), (CurrencyType.Euro, 50m, 2000m),
            (CurrencyType.Gold22K, 2m, 60m), (CurrencyType.Gold24K, 1m, 40m), (CurrencyType.QuarterGold, 1m, 10m),
            (CurrencyType.HalfGold, 1m, 4m), (CurrencyType.FullGold, 1m, 3m),
        };

        db.RunInTransaction(() =>
        {
            foreach (var name in names)
            {
                var customer = new Customer { Name = name, PhoneNumber = "5" + random.Next(30, 56) + random.Next(1000000, 9999999) };
                db.Insert(customer);

                var count = random.Next(3, 14);
                for (var i = 0; i < count; i++)
                {
                    var (currency, min, max) = currencies[random.Next(currencies.Length)];
                    var amount = Math.Round(min + (decimal)random.NextDouble() * (max - min),
                        currency is CurrencyType.Gold22K or CurrencyType.Gold24K ? 2 : 0);
                    var type = random.NextDouble() < 0.55 ? TransactionType.CustomerDebt : TransactionType.StoreDebt;
                    db.Insert(new Transaction
                    {
                        CustomerId = customer.Id,
                        Amount = amount,
                        CurrencyType = currency,
                        Type = type,
                        Date = DateTime.Now.AddDays(-random.Next(0, 170)).AddMinutes(-random.Next(0, 600)),
                        Description = type == TransactionType.CustomerDebt ? "Veresiye satış" : "Ödeme",
                        IsDeposit = type == TransactionType.StoreDebt && random.NextDouble() < 0.15,
                    });
                }
            }
        });

        CreateStock(db, random);
    }

    /// <summary>Örnek vitrin stoğu (etiketli ürünler, birkaç satış) ve kasa miktarları.</summary>
    private static void CreateStock(SQLiteConnection db, Random random)
    {
        db.CreateTable<StockItem>();
        db.CreateTable<StockMovement>();
        db.CreateTable<StockCategory>();
        db.CreateTable<StockLocation>();
        db.CreateTable<KaratDefinition>();
        db.CreateTable<BulkProduct>();

        long[] bulkAmounts = { 24, 9, 4, 2, 35_500 };
        string[] models = { "Burgu", "Hasır", "Ajda", "Kelebek", "Tektaş", "Halka", "Yaprak", "Zincir" };

        db.RunInTransaction(() =>
        {
            db.InsertAll(StockDefaults.Karats.Select(k => new KaratDefinition { Karat = k.Karat, Milyem = k.Milyem }));
            db.InsertAll(StockDefaults.Categories.Select((c, i) => new StockCategory
            {
                Name = c.Name, ShortName = c.Short, DefaultKarat = c.Karat, SortOrder = i
            }));
            db.InsertAll(StockDefaults.Locations.Select((l, i) => new StockLocation { Name = l, SortOrder = i }));
            db.InsertAll(StockDefaults.BulkProducts.Select((b, i) => new BulkProduct
            {
                Name = b.Name, Unit = b.Unit, Karat = b.Karat, PieceWeightMg = b.PieceMg, SortOrder = i,
                Amount = bulkAmounts[i]
            }));

            var categoryCount = StockDefaults.Categories.Length;
            for (var n = 1; n <= 36; n++)
            {
                var category = random.Next(categoryCount);
                var karat = StockDefaults.Categories[category].Karat;
                var weightMg = karat == 22 ? random.Next(5_000, 32_000) : random.Next(1_200, 9_000);
                var created = DateTime.Now.AddDays(-random.Next(1, 200)).AddMinutes(-random.Next(0, 600));
                var sold = n % 6 == 0;

                var item = new StockItem
                {
                    Code = ProductCode.Format(n),
                    CategoryId = category + 1,
                    Karat = karat,
                    ModelName = models[random.Next(models.Length)],
                    WeightMg = weightMg / 10 * 10,
                    StoneWeightMg = karat == 14 && random.NextDouble() < 0.3 ? random.Next(100, 600) / 10 * 10 : 0,
                    LocationId = random.Next(1, StockDefaults.Locations.Length + 1),
                    Status = sold ? StockItemStatus.Sold : StockItemStatus.InStock,
                    CreatedAt = created,
                    SoldAt = sold ? Min(created.AddDays(random.Next(1, 30)), DateTime.Now) : null,
                };
                db.Insert(item);

                db.Insert(new StockMovement
                {
                    ItemId = item.Id, Code = item.Code, Type = StockMovementType.Entry, Date = item.CreatedAt,
                    Description = "Ürün girişi", WeightMg = item.WeightMg
                });
                if (sold)
                    db.Insert(new StockMovement
                    {
                        ItemId = item.Id, Code = item.Code, Type = StockMovementType.Sale, Date = item.SoldAt!.Value,
                        Description = "Satış", WeightMg = item.WeightMg
                    });
            }
        });

        static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
    }
}
