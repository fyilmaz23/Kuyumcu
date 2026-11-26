using Kuyumcu.Models;
using Kuyumcu.Stock;
using Xunit;

namespace Kuyumcu.Core.Tests;

public class SalePriceTests
{
    // elazigaltinfiyatlari.com tablosundaki satırlar (26.11.2026 örneği)
    private static readonly List<GoldPriceDto> Prices =
    [
        new("24 Ayar", 6000, 6060), new("22 Ayar", 5450, 5990), new("14 Ayar", 5040, 5040),
        new("Çeyrek", 9790, 10290), new("Gram Altın", 6000, 6300),
    ];

    [Fact]
    public void Yirmi_gram_22_ayar_bilezik_listedeki_22_ayar_satisla_carpilir()
    {
        var q = SalePrice.Calculate(20_000, 22, 916, 0, Prices)!;
        Assert.Equal(5990m, q.UnitPrice);
        Assert.True(q.FromList);
        Assert.Equal(119_800m, q.Total);
        Assert.Equal("22 Ayar satış", q.Source);
    }

    [Fact]
    public void Iscilik_gram_basina_eklenir()
    {
        var q = SalePrice.Calculate(20_000, 22, 916, 150m, Prices)!;
        Assert.Equal(3_000m, q.Labor);
        Assert.Equal(122_800m, q.Total);
        Assert.Equal(119_800m, q.GoldValue);
    }

    [Fact]
    public void Kesirli_gram_tam_TLye_yuvarlanir()
    {
        Assert.Equal(122_016m, SalePrice.Calculate(20_370, 22, 916, 0, Prices)!.Total); // 20,37 × 5990 = 122.016,3
        Assert.Equal(25_200m, SalePrice.Calculate(5_000, 14, 585, 0, Prices)!.Total);
    }

    [Fact]
    public void Listede_olmayan_ayar_24_ayar_satis_carpi_milyem()
    {
        var q = SalePrice.Calculate(10_000, 18, 750, 0, Prices)!;
        Assert.False(q.FromList);
        Assert.Equal(4545m, q.UnitPrice);
        Assert.Equal(45_450m, q.Total);
    }

    [Fact]
    public void Fiyat_yoksa_hesaplanmaz()
    {
        Assert.Null(SalePrice.Calculate(10_000, 22, 916, 0, null));
        Assert.Null(SalePrice.Calculate(10_000, 22, 916, 0, new List<GoldPriceDto>()));
        Assert.Null(SalePrice.Calculate(0, 22, 916, 0, Prices));
    }

    [Fact]
    public void Veresiye_22_ayar_karsiligi_fiyat_bolu_22_ayar_satis()
    {
        Assert.Equal(20m, SalePrice.To22KGram(119_800m, Prices));
        Assert.Equal(20.5m, SalePrice.To22KGram(122_800m, Prices));
        Assert.Null(SalePrice.To22KGram(1000m, null));
    }

    [Fact]
    public void Gram_altin_ve_ceyrek_ayar_satiri_sayilmaz()
    {
        Assert.Equal("22 Ayar", SalePrice.FindKaratRow(Prices, 22)!.Type);
        Assert.Null(SalePrice.FindKaratRow(Prices, 18));
    }
}
