using System.Text;
using Kuyumcu.Labels;
using Kuyumcu.Models;

namespace Kuyumcu.Tests;

public class ZplAndBarcodeTests
{
    private static readonly ZplLanguage Zpl = new();
    private static readonly TsplLanguage Tspl = new();
    private static readonly LabelContent Bilezik = new("200000000011", 10_255, 22, "BLZ");

    [Fact]
    public void Urun_etiketi_ZPL_ciktisi()
    {
        var zpl = Zpl.RenderText(LabelLayout.BuildItemLabel(new LabelPrinterSettings(), Bilezik));

        var expected = string.Join("\r\n",
            "~SD20",
            "^XA",
            "^CI28",
            "^PW575",
            "^LL80",
            "^LH0,0",
            "^MNY",
            "^PR3",
            "^PON",
            "^FO252,18^BXN,3,200^FH_^FD200000000011^FS",
            "^FO304,16^A0N,20,12^FH_^FD200000^FS",
            "^FO304,38^A0N,20,12^FH_^FD000011^FS",
            "^FO412,16^A0N,20,12^FH_^FD10,26 gr^FS",
            "^FO412,38^A0N,20,12^FH_^FD22K BLZ^FS",
            "^PQ1",
            "^XZ",
            "");
        Assert.Equal(expected, zpl);
    }

    [Fact]
    public void ZPL_QR_ters_yon_kopya_ve_koyuluk()
    {
        var s = new LabelPrinterSettings { CodeType = LabelCodeType.QrCode, Direction = 0, Density = 15, Speed = 2 };
        var zpl = Zpl.RenderText(LabelLayout.BuildItemLabel(s, Bilezik, copies: 3));

        Assert.StartsWith("~SD30\r\n^XA", zpl);
        Assert.Contains("^POI", zpl);
        Assert.Contains("^PR2", zpl);
        Assert.Contains("^FO252,18^BQN,2,3^FH_^FDLA,200000000011^FS", zpl);
        Assert.Contains("^PQ3\r\n^XZ", zpl);
        Assert.DoesNotContain("^BX", zpl);
    }

