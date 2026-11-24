using System.Text;
using Kuyumcu.Labels;
using Kuyumcu.Models;

namespace Kuyumcu.Tests;

public class LabelTests
{
    private static readonly TsplLanguage Tspl = new();
    private static readonly LabelContent Bilezik = new("200000000011", 10_255, 22, "BLZ");

    [Fact]
    public void Milimetre_203_dpi_noktaya_cevrilir()
    {
        Assert.Equal(8, LabelUnits.ToDots(1, 203));
        Assert.Equal(575, LabelUnits.ToDots(72, 203));
        Assert.Equal(80, LabelUnits.ToDots(10, 203));
        Assert.Equal(24, LabelUnits.ToDots(3, 203));
    }

    [Fact]
    public void Urun_etiketi_TSPL_ciktisi()
    {
        var doc = LabelLayout.BuildItemLabel(new LabelPrinterSettings(), Bilezik);
        var tspl = Tspl.RenderText(doc);

        var expected = string.Join("\r\n",
            "SIZE 72 mm,10 mm",
            "GAP 3 mm,0 mm",
            "DIRECTION 1,0",
            "REFERENCE 0,0",
            "SPEED 3",
            "DENSITY 10",
            "CODEPAGE 1254",
            "CLS",
            "DMATRIX 16,18,54,54,x3,\"200000000011\"",
            "TEXT 68,16,\"2\",0,1,1,\"200000\"",
            "TEXT 68,38,\"2\",0,1,1,\"000011\"",
            "TEXT 200,16,\"2\",0,1,1,\"10,26 gr\"",
            "TEXT 200,38,\"2\",0,1,1,\"22K BLZ\"",
            "PRINT 1,1",
            "");
        Assert.Equal(expected, tspl);
    }

    [Fact]
    public void Etikette_fiyat_yok_gram_iki_ondalik()
    {
        var tspl = Tspl.RenderText(LabelLayout.BuildItemLabel(new LabelPrinterSettings(), new LabelContent("200000000011", 4_250, 14, null)));
        Assert.Contains("\"4,25 gr\"", tspl);
        Assert.Contains("\"14K\"", tspl);
        Assert.DoesNotContain("TL", tspl);
        Assert.DoesNotContain("₺", tspl);
    }

    [Fact]
    public void QR_secilince_QRCODE_basilir_ve_kaydirma_uygulanir()
    {
        var s = new LabelPrinterSettings { CodeType = LabelCodeType.QrCode, OffsetXMm = 1, OffsetYMm = -0.5, CodeTextTwoLines = false };
        var tspl = Tspl.RenderText(LabelLayout.BuildItemLabel(s, Bilezik));

        Assert.Contains("QRCODE 24,14,L,3,A,0,\"200000000011\"", tspl);   // (2+1) mm, (2,2−0,5) mm
        Assert.Contains("TEXT 76,12,\"2\",0,1,1,\"200000000011\"", tspl);
        Assert.DoesNotContain("DMATRIX", tspl);
    }

    [Fact]
    public void Kategori_kapatilinca_basilmaz_dukkan_adi_istege_bagli()
    {
        var s = new LabelPrinterSettings { ShowCategory = false, ShowBusinessName = true, BusinessText = "Çarşı" };
        var tspl = Tspl.RenderText(LabelLayout.BuildItemLabel(s, Bilezik));
        Assert.Contains("\"22K\"", tspl);
        Assert.DoesNotContain("BLZ", tspl);
        Assert.Contains("\"Carsi\"", tspl); // Türkçe sadeleştirme varsayılan açık
    }

    [Fact]
    public void Turkce_sadelestirme_ve_tirnak_ayiklama()
    {
        Assert.Equal("SGICOU sgicou", LabelTextHelper.Transliterate("ŞĞİÇÖÜ şğıçöü"));
        Assert.Equal("KUNYE", LabelTextHelper.Prepare("KÜNYE", true));
        Assert.Equal("KÜNYE", LabelTextHelper.Prepare("KÜNYE", false));
        Assert.Equal("ab", LabelTextHelper.Prepare("a\"b", false));
    }

    [Fact]
    public void Test_etiketi_Turkce_harfleri_1254_ile_gonderir()
    {
        var bytes = Tspl.Render(LabelLayout.BuildTestLabel(new LabelPrinterSettings()));
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var text = Encoding.GetEncoding(1254).GetString(bytes);

        Assert.Contains(LabelLayout.TurkishSample, text);
        Assert.Contains((byte)0xDE, bytes); // Ş (Windows-1254)
        Assert.Contains((byte)0xFD, bytes); // ı
    }

    [Fact]
    public void Hizalama_etiketi_cerceve_ve_10_mm_cizgileri()
    {
        var doc = LabelLayout.BuildAlignmentLabel(new LabelPrinterSettings());
        var tspl = Tspl.RenderText(doc);

        Assert.Contains("BOX 0,0,574,79,2", tspl);
        // 10, 20, …, 70 mm'de uzun çizgi
        var longBars = doc.Elements.OfType<LabelBar>().Where(b => b.Height == 80).Select(b => b.X).ToList();
        Assert.Equal(new[] { 80, 160, 240, 320, 400, 480, 559 }, longBars);
        Assert.Contains("TEXT 83,4,\"1\",0,1,1,\"10\"", tspl);
        Assert.Contains("\"70\"", tspl);

        // Cetvel: 1..71 mm her milimetrede bir çentik, her 5 mm'de numara
        Assert.Equal(71, doc.Elements.OfType<LabelBar>().Count());
        Assert.Contains("TEXT 43,4,\"1\",0,1,1,\"5\"", tspl);
        Assert.Contains("BAR 40,56,1,24", tspl);   // 5 mm çentiği
        Assert.Contains("BAR 8,70,1,10", tspl);    // 1 mm çentiği
        Assert.Equal(14, doc.Elements.OfType<LabelText>().Count()); // 5, 10, …, 70
    }

    [Fact]
    public void Ayar_degerleri_sinirlanir()
    {
        var s = new LabelPrinterSettings { Density = 40, Speed = 0, Direction = 7, CodeModuleDots = 50, CodeTextFont = "9" };
        var doc = LabelLayout.BuildItemLabel(s, Bilezik, copies: 0);
        Assert.Equal(15, doc.Density);
        Assert.Equal(1, doc.Speed);
        Assert.Equal(1, doc.Direction);
        Assert.Equal(1, doc.Copies);
        Assert.Equal(10, doc.Elements.OfType<LabelDataMatrix>().Single().ModuleDots);
        Assert.All(doc.Elements.OfType<LabelText>(), t => Assert.Equal("2", t.Font));
    }
}
