using Kuyumcu.Models;

namespace Kuyumcu.Services;

/// <summary>
/// Bir müşterinin hesap durumu (bakiyeler, kümülatif kalanlar, emanetler).
///
/// Kurallar masaüstündeki Müşteri Detayı ekranıyla AYNIDIR; değiştirmeden önce iki tarafı birlikte düşünün:
///  - Giden (<see cref="TransactionType.CustomerDebt"/>) bakiyeyi artırır, Gelen (<see cref="TransactionType.StoreDebt"/>) azaltır.
///    Bakiye &gt; 0 ise Giden (müşteri borçlu), &lt; 0 ise Gelen (müşteri alacaklı).
///  - Emanetler bakiyeye girmez, ayrı listelenir.
///  - Gizli işlemler, görünürlük açılmadıkça bakiyeye de listeye de girmez.
///  - Kalan (çalışan bakiye) her para biriminde tüm geçmiş üzerinden tarih + kimlik sırasıyla hesaplanır.
/// </summary>
public sealed class CustomerLedger
{
    private CustomerLedger(
        List<TransactionGroup> groups,
        List<Transaction> deposits,
        List<Transaction> visible,
        bool hasHiddenTransactions,
        bool showingHidden)
    {
        Groups = groups;
        Deposits = deposits;
        _visible = visible;
        HasHiddenTransactions = hasHiddenTransactions;
        ShowingHidden = showingHidden;
    }

    private readonly List<Transaction> _visible;

    /// <summary>
    /// Para birimi başına hareketler (eskiden yeniye), bakiye ve her işlemden sonraki kalan.
    /// Sıra: <see cref="CurrencyType"/> tanım sırası (TL, Dolar, ... , Ziynet).
    /// </summary>
    public IReadOnlyList<TransactionGroup> Groups { get; }

    /// <summary>Emanet işlemleri (yeniden eskiye). Masaüstünde olduğu gibi gizli olsa da listelenir.</summary>
    public IReadOnlyList<Transaction> Deposits { get; }

    /// <summary>Emanetlerin para birimi başına toplamı.</summary>
    public IEnumerable<(CurrencyType Currency, decimal Total)> DepositTotals =>
        Deposits.GroupBy(t => t.CurrencyType).OrderBy(g => g.Key).Select(g => (g.Key, g.Sum(t => t.Amount)));

    /// <summary>Müşterinin (emanet dışı) gizli işlemi var mı?</summary>
    public bool HasHiddenTransactions { get; }

    /// <summary>Gizli işlemler hesaba katılıyor mu?</summary>
    public bool ShowingHidden { get; }

    /// <summary>Gösterilen işlem sayısı (emanetler dahil).</summary>
    public int VisibleCount => _visible.Count;

    /// <summary>Son işlemler (emanetler dahil; gizliler görünürlük ayarına göre), yeniden eskiye.</summary>
    public IEnumerable<Transaction> Recent(int count) =>
        _visible.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id).Take(count);

    /// <summary>
    /// Müşterinin silinmemiş işlemlerinden hesap durumunu oluşturur.
    /// </summary>
    /// <param name="customerTransactions">Müşterinin işlemleri (silinmişler varsa atılır).</param>
    /// <param name="showHidden">Gizli işlemler hesaba katılsın mı (masaüstünde müşteri adına tıklamak).</param>
    public static CustomerLedger Build(IEnumerable<Transaction> customerTransactions, bool showHidden)
    {
        var all = customerTransactions.Where(t => !t.IsDeleted).ToList();

        var deposits = all.Where(t => t.IsDeposit)
            .OrderByDescending(t => t.Date).ThenByDescending(t => t.Id)
            .ToList();

        var regular = all.Where(t => !t.IsDeposit).ToList();
        var hasHidden = regular.Any(t => t.IsHidden);
        var counted = showHidden ? regular : regular.Where(t => !t.IsHidden).ToList();

        var groups = new List<TransactionGroup>();
        foreach (var currencyGroup in counted.GroupBy(t => t.CurrencyType).OrderBy(g => g.Key))
        {
            var ordered = currencyGroup.OrderBy(t => t.Date).ThenBy(t => t.Id).ToList();

            var running = 0m;
            var runningBalances = new Dictionary<int, decimal>();
            foreach (var t in ordered)
            {
                running += Signed(t);
                runningBalances[t.Id] = running;
            }

            groups.Add(new TransactionGroup
            {
                Key = currencyGroup.Key,
                Transactions = ordered,
                Balance = running,
                RunningBalances = runningBalances,
            });
        }

        var visible = all.Where(t => showHidden || !t.IsHidden).ToList();
        return new CustomerLedger(groups, deposits, visible, hasHidden, showHidden);
    }

    /// <summary>Giden +, Gelen −.</summary>
    public static decimal Signed(Transaction t) =>
        t.Type == TransactionType.CustomerDebt ? t.Amount : -t.Amount;

    /// <summary>
    /// Müşteri listesi ve genel özet için para birimi başına net bakiyeler (sıfır olmayanlar).
    /// Masaüstündeki <c>DatabaseService.GetCurrencyBalanceSummaryAsync</c> ile aynı kural:
    /// emanetler hariç, gizli işlemler DAHİL.
    /// </summary>
    public static List<(CurrencyType Currency, decimal Net)> NetBalances(IEnumerable<Transaction> customerTransactions) =>
        customerTransactions
            .Where(t => !t.IsDeleted && !t.IsDeposit)
            .GroupBy(t => t.CurrencyType)
            .Select(g => (g.Key, g.Sum(Signed)))
            .Where(x => x.Item2 != 0)
            .OrderBy(x => x.Key)
            .ToList();
}
