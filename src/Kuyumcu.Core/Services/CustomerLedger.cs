using Kuyumcu.Models;

namespace Kuyumcu.Services;

/// <summary>
/// Bir müşterinin hesap durumu (bakiyeler, kümülatif kalanlar, emanetler).
///
/// Kurallar masaüstü ve Android Müşteri Detayı ekranlarında ortaktır:
///  - Giden (<see cref="TransactionType.CustomerDebt"/>) bakiyeyi artırır, Gelen (<see cref="TransactionType.StoreDebt"/>) azaltır.
///    Bakiye &gt; 0 ise Giden (müşteri borçlu), &lt; 0 ise Gelen (müşteri alacaklı).
///  - Emanetler bakiyeye girmez, ayrı tutulur. Gelen emanet = müşteri bıraktı, Giden emanet = müşteriye geri verildi
///    (iade). Emanet bakiyesi = Gelen − Giden (&gt; 0: dükkânda duran emanet). Eski kayıtlarda sadece Gelen emanet vardır.
///  - Gizli işlemler, görünürlük açılmadıkça bakiyeye de listeye de girmez.
///  - Kalan (çalışan bakiye) her para biriminde tüm geçmiş üzerinden tarih + kimlik sırasıyla hesaplanır.
///  - Hesap sıfırlama silmez: bakiyenin tersi yönde bir işlem eklenir (bkz. <see cref="Settlement"/>).
/// </summary>
public sealed class CustomerLedger
{
    private CustomerLedger(
        List<TransactionGroup> groups,
        List<Transaction> deposits,
        List<TransactionGroup> depositGroups,
        List<Transaction> visible,
        bool hasHiddenTransactions,
        bool showingHidden)
    {
        Groups = groups;
        Deposits = deposits;
        DepositGroups = depositGroups;
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

    /// <summary>Emanet işlemleri (yeniden eskiye). Gizli olsa da listelenir.</summary>
    public IReadOnlyList<Transaction> Deposits { get; }

    /// <summary>
    /// Para birimi başına emanet hareketleri (eskiden yeniye). Bakiye ve kalan <see cref="DepositSigned"/> işaretiyle:
    /// pozitif = dükkânda duran emanet.
    /// </summary>
    public IReadOnlyList<TransactionGroup> DepositGroups { get; }

    /// <summary>Dükkânda duran emanetler (para birimi başına, sıfır olmayanlar).</summary>
    public IEnumerable<(CurrencyType Currency, decimal Total)> DepositTotals =>
        DepositGroups.Where(g => g.Balance != 0).Select(g => (g.Key, g.Balance));

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
    /// <param name="showHidden">Gizli işlemler hesaba katılsın mı (müşteri adına tıklamak).</param>
    public static CustomerLedger Build(IEnumerable<Transaction> customerTransactions, bool showHidden)
    {
        var all = customerTransactions.Where(t => !t.IsDeleted).ToList();

        var deposits = all.Where(t => t.IsDeposit)
            .OrderByDescending(t => t.Date).ThenByDescending(t => t.Id)
            .ToList();

        var regular = all.Where(t => !t.IsDeposit).ToList();
        var hasHidden = regular.Any(t => t.IsHidden);
        var counted = showHidden ? regular : regular.Where(t => !t.IsHidden).ToList();

        var groups = BuildGroups(counted, Signed);
        var depositGroups = BuildGroups(deposits, DepositSigned);

        var visible = all.Where(t => showHidden || !t.IsHidden).ToList();
        return new CustomerLedger(groups, deposits, depositGroups, visible, hasHidden, showHidden);
    }

    private static List<TransactionGroup> BuildGroups(IEnumerable<Transaction> transactions, Func<Transaction, decimal> signed)
    {
        var groups = new List<TransactionGroup>();
        foreach (var currencyGroup in transactions.GroupBy(t => t.CurrencyType).OrderBy(g => g.Key))
        {
            var ordered = currencyGroup.OrderBy(t => t.Date).ThenBy(t => t.Id).ToList();

            var running = 0m;
            var runningBalances = new Dictionary<int, decimal>();
            foreach (var t in ordered)
            {
                running += signed(t);
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
        return groups;
    }

    /// <summary>Giden +, Gelen −.</summary>
    public static decimal Signed(Transaction t) =>
        t.Type == TransactionType.CustomerDebt ? t.Amount : -t.Amount;

    /// <summary>Emanette: Gelen (bırakılan) +, Giden (iade) −.</summary>
    public static decimal DepositSigned(Transaction t) =>
        t.Type == TransactionType.StoreDebt ? t.Amount : -t.Amount;

    /// <summary>
    /// Bakiyeyi sıfırlayan işlem (silmeden, tersi yönde giriş). Bakiye 0 ise null.
    /// Normal hesapta: Giden bakiyesi → aynı miktar Gelen; Gelen bakiyesi → Giden.
    /// Emanette (<paramref name="isDeposit"/>): dükkânda duran emanet (&gt; 0) → Giden emanet (iade).
    /// </summary>
    /// <param name="balance">Normal hesapta <see cref="Signed"/>, emanette <see cref="DepositSigned"/> işaretiyle bakiye.</param>
    public static Transaction? Settlement(int customerId, CurrencyType currency, decimal balance, bool isDeposit,
        DateTime date, string? description)
    {
        if (balance == 0)
            return null;

        // Normal: bakiye > 0 (Giden) → Gelen. Emanet: bakiye > 0 (bırakılmış) → Giden.
        var type = (balance > 0) ^ isDeposit ? TransactionType.StoreDebt : TransactionType.CustomerDebt;
        return new Transaction
        {
            CustomerId = customerId,
            CurrencyType = currency,
            Amount = Math.Abs(balance),
            Type = type,
            IsDeposit = isDeposit,
            Date = date,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
        };
    }

    /// <summary>
    /// Müşteri listesi ve genel özet için para birimi başına net bakiyeler (sıfır olmayanlar).
    /// <c>DatabaseService.GetCurrencyBalanceSummaryAsync</c> ile aynı kural: emanetler hariç, gizli işlemler DAHİL.
    /// </summary>
    public static List<(CurrencyType Currency, decimal Net)> NetBalances(IEnumerable<Transaction> customerTransactions) =>
        customerTransactions
            .Where(t => !t.IsDeleted && !t.IsDeposit)
            .GroupBy(t => t.CurrencyType)
            .Select(g => (g.Key, g.Sum(Signed)))
            .Where(x => x.Item2 != 0)
            .OrderBy(x => x.Key)
            .ToList();

    /// <summary>Dükkânda duran emanetler (para birimi başına, sıfır olmayanlar; gizliler dahil).</summary>
    public static List<(CurrencyType Currency, decimal Net)> NetDeposits(IEnumerable<Transaction> customerTransactions) =>
        customerTransactions
            .Where(t => !t.IsDeleted && t.IsDeposit)
            .GroupBy(t => t.CurrencyType)
            .Select(g => (g.Key, g.Sum(DepositSigned)))
            .Where(x => x.Item2 != 0)
            .OrderBy(x => x.Key)
            .ToList();
}

/// <summary>Defter görünümünde bir satır: işlem ve altına "Kalan" çizgisi çekilip çekilmeyeceği.</summary>
/// <param name="BalanceAfter">Bu işlemden sonraki kalan (grubun işaret kuralıyla).</param>
/// <param name="ShowBalance">Altına çizgi çekilip kalan yazılır mı?</param>
public sealed record LedgerRow(Transaction Transaction, decimal BalanceAfter, bool ShowBalance);

/// <summary>
/// Bir para biriminin defter sayfası (bakkal defteri gibi): alınanlar alt alta yazılır; müşteri ödeme getirdiğinde
/// (bakiyeyi sıfıra doğru azaltan her işlemde) altına çizgi çekilip kalan yazılır. En alttaki satırda kalan her zaman yazılır.
/// Sayfaya sığmayan eski satırların toplamı en üstte "devir" olarak gösterilir.
/// </summary>
/// <param name="OlderCount">Gösterilmeyen eski işlem sayısı (0 ise devir yok).</param>
/// <param name="CarriedForward">Gösterilmeyen eski işlemlerden devreden bakiye.</param>
public sealed record LedgerPage(int OlderCount, decimal CarriedForward, IReadOnlyList<LedgerRow> Rows)
{
    /// <param name="group">Eskiden yeniye sıralı işlemler ve kalanlar (<see cref="CustomerLedger.Groups"/> ya da
    /// <see cref="CustomerLedger.DepositGroups"/>).</param>
    /// <param name="take">Gösterilecek en yeni işlem sayısı.</param>
    public static LedgerPage From(TransactionGroup group, int take)
    {
        var all = group.Transactions;
        var skip = Math.Max(0, all.Count - Math.Max(0, take));
        var carried = skip == 0 ? 0m : group.RunningBalances.GetValueOrDefault(all[skip - 1].Id);

        var rows = new List<LedgerRow>(all.Count - skip);
        var before = carried;
        for (var i = skip; i < all.Count; i++)
        {
            var t = all[i];
            var after = group.RunningBalances.GetValueOrDefault(t.Id);
            var change = after - before;
            // Bakiyeyi sıfıra doğru götüren (ödeme / iade) ya da yön değiştiren işlem: altına çizgi çekilir
            var settles = before != 0 && change != 0 && Math.Sign(change) != Math.Sign(before);
            var isLast = i == all.Count - 1;
            rows.Add(new LedgerRow(t, after, settles || isLast));
            before = after;
        }

        return new LedgerPage(skip, carried, rows);
    }
}
