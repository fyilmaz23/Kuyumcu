using Kuyumcu.Models;
using Kuyumcu.Stock;

namespace Kuyumcu.Labels;

/// <summary>Bir ürün etiketinin içeriği (fiyat basılmaz: altın fiyatı her gün değişir).</summary>
public sealed record LabelContent(string Code, int WeightMg, int Karat, string? CategoryShortName);

/// <summary>
/// Ayarlardan etiket belgesi üretir. Ürün etiketi, test etiketi ve hizalama etiketi.
/// X/Y kaydırma bütün öğelere uygulanır.
/// </summary>
public static class LabelLayout
{
    /// <summary>Test etiketinde basılan kod: hiçbir ürüne verilmeyen test kodu (bkz. <see cref="ProductCode.TestCode"/>).</summary>
    public static readonly string SampleCode = ProductCode.TestCode;

    /// <summary>Türkçe harflerin yazıcı fontunda nasıl çıktığını görmek için test etiketine basılan satır.</summary>
    public const string TurkishSample = "ÇĞİŞÖÜ";

    public static LabelDocument BuildItemLabel(LabelPrinterSettings s, LabelContent content, int copies = 1)
    {
        var elements = new List<LabelElement>();
        var t = s.TransliterateTurkish;

        AddCode(elements, s, content.Code);

        // 2. kanat: gram ve ayar + kısa kategori
        var line1 = $"{WeightFormat.Gram2(content.WeightMg)} gr";
        var line2 = $"{content.Karat}K";
        if (s.ShowCategory && !string.IsNullOrWhiteSpace(content.CategoryShortName))
            line2 += " " + LabelTextHelper.Prepare(content.CategoryShortName, t);
        AddTwoLines(elements, s, s.InfoXMm, s.InfoYMm, s.InfoFont, line1, line2);

        if (s.ShowBusinessName && !string.IsNullOrWhiteSpace(s.BusinessText))
            elements.Add(Text(s, s.BusinessXMm, s.BusinessYMm, s.BusinessFont, LabelTextHelper.Prepare(s.BusinessText, t)));

        return Document(s, elements, copies);
    }

    /// <summary>
    /// Test etiketi: örnek kodlu normal etiket. Kısa kategori yerine Türkçe harf örneği sadeleştirilmeden basılır;
    /// yazıcının yerleşik fontunda Türkçe harflerin doğru çıkıp çıkmadığı buradan görülür.
    /// </summary>
    public static LabelDocument BuildTestLabel(LabelPrinterSettings s)
    {
        var elements = new List<LabelElement>();
        AddCode(elements, s, SampleCode);
        AddTwoLines(elements, s, s.InfoXMm, s.InfoYMm, s.InfoFont, "12,34 gr", "22K " + TurkishSample);
        if (s.ShowBusinessName && !string.IsNullOrWhiteSpace(s.BusinessText))
            elements.Add(Text(s, s.BusinessXMm, s.BusinessYMm, s.BusinessFont, LabelTextHelper.Prepare(s.BusinessText, s.TransliterateTurkish)));
        return Document(s, elements, 1);
    }

    /// <summary>
    /// Hizalama etiketi: etiket kenarına çerçeve ve mm cetveli (her 5 mm numaralı, her 10 mm boydan boya çizgi).
    /// Kanatların ve kuyruğun nerede başlayıp bittiği bu etiketten okunur.
    /// </summary>
    public static LabelDocument BuildAlignmentLabel(LabelPrinterSettings s)
    {
        var elements = new List<LabelElement>();
        var dpi = s.Dpi;
        var ox = LabelUnits.ToDots(s.OffsetXMm, dpi);
        var oy = LabelUnits.ToDots(s.OffsetYMm, dpi);
        var w = LabelUnits.ToDots(s.WidthMm, dpi);
        var h = LabelUnits.ToDots(s.HeightMm, dpi);

        elements.Add(new LabelBox(Clamp(ox), Clamp(oy), Clamp(ox + w - 1), Clamp(oy + h - 1), 2));

        // Cetvel: her 1 mm'de kısa çentik, her 5 mm'de uzun çentik ve numara, her 10 mm'de boydan boya çizgi.
        // Kanat sınırları ve kuyruğun bittiği yer milimetre hassasiyetinde okunur.
        var tick1 = Math.Max(4, h / 8);
        var tick5 = Math.Max(8, h * 3 / 10);
        for (var mm = 1; mm < s.WidthMm; mm++)
        {
            var x = Clamp(ox + LabelUnits.ToDots(mm, dpi));
            if (mm % 10 == 0)
                elements.Add(new LabelBar(x, Clamp(oy), 2, h));
            else if (mm % 5 == 0)
                elements.Add(new LabelBar(x, Clamp(oy + h - tick5), 1, tick5));
            else
                elements.Add(new LabelBar(x, Clamp(oy + h - tick1), 1, tick1));

            if (mm % 5 == 0)
                elements.Add(new LabelText(Clamp(x + 3), Clamp(oy + 4), "1", mm.ToString()));
        }
        return Document(s, elements, 1);
    }

    private static void AddCode(List<LabelElement> elements, LabelPrinterSettings s, string code)
    {
        var x = Dots(s, s.CodeXMm, s.OffsetXMm);
        var y = Dots(s, s.CodeYMm, s.OffsetYMm);
        var module = Math.Clamp(s.CodeModuleDots, 1, 10);
        elements.Add(s.CodeType == LabelCodeType.QrCode
            ? new LabelQrCode(x, y, module, code)
            : new LabelDataMatrix(x, y, module, code));

        if (s.CodeTextTwoLines && code.Length > 6)
        {
            var half = (code.Length + 1) / 2;
            AddTwoLines(elements, s, s.CodeTextXMm, s.CodeTextYMm, s.CodeTextFont, code[..half], code[half..]);
        }
        else
        {
            elements.Add(Text(s, s.CodeTextXMm, s.CodeTextYMm, s.CodeTextFont, code));
        }
    }

    private static void AddTwoLines(List<LabelElement> elements, LabelPrinterSettings s, double xMm, double yMm, string font, string line1, string line2)
    {
        var (_, cellH) = LabelUnits.FontCellSize(font);
        var first = Text(s, xMm, yMm, font, line1);
        elements.Add(first);
        elements.Add(first with { Y = first.Y + cellH + 2, Text = line2 });
    }

    private static LabelText Text(LabelPrinterSettings s, double xMm, double yMm, string font, string text) =>
        new(Dots(s, xMm, s.OffsetXMm), Dots(s, yMm, s.OffsetYMm), NormalizeFont(font), text);

    private static int Dots(LabelPrinterSettings s, double mm, double offsetMm) => Clamp(LabelUnits.ToDots(mm + offsetMm, s.Dpi));

    private static int Clamp(int dots) => Math.Max(0, dots);

    private static string NormalizeFont(string? font) => font is "1" or "2" or "3" or "4" or "5" ? font : "2";

    private static LabelDocument Document(LabelPrinterSettings s, List<LabelElement> elements, int copies) =>
        new(s.WidthMm, s.HeightMm, s.GapMm, s.Dpi, s.Direction is 0 or 1 ? s.Direction : 1,
            Math.Clamp(s.Density, 0, 15), Math.Clamp(s.Speed, 1, 6), elements, Math.Max(1, copies));
}
