using Kuyumcu.Models;
using Kuyumcu.Stock;
using SQLite;

namespace Kuyumcu.Services;

/// <summary>
/// Bir yedek dosyasının (.db3) belleğe alınmış, salt okunur görüntüsü. Android uygulaması bununla çalışır:
/// dosya SADECE okunur, hiçbir zaman yazılmaz.
/// </summary>
public sealed class BackupSnapshot
{
    private readonly ILookup<int, Transaction> _byCustomer;
    private readonly Dictionary<int, Customer> _customersById;
    private readonly Dictionary<int, List<(CurrencyType Currency, decimal Net)>> _netBalances;

    private BackupSnapshot(List<Customer> customers, List<Transaction> transactions, AppSettings settings, StockSnapshot stock)
    {
        Customers = customers;
        Transactions = transactions;
        Settings = settings;
        Stock = stock;

        _customersById = customers.ToDictionary(c => c.Id);
        _byCustomer = transactions.ToLookup(t => t.CustomerId);
        _netBalances = customers.ToDictionary(c => c.Id, c => CustomerLedger.NetBalances(_byCustomer[c.Id]));
    }

    /// <summary>Silinmemiş müşteriler, ada göre sıralı.</summary>
    public IReadOnlyList<Customer> Customers { get; }

    /// <summary>Silinmemiş müşterilerin silinmemiş işlemleri (emanet ve gizliler dahil), yeniden eskiye.</summary>
    public IReadOnlyList<Transaction> Transactions { get; }

    /// <summary>İşletme ayarları (işletme adı, altın fiyatı sitesi bilgileri, fiyat görseli şablonu).</summary>
    public AppSettings Settings { get; }

    /// <summary>Vitrin stoğu ve kasa ürünleri (stok tabloları olmayan eski yedekte boş).</summary>
    public StockSnapshot Stock { get; }

    public Customer? GetCustomer(int id) => _customersById.GetValueOrDefault(id);

    public IEnumerable<Transaction> GetTransactions(int customerId) => _byCustomer[customerId];

    /// <summary>Müşterinin sıfır olmayan net bakiyeleri (emanet hariç, gizli dahil; bkz. <see cref="CustomerLedger.NetBalances"/>).</summary>
    public IReadOnlyList<(CurrencyType Currency, decimal Net)> GetNetBalances(int customerId) =>
        _netBalances.TryGetValue(customerId, out var list) ? list : Array.Empty<(CurrencyType, decimal)>();

    /// <summary>
    /// Genel özet: para birimi başına, borçlu müşterilerin toplamı (Giden) ve alacaklı müşterilerin toplamı (Gelen).
    /// Masaüstü Ana Sayfa'daki toplamlarla aynı hesap.
    /// </summary>
    public List<CurrencyTotals> GetTotals() =>
        _netBalances.Values.SelectMany(x => x)
            .GroupBy(x => x.Currency)
            .OrderBy(g => g.Key)
            .Select(g => new CurrencyTotals(
                g.Key,
                Giden: g.Where(x => x.Net > 0).Sum(x => x.Net),
                Gelen: g.Where(x => x.Net < 0).Sum(x => -x.Net),
                GidenCustomers: g.Count(x => x.Net > 0),
                GelenCustomers: g.Count(x => x.Net < 0)))
            .ToList();

    /// <summary>
    /// Yedek dosyasını salt okunur açıp belleğe alır. Dosya bu uygulamanın yedeği değilse
    /// <see cref="InvalidDataException"/> fırlatır.
    /// </summary>
    public static BackupSnapshot Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Yedek dosyası bulunamadı.", path);

        SQLiteConnection db;
        try
        {
            db = new SQLiteConnection(new SQLiteConnectionString(path, SQLiteOpenFlags.ReadOnly, storeDateTimeAsTicks: true));
        }
        catch (SQLiteException ex)
        {
            throw new InvalidDataException("Dosya bir Kuyumcu Defteri yedeği değil ya da bozuk.", ex);
        }

        using (db)
        {
            try
            {
                var tables = db.QueryScalars<string>("SELECT name FROM sqlite_master WHERE type='table'")
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (!tables.Contains("Customer") || !tables.Contains("Transaction"))
                    throw new InvalidDataException("Dosyada müşteri tabloları yok; bu bir Kuyumcu Defteri yedeği değil.");

                var customers = db.Table<Customer>().Where(c => !c.IsDeleted).ToList()
                    .OrderBy(c => c.Name ?? string.Empty, StringComparer.Create(TurkishCulture, ignoreCase: true))
                    .ToList();
                var customerIds = customers.Select(c => c.Id).ToHashSet();

                var transactions = db.Table<Transaction>().Where(t => !t.IsDeleted).ToList()
                    .Where(t => customerIds.Contains(t.CustomerId))
                    .OrderByDescending(t => t.Date).ThenByDescending(t => t.Id)
                    .ToList();

                var settings = tables.Contains("AppSettings")
                    ? db.Query<AppSettings>("SELECT * FROM AppSettings LIMIT 1").FirstOrDefault() ?? new AppSettings()
                    : new AppSettings();

                return new BackupSnapshot(customers, transactions, settings, StockSnapshot.Load(db, tables));
            }
            catch (SQLiteException ex)
            {
                throw new InvalidDataException("Yedek dosyası okunamadı (bozuk olabilir).", ex);
            }
        }
    }

    private static readonly System.Globalization.CultureInfo TurkishCulture = new("tr-TR");
}

/// <summary>Bir para biriminde toplam Giden / Gelen ve kaç müşteriden oluştuğu.</summary>
public sealed record CurrencyTotals(CurrencyType Currency, decimal Giden, decimal Gelen, int GidenCustomers, int GelenCustomers);
