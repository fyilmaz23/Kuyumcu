using Kuyumcu.Models;
using Kuyumcu.Services;
using Xunit;

namespace Kuyumcu.Core.Tests;

public class TransactionQueryTests
{
    private static readonly DateTime Now = new(2026, 11, 26, 15, 0, 0); // Perşembe
    private static readonly Dictionary<int, string> Names = new() { [1] = "Şükrü Çelik", [2] = "Ayşe Yılmaz" };
    private static int _id;

    private static Transaction Tx(int customer, TransactionType type, decimal amount, CurrencyType currency, DateTime date,
        bool deposit = false, bool hidden = false, string? note = null, bool cash = false) => new()
    {
        Id = ++_id, CustomerId = customer, Type = type, Amount = amount, CurrencyType = currency, Date = date,
        IsDeposit = deposit, IsHidden = hidden, Description = note, AffectsCash = cash,
    };

    private static readonly List<Transaction> All =
    [
        Tx(1, TransactionType.CustomerDebt, 2, CurrencyType.QuarterGold, Now.AddHours(-1), cash: true),   // bugün
        Tx(1, TransactionType.StoreDebt, 100, CurrencyType.Euro, Now.AddDays(-1)),                       // dün
        Tx(2, TransactionType.StoreDebt, 5, CurrencyType.Gold22K, Now.AddDays(-3), deposit: true),       // pazartesi, emanet
        Tx(2, TransactionType.CustomerDebt, 50, CurrencyType.Euro, Now.AddDays(-20), note: "Bilezik"),   // bu ay
        Tx(2, TransactionType.CustomerDebt, 9, CurrencyType.Euro, Now.AddHours(-2), hidden: true),       // gizli
        Tx(3, TransactionType.CustomerDebt, 9, CurrencyType.Euro, Now.AddHours(-2)),                     // silinmiş müşteri
    ];

    private static int Count(TransactionFilter f) => TransactionQuery.Apply(All, f, Names, Now).Count;

    [Fact]
    public void Gizli_ve_silinmis_musterinin_islemi_listelenmez_yeniden_eskiye()
    {
        var list = TransactionQuery.Apply(All, new TransactionFilter(), Names, Now);
        Assert.Equal(4, list.Count);
        Assert.Equal(CurrencyType.QuarterGold, list[0].CurrencyType);
    }

    [Theory]
    [InlineData(TransactionPeriod.Today, 1)]
    [InlineData(TransactionPeriod.Yesterday, 1)]
    [InlineData(TransactionPeriod.ThisWeek, 3)]
    [InlineData(TransactionPeriod.ThisMonth, 4)]
    public void Doneme_gore_suzer(TransactionPeriod period, int expected) =>
        Assert.Equal(expected, Count(new TransactionFilter { Period = period }));

    [Fact]
    public void Tur_cins_kasa_ve_arama()
    {
        Assert.Equal(2, Count(new TransactionFilter { Kind = TransactionKind.Giden }));
        Assert.Equal(1, Count(new TransactionFilter { Kind = TransactionKind.Deposit }));
        Assert.Equal(2, Count(new TransactionFilter { Currencies = { CurrencyType.Euro } }));
        Assert.Equal(1, Count(new TransactionFilter { OnlyCash = true }));
        Assert.Equal(2, Count(new TransactionFilter { Search = "sukru" }));
        Assert.Equal(1, Count(new TransactionFilter { Search = "bilezik" }));
    }

    [Fact]
    public void Ozel_aralik_bitis_gunu_dahil()
    {
        var f = new TransactionFilter { Period = TransactionPeriod.Custom, From = Now.AddDays(-3), To = Now.AddDays(-1) };
        Assert.Equal(2, Count(f));
    }

    [Fact]
    public void Toplamlar_emanet_haric_cins_basina()
    {
        var totals = TransactionQuery.Totals(TransactionQuery.Apply(All, new TransactionFilter(), Names, Now));
        var euro = totals.Single(t => t.Currency == CurrencyType.Euro);
        Assert.Equal(50m, euro.Giden);
        Assert.Equal(100m, euro.Gelen);
        Assert.Equal(-50m, euro.Net);
        Assert.DoesNotContain(totals, t => t.Currency == CurrencyType.Gold22K);
    }
}
