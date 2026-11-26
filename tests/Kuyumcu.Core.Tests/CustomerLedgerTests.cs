using Kuyumcu.Models;
using Kuyumcu.Services;
using Xunit;

namespace Kuyumcu.Core.Tests;

public class CustomerLedgerTests
{
    private static int _id;
    private static readonly DateTime Day = new(2026, 3, 1, 10, 0, 0);

    private static Transaction Tx(TransactionType type, decimal amount, int day, CurrencyType currency = CurrencyType.QuarterGold,
        bool deposit = false, bool hidden = false, int customerId = 1) => new()
    {
        Id = ++_id,
        CustomerId = customerId,
        Type = type,
        Amount = amount,
        CurrencyType = currency,
        Date = Day.AddDays(day),
        IsDeposit = deposit,
        IsHidden = hidden,
    };

    private const TransactionType Giden = TransactionType.CustomerDebt;
    private const TransactionType Gelen = TransactionType.StoreDebt;

    [Fact]
    public void Defter_odemeden_sonra_kalan_yazar_alimlarda_yazmaz()
    {
        // 2 + 1 çeyrek aldı, 1 getirdi, 1 daha aldı, 3 getirdi (hesap sıfır)
        var txs = new[] { Tx(Giden, 2, 0), Tx(Giden, 1, 1), Tx(Gelen, 1, 2), Tx(Giden, 1, 3), Tx(Gelen, 3, 4) };
        var group = CustomerLedger.Build(txs, false).Groups.Single();

        var page = LedgerPage.From(group, 10);

        Assert.Equal(0, page.OlderCount);
        Assert.Equal(new[] { false, false, true, false, true }, page.Rows.Select(r => r.ShowBalance));
        Assert.Equal(2m, page.Rows[2].BalanceAfter);
        Assert.Equal(0m, page.Rows[4].BalanceAfter);
    }

    [Fact]
    public void Defter_son_satirda_her_zaman_kalan_yazar_ve_eskileri_devreder()
    {
        var txs = new[] { Tx(Giden, 5, 0), Tx(Gelen, 2, 1), Tx(Giden, 1, 2), Tx(Giden, 1, 3) };
        var group = CustomerLedger.Build(txs, false).Groups.Single();

        var page = LedgerPage.From(group, 2);

        Assert.Equal(2, page.OlderCount);
        Assert.Equal(3m, page.CarriedForward);
        Assert.Equal(new[] { false, true }, page.Rows.Select(r => r.ShowBalance));
        Assert.Equal(5m, page.Rows[^1].BalanceAfter);
    }

    [Fact]
    public void Defter_alacakli_musteride_giden_odeme_sayilir()
    {
        // Müşteri 1000 TL bıraktı (Gelen), 400 TL'lik mal aldı (Giden): kalan çizgisi çekilir
        var txs = new[] { Tx(Gelen, 1000, 0, CurrencyType.TurkishLira), Tx(Giden, 400, 1, CurrencyType.TurkishLira) };
        var page = LedgerPage.From(CustomerLedger.Build(txs, false).Groups.Single(), 10);

        Assert.Equal(new[] { false, true }, page.Rows.Select(r => r.ShowBalance));
        Assert.Equal(-600m, page.Rows[1].BalanceAfter);
    }

    [Fact]
    public void Emanet_iadesi_emanet_bakiyesini_dusurur_ve_normal_bakiyeye_girmez()
    {
        var txs = new[]
        {
            Tx(Giden, 1, 0),
            Tx(Gelen, 10, 1, CurrencyType.Gold22K, deposit: true),
            Tx(Giden, 4, 2, CurrencyType.Gold22K, deposit: true),
        };
        var ledger = CustomerLedger.Build(txs, false);

        Assert.Single(ledger.Groups);
        Assert.Equal(CurrencyType.QuarterGold, ledger.Groups[0].Key);
        var deposit = Assert.Single(ledger.DepositTotals);
        Assert.Equal((CurrencyType.Gold22K, 6m), deposit);
        Assert.Equal(6m, ledger.DepositGroups.Single().Balance);
        Assert.Equal(new[] { (CurrencyType.Gold22K, 6m) }, CustomerLedger.NetDeposits(txs));
    }

