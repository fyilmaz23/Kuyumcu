using Kuyumcu.Stock;

namespace Kuyumcu.Tests;

public class ProductCodeTests
{
    [Fact]
    public void Format_ilk_kod_12_hane_ve_2_ile_baslar()
    {
        var code = ProductCode.Format(1);
        Assert.Equal("200000000011", code);
        Assert.Equal(12, code.Length);
        Assert.True(ProductCode.IsValid(code));
    }

    [Fact]
    public void Kodlar_sirayla_artar()
    {
        Assert.Equal("200000000028", ProductCode.Format(2));
        Assert.Equal("200000001230", ProductCode.Format(123));
        Assert.True(string.CompareOrdinal(ProductCode.Format(99), ProductCode.Format(100)) < 0);
    }

    [Fact]
    public void Kontrol_hanesi_GS1_mod10_ile_uyumlu()
    {
        // Bilinen UPC-A: 036000291452
        Assert.Equal('2', ProductCode.ComputeCheckDigit("03600029145"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(1234567)]
    [InlineData(9_999_999_999)]
    public void Sira_numarasi_koddan_geri_okunur(long sequence)
    {
        Assert.True(ProductCode.TryGetSequence(ProductCode.Format(sequence), out var back));
        Assert.Equal(sequence, back);
    }

    [Fact]
    public void Test_etiketi_kodu_gecerli_ama_hicbir_urune_verilmez()
    {
        Assert.Equal("200000000004", ProductCode.TestCode);
        Assert.True(ProductCode.ParseInput(ProductCode.TestCode).IsValid); // okuyucu denemesinde okunabilsin
        Assert.True(ProductCode.IsTestCode("200000000004"));
        Assert.False(ProductCode.IsTestCode(ProductCode.Format(1)));
        Assert.Equal(ProductCode.TestCode, Kuyumcu.Labels.LabelLayout.SampleCode);
    }

    [Fact]
    public void Gecersiz_sira_numarasi_hata_verir()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ProductCode.Format(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProductCode.Format(10_000_000_000));
    }

    [Fact]
    public void Tek_hane_hatasi_ve_yer_degistirme_yakalanir()
    {
        var code = ProductCode.Format(1234567);           // 200012345671 biçiminde
        var chars = code.ToCharArray();
        chars[7] = chars[7] == '9' ? '8' : (char)(chars[7] + 1);
        Assert.False(ProductCode.IsValid(new string(chars)));

        var swapped = code.ToCharArray();
        (swapped[6], swapped[7]) = (swapped[7], swapped[6]);
        Assert.False(ProductCode.IsValid(new string(swapped)));
    }

    [Theory]
    [InlineData("200000000011", true)]
    [InlineData(" 200000000011 ", true)]        // okuyucu boşluk ekleyebilir
    [InlineData("2000000000ş1", false)]         // Türkçe Q klavye tuzağı
    [InlineData("20000000001", false)]          // eksik hane
    [InlineData("100000000011", false)]         // mağaza kodu değil
    [InlineData("200000000012", false)]         // kontrol hanesi yanlış
    [InlineData("", false)]
    public void Okutma_girisi_denetlenir(string input, bool valid)
    {
        var result = ProductCode.ParseInput(input);
        Assert.Equal(valid, result.IsValid);
        if (!valid) Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Fact]
    public void Harf_iceren_giriste_klavye_uyarisi_verilir()
    {
        var result = ProductCode.ParseInput("2000ABC00011");
        Assert.Contains("klavye", result.Error);
    }
}
