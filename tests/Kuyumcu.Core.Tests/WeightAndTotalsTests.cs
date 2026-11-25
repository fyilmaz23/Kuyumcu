using Kuyumcu.Models;
using Kuyumcu.Services;
using Kuyumcu.Stock;

namespace Kuyumcu.Tests;

public class WeightFormatTests
{
    [Theory]
    [InlineData("4,25", 4250)]
    [InlineData("4.25", 4250)]
    [InlineData("4,255", 4255)]
    [InlineData("4,2", 4200)]
    [InlineData("12", 12000)]
    [InlineData(",5", 500)]
    [InlineData("0,001", 1)]
    [InlineData(" 4,25 gr ", 4250)]
    [InlineData("4,25g", 4250)]
    public void Gecerli_gram_okunur(string input, int expectedMg)
    {
        Assert.True(WeightFormat.TryParseGram(input, out var mg, out var error), error);
        Assert.Equal(expectedMg, mg);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("0,000")]
    [InlineData("4,2555")]      // 4 ondalık
    [InlineData("1.234,5")]     // binlik ayıracı kabul edilmez
    [InlineData("abc")]
    [InlineData("-3")]
    [InlineData("20000")]       // makul üst sınırın üstünde
    public void Gecersiz_gram_reddedilir(string input)
    {
        Assert.False(WeightFormat.TryParseGram(input, out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void Gram_gosterimi_Turkce()
    {
        Assert.Equal("4,25", WeightFormat.Gram2(4250));
        Assert.Equal("4,255", WeightFormat.Gram(4255));
        Assert.Equal("4,25", WeightFormat.Gram(4250));
        Assert.Equal("1.234,50", WeightFormat.Gram2(1_234_500));
        Assert.Equal("0,916", WeightFormat.Milyem(916));
        Assert.Equal("4,255", WeightFormat.ToInput(4255));
    }

    [Theory]
    [InlineData("0,916", 916)]
    [InlineData("0.050", 50)]
    [InlineData("916", 916)]
    [InlineData("50", 50)]
    public void Milyem_okunur(string input, int expected)
    {
        Assert.True(WeightFormat.TryParseMilyem(input, out var value));
        Assert.Equal(expected, value);
    }
}

public class StockCalculatorTests
{
    private static readonly List<KaratDefinition> Karats = StockDefaults.Karats
        .Select(k => new KaratDefinition { Karat = k.Karat, Milyem = k.Milyem }).ToList();

    private static StockItem Item(string code, int karat, int mg, int stoneMg = 0) =>
        new() { Code = code, Karat = karat, WeightMg = mg, StoneWeightMg = stoneMg };

    [Fact]
    public void Has_karsiligi_net_gram_ve_milyemden_hesaplanir()
    {
        Assert.Equal(9160, StockCalculator.HasMg(10_000, 916));
        Assert.Equal(5850, StockCalculator.HasMg(10_000, 585));
        Assert.Equal(1, StockCalculator.HasMg(1, 916)); // 0,916 mg → 1 mg (yukarı yuvarlanır)
    }

    [Fact]
    public void Ayar_bazinda_toplam_adet_gram_ve_has()
    {
        var items = new[]
        {
            Item("a", 22, 10_250),
            Item("b", 22, 20_000),
            Item("c", 14, 3_500, stoneMg: 500),   // net 3 gr
        };

        var totals = StockCalculator.Calculate(items, Karats);

        Assert.Equal(3, totals.Count);
        Assert.Equal(33_750, totals.WeightMg);
        Assert.Equal(33_250, totals.NetWeightMg);

        var k22 = totals.ByKarat.Single(k => k.Karat == 22);
        Assert.Equal(2, k22.Count);
        Assert.Equal(30_250, k22.WeightMg);
        Assert.Equal(StockCalculator.HasMg(30_250, 916), k22.HasMg); // 27,709 gr has

        var k14 = totals.ByKarat.Single(k => k.Karat == 14);
        Assert.Equal(3_000, k14.NetWeightMg);
        Assert.Equal(1_755, k14.HasMg);

        Assert.Equal(k22.HasMg + k14.HasMg, totals.HasMg);
        Assert.Equal(22, totals.ByKarat[0].Karat); // yüksek ayar önce
    }

    [Fact]
    public void Kasa_urunu_adet_ve_gram_ile_hesaplanir()
    {
        var ceyrek = new BulkProduct { Name = "Çeyrek", Unit = BulkUnit.Piece, Karat = 22, PieceWeightMg = 1754, Amount = 10 };
        var gram = new BulkProduct { Name = "Gram", Unit = BulkUnit.Gram, Karat = 24, Amount = 25_000 };

        var c = StockCalculator.CalculateBulk(ceyrek, Karats);
        Assert.Equal(17_540, c.WeightMg);
        Assert.Equal(StockCalculator.HasMg(17_540, 916), c.HasMg);

        var g = StockCalculator.CalculateBulk(gram, Karats);
        Assert.Equal(25_000, g.WeightMg);
        Assert.Equal(24_875, g.HasMg);
    }

    [Fact]
    public void Satis_fiyati_onerisi()
    {
        // 10 gr 22 ayar, işçilik 0,040: 10 × 0,956 × 6.160 = 58.889,6 → 58.890
        Assert.Equal(58_890m, StockCalculator.SuggestSalePrice(10_000, 916, 40, 6160m));
        Assert.Equal(0m, StockCalculator.SuggestSalePrice(10_000, 916, 40, 0m));
    }

    [Fact]
    public void Veresiye_22_ayar_gram_karsiligi()
    {
        // 10 gr 14 ayar işçiliksiz: 10 × 0,585 / 0,916 = 6,39 gr 22 ayar
        Assert.Equal(6.39m, StockCalculator.Suggest22KGram(10_000, 585, 0, 916));
        // 22 ayar işçiliksiz: gram aynen
        Assert.Equal(10m, StockCalculator.Suggest22KGram(10_000, 916, 0, 916));
    }

    [Fact]
    public void Sayim_eksik_ve_fazla_listesi()
    {
        var expected = new[] { Item("200000000011", 22, 1000), Item("200000000028", 22, 1000), Item("200000000035", 14, 1000) };
        var scanned = new[] { "200000000011", "200000000035", "200000000042" };

        var result = StockCalculator.CompareCount(expected, scanned);

        Assert.Equal(new[] { "200000000011", "200000000035" }, result.Found.Select(i => i.Code));
        Assert.Equal("200000000028", Assert.Single(result.Missing).Code);
        Assert.Equal("200000000042", Assert.Single(result.Extra));
    }
}

public class CsvTests
{
    [Fact]
    public void Ayrac_tirnak_ve_formul_kacisi()
    {
        var csv = Csv.Build(new[] { "Kod", "Not" }, new[]
        {
            new[] { "200000000011", "a;b" },
            new[] { "x", "\"tırnak\"" },
            new[] { "=SUM(A1)", null }
        });
        var lines = csv.Split("\r\n");
        Assert.Equal("Kod;Not", lines[0]);
        Assert.Equal("200000000011;\"a;b\"", lines[1]);
        Assert.Equal("x;\"\"\"tırnak\"\"\"", lines[2]);
        Assert.Equal("'=SUM(A1);", lines[3]);
    }
}