    [Fact]
    public void ZPL_komut_karakterleri_yaziyi_bozamaz()
    {
        var s = new LabelPrinterSettings { ShowBusinessName = true, BusinessText = "A^B~C_D" };
        var zpl = Zpl.RenderText(LabelLayout.BuildItemLabel(s, Bilezik));

        Assert.Contains("^FDA_5EB_7EC_5FD^FS", zpl);
        // ^XA ve ^XZ dışında yazıdan gelen komut yok
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(zpl, @"\^XA"));
    }

    [Fact]
    public void ZPL_Turkce_harfleri_UTF8_ile_gonderir()
    {
        var bytes = Zpl.Render(LabelLayout.BuildTestLabel(new LabelPrinterSettings()));
        var text = Encoding.UTF8.GetString(bytes);

        Assert.Contains("^CI28", text);
        Assert.Contains(LabelLayout.TurkishSample, text);
        Assert.NotEqual(0xEF, bytes[0]); // BOM yok
    }

    [Fact]
    public void ZPL_hizalama_etiketi_cerceve_ve_cizgiler()
    {
        var zpl = Zpl.RenderText(LabelLayout.BuildAlignmentLabel(new LabelPrinterSettings()));

        Assert.Contains("^FO0,0^GB575,80,2^FS", zpl);      // TSPL: BOX 0,0,574,79,2
        Assert.Contains("^FO80,0^GB2,80,2^FS", zpl);       // 10 mm çizgisi (dolu)
        Assert.Contains("^FO40,56^GB1,24,1^FS", zpl);      // 5 mm çentiği
        Assert.Contains("^FO83,4^A0N,12,8^FH_^FD10^FS", zpl);
    }

    [Fact]
    public void Dil_secimi_ZPL_doner()
    {
        Assert.IsType<ZplLanguage>(LabelLanguages.Get(LabelPrinterLanguage.Zpl));
        Assert.IsType<TsplLanguage>(LabelLanguages.Get(LabelPrinterLanguage.Tspl));
    }

    [Fact]
    public void Code128_modul_sayisi()
    {
        Assert.Equal(101, LabelUnits.Code128Modules("200000000011"));  // C alt kümesi: (1 + 6 + 1) × 11 + 13
        Assert.Equal(13 + 11 * (2 + 3), LabelUnits.Code128Modules("ABC"));
        Assert.Equal(13 + 11 * (2 + 3), LabelUnits.Code128Modules("123")); // tek sayıda rakam: B
        Assert.True(LabelUnits.IsCode128SetC("200000000011"));
        Assert.False(LabelUnits.IsCode128SetC("12345"));
        Assert.False(LabelUnits.IsCode128SetC(""));
    }

    [Fact]
    public void Barkod_secilince_yerlesim_kanada_sigar()
    {
        var s = new LabelPrinterSettings();
        s.ApplyCodeLayoutDefaults(LabelCodeType.Code128);
        var doc = LabelLayout.BuildItemLabel(s, Bilezik);

        var bc = doc.Elements.OfType<LabelCode128>().Single();
        Assert.Equal(1, bc.NarrowDots);
        Assert.Equal(36, bc.HeightDots);                      // 4,5 mm
        Assert.Equal(101, bc.WidthDots);
        // 1. kanat 30–50 mm (240–400 nokta): sessiz bölge (10 modül) dahil sığar
        Assert.True(bc.X - 10 >= LabelUnits.ToDots(30, 203));
        Assert.True(bc.X + bc.WidthDots + 10 <= LabelUnits.ToDots(50, 203));

        // Rakamlar barkodun altında tek satır, etiketten taşmaz
        var digits = doc.Elements.OfType<LabelText>().Single(t => t.Text == Bilezik.Code);
        var (cw, ch) = LabelUnits.FontCellSize(digits.Font);
        Assert.True(digits.Y > bc.Y + bc.HeightDots);
        Assert.True(digits.Y + ch <= doc.HeightDots);
        Assert.True(digits.X + cw * digits.Text.Length <= LabelUnits.ToDots(50, 203));
        Assert.Empty(doc.Elements.OfType<LabelDataMatrix>());
    }

    [Fact]
    public void Barkod_TSPL_ve_ZPL_komutlari()
    {
        var s = new LabelPrinterSettings();
        s.ApplyCodeLayoutDefaults(LabelCodeType.Code128);
        var doc = LabelLayout.BuildItemLabel(s, Bilezik);

        Assert.Contains("BARCODE 269,8,\"128\",36,0,0,1,1,\"200000000011\"", Tspl.RenderText(doc));
        Assert.Contains("^FO269,8^BY1^BCN,36,N,N,N^FH_^FD>;200000000011^FS", Zpl.RenderText(doc));
    }

    [Fact]
    public void Barkod_eski_satirda_sifir_degerler_varsayilan_sayilir()
    {
        var s = new LabelPrinterSettings { CodeType = LabelCodeType.Code128, BarcodeNarrowDots = 0, BarcodeHeightMm = 0 };
        var bc = LabelLayout.BuildItemLabel(s, Bilezik).Elements.OfType<LabelCode128>().Single();
        Assert.Equal(LabelPrinterSettings.DefaultBarcodeNarrowDots, bc.NarrowDots);
        Assert.Equal(36, bc.HeightDots);

        s.BarcodeNarrowDots = 9;
        Assert.Equal(4, LabelLayout.BuildItemLabel(s, Bilezik).Elements.OfType<LabelCode128>().Single().NarrowDots);
    }

    [Fact]
    public void Kod_tipi_2D_ye_donunce_yerlesim_geri_gelir()
    {
        var s = new LabelPrinterSettings { OffsetXMm = 1.5 };
        s.ApplyCodeLayoutDefaults(LabelCodeType.Code128);
        s.ApplyCodeLayoutDefaults(LabelCodeType.DataMatrix);

        var d = new LabelPrinterSettings();
        Assert.Equal(LabelCodeType.DataMatrix, s.CodeType);
        Assert.Equal(d.CodeXMm, s.CodeXMm);
        Assert.Equal(d.CodeTextXMm, s.CodeTextXMm);
        Assert.Equal(d.CodeTextFont, s.CodeTextFont);
        Assert.True(s.CodeTextTwoLines);
        Assert.Equal(1.5, s.OffsetXMm); // kaydırma korunur
    }
}
