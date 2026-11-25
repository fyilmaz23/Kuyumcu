using Kuyumcu.Models;
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
    }
}