    [Fact]
    public void Tamamen_iade_edilen_emanet_toplamlarda_gorunmez_ama_gecmisi_kalir()
    {
        var txs = new[]
        {
            Tx(Gelen, 3, 0, CurrencyType.FullGold, deposit: true),
            Tx(Giden, 3, 5, CurrencyType.FullGold, deposit: true),
        };
        var ledger = CustomerLedger.Build(txs, false);

        Assert.Empty(ledger.DepositTotals);
        Assert.Equal(2, ledger.Deposits.Count);
        Assert.Equal(0m, ledger.DepositGroups.Single().Balance);
    }

    [Theory]
    [InlineData(200, false, TransactionType.StoreDebt)]   // 200 € Giden → 200 € Gelen
    [InlineData(-50, false, TransactionType.CustomerDebt)] // 50 € Gelen → 50 € Giden
    [InlineData(7, true, TransactionType.CustomerDebt)]    // dükkânda 7 emanet → 7 Giden emanet (iade)
    [InlineData(-2, true, TransactionType.StoreDebt)]
    public void Sifirlama_ters_yonde_islem_uretir(decimal balance, bool deposit, TransactionType expected)
    {
        var t = CustomerLedger.Settlement(42, CurrencyType.Euro, balance, deposit, Day, " Hesap sıfırlandı ");

        Assert.NotNull(t);
        Assert.Equal(expected, t!.Type);
        Assert.Equal(Math.Abs(balance), t.Amount);
        Assert.Equal(deposit, t.IsDeposit);
        Assert.Equal(42, t.CustomerId);
        Assert.Equal(CurrencyType.Euro, t.CurrencyType);
        Assert.Equal("Hesap sıfırlandı", t.Description);
    }

    [Fact]
    public void Sifirlama_sonrasi_bakiye_sifir_olur()
    {
        var txs = new List<Transaction>
        {
            Tx(Giden, 200, 0, CurrencyType.Euro), Tx(Gelen, 50, 1, CurrencyType.Euro),
            Tx(Gelen, 5, 1, CurrencyType.Gold22K, deposit: true),
        };
        var ledger = CustomerLedger.Build(txs, false);
        foreach (var g in ledger.Groups)
            txs.Add(CustomerLedger.Settlement(1, g.Key, g.Balance, false, Day.AddDays(2), null)!);
        foreach (var g in ledger.DepositGroups)
            txs.Add(CustomerLedger.Settlement(1, g.Key, g.Balance, true, Day.AddDays(2), null)!);

        var after = CustomerLedger.Build(txs, false);
        Assert.All(after.Groups, g => Assert.Equal(0m, g.Balance));
        Assert.Empty(after.DepositTotals);
        Assert.Null(CustomerLedger.Settlement(1, CurrencyType.Euro, 0, false, Day, null));
    }

    [Fact]
    public void Gizli_islemler_gorunurluk_acilmadikca_bakiyeye_girmez()
    {
        var txs = new[] { Tx(Giden, 2, 0), Tx(Giden, 5, 1, hidden: true) };
        Assert.Equal(2m, CustomerLedger.Build(txs, false).Groups.Single().Balance);
        Assert.Equal(7m, CustomerLedger.Build(txs, true).Groups.Single().Balance);
    }
}

public class CustomerDirectoryTests
{
    private static int _id;
    private static readonly DateTime Now = new(2026, 11, 26, 12, 0, 0);

    private static Transaction Tx(int customer, TransactionType type, decimal amount, CurrencyType currency, int daysAgo = 1,
        bool deposit = false) => new()
    {
        Id = ++_id, CustomerId = customer, Type = type, Amount = amount, CurrencyType = currency,
        Date = Now.AddDays(-daysAgo), IsDeposit = deposit,
    };

