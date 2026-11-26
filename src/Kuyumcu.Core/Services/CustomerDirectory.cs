using System.Globalization;
using Kuyumcu.Models;

namespace Kuyumcu.Services;

/// <summary>Müşteri listesinde bakiye yönü süzgeci.</summary>
public enum BalanceFilter
{
    /// <summary>Hepsi.</summary>
    All,
    /// <summary>Giden bakiyesi olanlar (müşteri borçlu).</summary>
    Giden,
    /// <summary>Gelen bakiyesi olanlar (müşteri alacaklı).</summary>
    Gelen,
    /// <summary>Açık bakiyesi olmayanlar (seçili cinslerde ya da hiçbir cinste).</summary>
    Zero,
}

public enum CustomerSort
{
    Name,
    LastTransaction,
    /// <summary>Seçili tek cinsteki bakiye (büyükten küçüğe). Cins seçili değilse açık bakiye sayısı.</summary>
    Balance,
}

/// <summary>Müşteriler sayfasının süzgeç durumu (sayfadan çıkıp dönünce korunur).</summary>
public sealed class CustomerListFilter
{
    public string? Search { get; set; }
    public BalanceFilter Balance { get; set; } = BalanceFilter.All;
    public HashSet<CurrencyType> Currencies { get; set; } = new();
    /// <summary>Dükkânda emaneti olanlar.</summary>
    public bool HasDeposit { get; set; }
    /// <summary>Son işlemi bu kadar günden eski olanlar (hiç işlemi olmayanlar dahil değil).</summary>
    public int? InactiveDays { get; set; }
    public CustomerSort Sort { get; set; } = CustomerSort.Name;

    public bool IsEmpty => string.IsNullOrWhiteSpace(Search) && Balance == BalanceFilter.All && Currencies.Count == 0
                           && !HasDeposit && InactiveDays == null;

    public CustomerListFilter Clone() => new()
    {
        Search = Search,
        Balance = Balance,
        Currencies = new HashSet<CurrencyType>(Currencies),
        HasDeposit = HasDeposit,
        InactiveDays = InactiveDays,
        Sort = Sort,
    };

    /// <summary>Kısa açıklama (ör. "Euro · Giden"), müşteri detayındaki önceki/sonraki bilgisinde gösterilir.</summary>
    public string Describe()
    {
        var parts = new List<string>();
        if (Currencies.Count > 0)
            parts.Add(string.Join(", ", Currencies.OrderBy(c => c).Select(c => c.GetShortName())));
        if (Balance != BalanceFilter.All)
            parts.Add(Balance switch { BalanceFilter.Giden => "Giden", BalanceFilter.Gelen => "Gelen", _ => "Hesabı sıfır" });
        if (HasDeposit)
            parts.Add("Emaneti olan");
        if (InactiveDays is { } d)
            parts.Add($"{d}+ gün işlemsiz");
        if (!string.IsNullOrWhiteSpace(Search))
            parts.Add($"\"{Search.Trim()}\"");
        return string.Join(" · ", parts);
    }
}

/// <summary>Müşteri listesinin bir satırı: bakiyeler, emanetler ve son işlem.</summary>
public sealed record CustomerRow(
    Customer Customer,
    IReadOnlyList<(CurrencyType Currency, decimal Net)> Balances,
    IReadOnlyList<(CurrencyType Currency, decimal Net)> Deposits,
    DateTime? LastTransaction,
    int TransactionCount)
{
    public decimal BalanceOf(CurrencyType currency)
    {
        foreach (var b in Balances)
            if (b.Currency == currency)
                return b.Net;
        return 0;
    }
}

