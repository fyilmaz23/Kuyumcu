using Kuyumcu.Models;
using Kuyumcu.Stock;

namespace Kuyumcu.Tests;

public class BulkSaleTests
{
    private static BulkProduct Piece(string name, int pieceMg, int karat = 22) =>
        new() { Name = name, Unit = BulkUnit.Piece, Karat = karat, PieceWeightMg = pieceMg };

    private static BulkProduct Gram(string name, int karat) => new() { Name = name, Unit = BulkUnit.Gram, Karat = karat };

    private static readonly BulkProduct Ceyrek = Piece("Çeyrek", 1754);
    private static readonly BulkProduct Yarim = Piece("Yarım", 3508);
    private static readonly BulkProduct Tam = Piece("Tam", 7016);
    private static readonly BulkProduct Ata = Piece("Ata Lira", 7216);
    private static readonly BulkProduct Gram24 = Gram("24 Ayar Gram Altın", 24);

    // elazigaltinfiyatlari.com satır adları (GoldPriceSourceTests ile aynı)
    private static readonly List<GoldPriceDto> Prices = new()
    {
        new("24 Ayar", 5_900m, 6_000m),
        new("22 Ayar", 5_400m, 5_500m),
        new("Beşli", 200_000m, 210_000m),
        new("Tam (Ata)", 40_700m, 42_200m),
        new("Yarım", 19_900m, 20_900m),
        new("Çeyrek", 9_950m, 10_450m),
        new("Gram Altın", 5_950m, 6_050m),
    };

    [Fact]
    public void Kendi_cinsi_ada_ve_ayara_gore_bulunur()
    {
        Assert.Equal(CurrencyType.QuarterGold, BulkSale.OwnCurrency(Ceyrek));
        Assert.Equal(CurrencyType.HalfGold, BulkSale.OwnCurrency(Yarim));
        Assert.Equal(CurrencyType.FullGold, BulkSale.OwnCurrency(Tam));
        Assert.Equal(CurrencyType.QuarterGold, BulkSale.OwnCurrency(Piece("ÇEYREK ALTIN", 1754)));
        Assert.Null(BulkSale.OwnCurrency(Ata));                     // defterde Ata cinsi yok
        Assert.Null(BulkSale.OwnCurrency(Piece("Tamamlayıcı", 1000))); // "tam" kelime olarak aranır
        Assert.Equal(CurrencyType.Gold24K, BulkSale.OwnCurrency(Gram24));
        Assert.Equal(CurrencyType.Gold22K, BulkSale.OwnCurrency(Gram("22 ayar hurda", 22)));
        Assert.Null(BulkSale.OwnCurrency(Gram("18 ayar", 18)));
    }

    [Fact]
    public void Secilebilecek_cinsler_kendi_cinsi_once()
    {
        Assert.Equal(new[] { CurrencyType.QuarterGold, CurrencyType.TurkishLira, CurrencyType.Gold22K }, BulkSale.CreditCurrencies(Ceyrek));
        Assert.Equal(new[] { CurrencyType.TurkishLira, CurrencyType.Gold22K }, BulkSale.CreditCurrencies(Ata));
        // 22 ayar gramla tutulan ürünün kendi cinsi zaten 22 ayar: iki kez listelenmez
        Assert.Equal(new[] { CurrencyType.Gold22K, CurrencyType.TurkishLira }, BulkSale.CreditCurrencies(Gram("22 ayar hurda", 22)));
    }

    [Fact]
    public void Kendi_cinsinde_borc_satilan_miktardir()
    {
        Assert.Equal(3m, BulkSale.SuggestCredit(Ceyrek, 3, CurrencyType.QuarterGold, 916, 916, null));
        Assert.Equal(12.35m, BulkSale.SuggestCredit(Gram24, 12_345, CurrencyType.Gold24K, 995, 916, null)); // mg → gr, 2 ondalık
    }

    [Fact]
    public void Yirmi_iki_ayar_karsiligi_agirlik_x_milyem()
    {
        // 2 çeyrek = 3,508 gr, 22 ayar → 3,51
        Assert.Equal(3.51m, BulkSale.SuggestCredit(Ceyrek, 2, CurrencyType.Gold22K, 916, 916, null));
        // 10 gr 24 ayar (995) → 10 × 995 / 916 = 10,86
        Assert.Equal(10.86m, BulkSale.SuggestCredit(Gram24, 10_000, CurrencyType.Gold22K, 995, 916, null));
    }

    [Fact]
    public void TL_onerisi_fiyat_listesindeki_satirdan()
    {
        Assert.Equal(20_900m, BulkSale.SuggestPrice(Ceyrek, 2, 916, Prices));        // 2 × 10.450
        Assert.Equal(42_200m, BulkSale.SuggestPrice(Tam, 1, 916, Prices));           // "Tam (Ata)"
        Assert.Equal(42_200m, BulkSale.SuggestPrice(Ata, 1, 916, Prices));           // Ata Lira → "Tam (Ata)"
        Assert.Equal(15_130m, BulkSale.SuggestPrice(Gram24, 2_500, 995, Prices));    // 2,5 × 6.050 = 15.125 → 10 TL'ye
        Assert.Equal(20_900m, BulkSale.SuggestCredit(Ceyrek, 2, CurrencyType.TurkishLira, 916, 916, Prices));
    }

    [Fact]
    public void TL_onerisi_satir_yoksa_has_uzerinden_fiyat_yoksa_sifir()
    {
        var bilinmeyen = Piece("Reşat", 7216);
        // has = 7,216 × 0,916 = 6,610 gr × 6.000 = 39.660
        Assert.Equal(39_660m, BulkSale.SuggestPrice(bilinmeyen, 1, 916, Prices));
        Assert.Equal(0m, BulkSale.SuggestPrice(Ceyrek, 1, 916, null));
        Assert.Equal(0m, BulkSale.SuggestPrice(Ceyrek, 0, 916, Prices));
    }

    [Fact]
    public void Defter_aciklamasi()
    {
        Assert.Equal("Kasa satışı: 2 adet Çeyrek", BulkSale.Description(Ceyrek, 2, null));
        Assert.Equal("Kasa satışı: 12,345 gr 24 Ayar Gram Altın - düğün", BulkSale.Description(Gram24, 12_345, " düğün "));
    }
}
