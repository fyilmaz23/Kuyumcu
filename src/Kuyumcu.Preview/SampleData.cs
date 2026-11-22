using Kuyumcu.Models;
using Kuyumcu.Services;

namespace Kuyumcu.Preview;

/// <summary>Önizleme ilk açıldığında gerçekçi örnek müşteri ve işlemler oluşturur.</summary>
public static class SampleData
{
    public static async Task SeedAsync(DatabaseService db, LicenseService license)
    {
        await db.UpdateDatabaseSchemaAsync();
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
}