/// <summary>
/// Müşteri listesi ve ana sayfa için bellekte süzme/sıralama (saf; birim testli).
/// Bakiyeler <see cref="CustomerLedger.NetBalances"/> kuralıyla: emanet hariç, gizli işlemler dahil.
/// </summary>
public static class CustomerDirectory
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly StringComparer TrComparer = StringComparer.Create(Tr, ignoreCase: true);

    public static List<CustomerRow> BuildRows(IEnumerable<Customer> customers, IEnumerable<Transaction> transactions)
    {
        var byCustomer = transactions.Where(t => !t.IsDeleted)
            .GroupBy(t => t.CustomerId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return customers.Where(c => !c.IsDeleted).Select(c =>
        {
            var list = byCustomer.GetValueOrDefault(c.Id) ?? new List<Transaction>();
            return new CustomerRow(
                c,
                CustomerLedger.NetBalances(list),
                CustomerLedger.NetDeposits(list),
                list.Count == 0 ? null : list.Max(t => t.Date),
                list.Count);
        }).ToList();
    }

    public static List<CustomerRow> Apply(IEnumerable<CustomerRow> rows, CustomerListFilter filter, DateTime now)
    {
        var query = rows;

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = Normalize(filter.Search);
            var digits = new string(filter.Search.Where(char.IsDigit).ToArray());
            query = query.Where(r =>
                Normalize(r.Customer.Name).Contains(term, StringComparison.Ordinal) ||
                (digits.Length >= 3 && r.Customer.PhoneNumber != null &&
                 new string(r.Customer.PhoneNumber.Where(char.IsDigit).ToArray()).Contains(digits, StringComparison.Ordinal)));
        }

        var currencies = filter.Currencies;
        bool InScope(CurrencyType c) => currencies.Count == 0 || currencies.Contains(c);

        query = filter.Balance switch
        {
            BalanceFilter.Giden => query.Where(r => r.Balances.Any(b => b.Net > 0 && InScope(b.Currency))),
            BalanceFilter.Gelen => query.Where(r => r.Balances.Any(b => b.Net < 0 && InScope(b.Currency))),
            BalanceFilter.Zero => query.Where(r => !r.Balances.Any(b => InScope(b.Currency))),
            // Sadece cins seçiliyse: o cinslerde açık bakiyesi olanlar
            _ => currencies.Count == 0 ? query : query.Where(r => r.Balances.Any(b => InScope(b.Currency))),
        };

        if (filter.HasDeposit)
            query = query.Where(r => r.Deposits.Any(d => d.Net > 0 && InScope(d.Currency)));

        if (filter.InactiveDays is { } days)
        {
            var limit = now.AddDays(-days);
            query = query.Where(r => r.LastTransaction != null && r.LastTransaction < limit);
        }

        return Sort(query, filter).ToList();
    }

    private static IEnumerable<CustomerRow> Sort(IEnumerable<CustomerRow> rows, CustomerListFilter filter)
    {
        switch (filter.Sort)
        {
            case CustomerSort.LastTransaction:
                return rows.OrderByDescending(r => r.LastTransaction ?? DateTime.MinValue)
                    .ThenBy(r => r.Customer.Name, TrComparer);
            case CustomerSort.Balance when filter.Currencies.Count == 1:
                var currency = filter.Currencies.First();
                return rows.OrderByDescending(r => Math.Abs(r.BalanceOf(currency)))
                    .ThenBy(r => r.Customer.Name, TrComparer);
            case CustomerSort.Balance:
                return rows.OrderByDescending(r => r.Balances.Count)
                    .ThenBy(r => r.Customer.Name, TrComparer);
            default:
                return rows.OrderBy(r => r.Customer.Name, TrComparer);
        }
    }

    /// <summary>Türkçe harf ve şapka duyarsız karşılaştırma için sadeleştirir (İ/ı, â/a…).</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        var lower = text.Trim().ToLower(Tr);
        var chars = new char[lower.Length];
        for (var i = 0; i < lower.Length; i++)
        {
            chars[i] = lower[i] switch
            {
                'â' => 'a', 'î' => 'i', 'û' => 'u', 'ı' => 'i', 'ç' => 'c', 'ğ' => 'g', 'ö' => 'o', 'ş' => 's', 'ü' => 'u',
                var c => c,
            };
        }
        return new string(chars);
    }

    /// <summary>Alfabetik (Türkçe) müşteri sırası: önceki/sonraki müşteri gezintisinin varsayılanı.</summary>
    public static List<int> AlphabeticalIds(IEnumerable<Customer> customers) =>
        customers.Where(c => !c.IsDeleted).OrderBy(c => c.Name, TrComparer).ThenBy(c => c.Id).Select(c => c.Id).ToList();
}
