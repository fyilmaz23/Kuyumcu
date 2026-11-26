using Kuyumcu.Models;

namespace Kuyumcu.Services;

public enum TransactionPeriod
{
    Today,
    Yesterday,
    ThisWeek,
    ThisMonth,
    Last30Days,
    All,
    Custom,
}

public enum TransactionKind
{
    All,
    Giden,
    Gelen,
    /// <summary>Emanet bırakma ve teslimleri.</summary>
    Deposit,
}

/// <summary>İşlemler listesinin süzgeci (İşlemler sayfası ve ana sayfa bileşeni).</summary>
public sealed class TransactionFilter
{
    public TransactionPeriod Period { get; set; } = TransactionPeriod.All;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public TransactionKind Kind { get; set; } = TransactionKind.All;
    public HashSet<CurrencyType> Currencies { get; set; } = new();
    /// <summary>Müşteri adı ya da açıklamada geçen metin (Türkçe harf duyarsız).</summary>
    public string? Search { get; set; }
    /// <summary>Sadece kasaya işlenenler.</summary>
    public bool OnlyCash { get; set; }

    public bool IsEmpty => Period == TransactionPeriod.All && Kind == TransactionKind.All && Currencies.Count == 0
                           && string.IsNullOrWhiteSpace(Search) && !OnlyCash;

    public TransactionFilter Clone() => new()
    {
        Period = Period, From = From, To = To, Kind = Kind, Currencies = new HashSet<CurrencyType>(Currencies),
        Search = Search, OnlyCash = OnlyCash,
    };

    /// <summary>Dönemin başlangıcı (dahil) ve bitişi (hariç); <see cref="TransactionPeriod.All"/> için null.</summary>
    public (DateTime From, DateTime To)? Range(DateTime now)
    {
        var today = now.Date;
        // Hafta pazartesi başlar
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        return Period switch
        {
            TransactionPeriod.Today => (today, today.AddDays(1)),
            TransactionPeriod.Yesterday => (today.AddDays(-1), today),
            TransactionPeriod.ThisWeek => (weekStart, today.AddDays(1)),
            TransactionPeriod.ThisMonth => (new DateTime(today.Year, today.Month, 1), today.AddDays(1)),
            TransactionPeriod.Last30Days => (today.AddDays(-29), today.AddDays(1)),
            TransactionPeriod.Custom => ((From ?? DateTime.MinValue).Date, To is { } to ? to.Date.AddDays(1) : DateTime.MaxValue),
            _ => null,
        };
    }
}

/// <summary>Cins başına Giden/Gelen toplamı (süzülmüş işlemler için özet satırı).</summary>
public sealed record TransactionTotal(CurrencyType Currency, decimal Giden, int GidenCount, decimal Gelen, int GelenCount)
{
    public decimal Net => Giden - Gelen;
}

/// <summary>İşlemleri bellekte süzer (saf; birim testli). Silinenler ve gizliler listelenmez.</summary>
public static class TransactionQuery
{
    public static List<Transaction> Apply(IEnumerable<Transaction> transactions, TransactionFilter filter,
        IReadOnlyDictionary<int, string> customerNames, DateTime now)
    {
        var query = transactions.Where(t => !t.IsDeleted && !t.IsHidden && customerNames.ContainsKey(t.CustomerId));

        if (filter.Range(now) is { } range)
            query = query.Where(t => t.Date >= range.From && t.Date < range.To);

        query = filter.Kind switch
        {
            TransactionKind.Giden => query.Where(t => !t.IsDeposit && t.Type == TransactionType.CustomerDebt),
            TransactionKind.Gelen => query.Where(t => !t.IsDeposit && t.Type == TransactionType.StoreDebt),
            TransactionKind.Deposit => query.Where(t => t.IsDeposit),
            _ => query,
        };

        if (filter.Currencies.Count > 0)
            query = query.Where(t => filter.Currencies.Contains(t.CurrencyType));

        if (filter.OnlyCash)
            query = query.Where(t => t.AffectsCash && !t.IsDeposit);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = CustomerDirectory.Normalize(filter.Search);
            query = query.Where(t =>
                CustomerDirectory.Normalize(customerNames.GetValueOrDefault(t.CustomerId)).Contains(term, StringComparison.Ordinal) ||
                CustomerDirectory.Normalize(t.Description).Contains(term, StringComparison.Ordinal));
        }

        return query.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id).ToList();
    }

    /// <summary>Cins başına toplamlar (emanetler hariç).</summary>
    public static List<TransactionTotal> Totals(IEnumerable<Transaction> transactions) =>
        transactions.Where(t => !t.IsDeposit)
            .GroupBy(t => t.CurrencyType)
            .OrderBy(g => g.Key)
            .Select(g => new TransactionTotal(g.Key,
                g.Where(t => t.Type == TransactionType.CustomerDebt).Sum(t => t.Amount),
                g.Count(t => t.Type == TransactionType.CustomerDebt),
                g.Where(t => t.Type == TransactionType.StoreDebt).Sum(t => t.Amount),
                g.Count(t => t.Type == TransactionType.StoreDebt)))
            .ToList();
}