    private static List<CustomerRow> Rows()
    {
        var customers = new List<Customer>
        {
            new() { Id = 1, Name = "Şükrü Çelik", PhoneNumber = "5321234567" },
            new() { Id = 2, Name = "ayşe yılmaz", PhoneNumber = "5559876543" },
            new() { Id = 3, Name = "İbrahim Öz" },
            new() { Id = 4, Name = "Silinmiş", IsDeleted = true },
        };
        var txs = new List<Transaction>
        {
            Tx(1, TransactionType.CustomerDebt, 200, CurrencyType.Euro),                  // 1: Euro Giden
            Tx(1, TransactionType.StoreDebt, 1000, CurrencyType.TurkishLira),            // 1: TL Gelen
            Tx(2, TransactionType.StoreDebt, 50, CurrencyType.Euro, daysAgo: 200),       // 2: Euro Gelen, eski
            Tx(2, TransactionType.StoreDebt, 3, CurrencyType.Gold22K, daysAgo: 200, deposit: true),
            Tx(3, TransactionType.CustomerDebt, 1, CurrencyType.QuarterGold, daysAgo: 10),
            Tx(3, TransactionType.StoreDebt, 1, CurrencyType.QuarterGold, daysAgo: 5),   // 3: hesap sıfır
            Tx(4, TransactionType.CustomerDebt, 9, CurrencyType.Euro),
        };
        return CustomerDirectory.BuildRows(customers, txs);
    }

    private static int[] Ids(CustomerListFilter f) => CustomerDirectory.Apply(Rows(), f, Now).Select(r => r.Customer.Id).ToArray();

    [Fact]
    public void Silinmis_musteri_listelenmez_ve_ada_gore_turkce_siralanir()
    {
        Assert.Equal(new[] { 2, 3, 1 }, Ids(new CustomerListFilter()));
    }

    [Fact]
    public void Cins_ve_yon_birlikte_suzer()
    {
        var f = new CustomerListFilter { Balance = BalanceFilter.Giden, Currencies = { CurrencyType.Euro } };
        Assert.Equal(new[] { 1 }, Ids(f));

        f.Balance = BalanceFilter.Gelen;
        Assert.Equal(new[] { 2 }, Ids(f));

        f.Balance = BalanceFilter.All;
        Assert.Equal(new[] { 2, 1 }, Ids(f));
    }

    [Fact]
    public void Yon_tek_basina_herhangi_bir_cinste_arar()
    {
        Assert.Equal(new[] { 2, 1 }, Ids(new CustomerListFilter { Balance = BalanceFilter.Gelen }));
        Assert.Equal(new[] { 3 }, Ids(new CustomerListFilter { Balance = BalanceFilter.Zero }));
    }

    [Fact]
    public void Emanet_ve_islemsizlik_suzgeci()
    {
        Assert.Equal(new[] { 2 }, Ids(new CustomerListFilter { HasDeposit = true }));
        Assert.Equal(new[] { 2 }, Ids(new CustomerListFilter { InactiveDays = 90 }));
    }

    [Fact]
    public void Arama_turkce_harf_duyarsiz_ve_telefonla()
    {
        Assert.Equal(new[] { 1 }, Ids(new CustomerListFilter { Search = "sukru" }));
        Assert.Equal(new[] { 2 }, Ids(new CustomerListFilter { Search = "AYŞE" }));
        Assert.Equal(new[] { 3 }, Ids(new CustomerListFilter { Search = "ibra" }));
        Assert.Equal(new[] { 2 }, Ids(new CustomerListFilter { Search = "987" }));
    }

    [Fact]
    public void Tek_cinste_bakiyeye_gore_siralar()
    {
        var f = new CustomerListFilter { Currencies = { CurrencyType.Euro }, Sort = CustomerSort.Balance };
        Assert.Equal(new[] { 1, 2 }, Ids(f));
        Assert.Equal("Euro", f.Describe());
    }
}
