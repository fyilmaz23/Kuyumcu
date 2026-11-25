using Kuyumcu.Models;

namespace Kuyumcu.Tests;

public class ReceiptTextsTests
{
    [Fact]
    public void Baslik_bossa_is_yeri_adi_basilir_alt_yazi_basilmaz()
    {
        var texts = ReceiptTexts.From(new AppSettings { BusinessName = "Çarşı Kuyumculuk", ReceiptTitle = "  ", ReceiptFooter = null });

        Assert.Equal("Çarşı Kuyumculuk", texts.Title);
        Assert.Null(texts.Footer);
    }

    [Fact]
    public void Ayarlardaki_baslik_ve_alt_yazi_kullanilir()
    {
        var texts = ReceiptTexts.From(new AppSettings
        {
            BusinessName = "Çarşı Kuyumculuk",
            ReceiptTitle = " KUYUMCU MEHMET ",
            ReceiptFooter = "@kuyumcumehmetelazig",
        });

        Assert.Equal("KUYUMCU MEHMET", texts.Title);
        Assert.Equal("@kuyumcumehmetelazig", texts.Footer);
    }

    [Fact]
    public void Alt_yazida_bos_satirlar_atilir_ve_en_fazla_uc_satir_kalir()
    {
        var texts = ReceiptTexts.From(new AppSettings
        {
            BusinessName = "X",
            ReceiptFooter = "\r\n  @kuyumcu \r\n\r\n0555 555 55 55\nÇarşı Cad. 1\nfazla satır\n",
        });

        Assert.Equal("@kuyumcu\n0555 555 55 55\nÇarşı Cad. 1", texts.Footer);
    }

    [Fact]
    public void Sadece_bosluk_olan_alt_yazi_basilmaz()
    {
        Assert.Null(ReceiptTexts.From(new AppSettings { BusinessName = "X", ReceiptFooter = " \n \r\n " }).Footer);
    }
}
