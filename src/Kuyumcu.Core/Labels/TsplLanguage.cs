using System.Globalization;
using System.Text;
using Kuyumcu.Models;

namespace Kuyumcu.Labels;

/// <summary>
/// Yazıcı komut dili. Etiket belgesini yazıcıya gönderilecek ham baytlara çevirir.
/// Yeni bir dil (ör. ZPL) eklemek için bu arayüz uygulanıp <see cref="LabelLanguages.Get"/>'e eklenir.
/// </summary>
public interface ILabelLanguage
{
    LabelPrinterLanguage Language { get; }
    string DisplayName { get; }
    byte[] Render(LabelDocument document);
}

public static class LabelLanguages
{
    public static ILabelLanguage Get(LabelPrinterLanguage language) => language switch
    {
        LabelPrinterLanguage.Tspl => new TsplLanguage(),
        _ => throw new NotSupportedException($"Yazıcı dili desteklenmiyor: {language}")
    };
}

/// <summary>
/// TSC yazıcıların TSPL/TSPL2 dili (TE210 vb.). Yazılar Windows-1254 (Türkçe) kod sayfasıyla gönderilir;
/// yerleşik fontların Türkçe harfleri basıp basmadığı test etiketiyle kontrol edilmelidir.
/// </summary>
public sealed class TsplLanguage : ILabelLanguage
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    static TsplLanguage()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public LabelPrinterLanguage Language => LabelPrinterLanguage.Tspl;
    public string DisplayName => "TSPL (TSC)";

    /// <summary>Komut metni (test ve hata ayıklama için).</summary>
    public string RenderText(LabelDocument d)
    {
        var sb = new StringBuilder();
        void Line(string s) => sb.Append(s).Append("\r\n");

        Line($"SIZE {Mm(d.WidthMm)} mm,{Mm(d.HeightMm)} mm");
        Line($"GAP {Mm(d.GapMm)} mm,0 mm");
        Line($"DIRECTION {d.Direction},0");
        Line("REFERENCE 0,0");
        Line($"SPEED {d.Speed}");
        Line($"DENSITY {d.Density}");
        Line("CODEPAGE 1254");
        Line("CLS");

        foreach (var e in d.Elements)
        {
            switch (e)
            {
                case LabelText t:
                    Line($"TEXT {t.X},{t.Y},\"{t.Font}\",0,1,1,\"{Escape(t.Text)}\"");
                    break;
                case LabelDataMatrix m:
                    // Alan genişliği: 12 haneli kod 14×14 modül; biraz pay bırakılır
                    var area = m.ModuleDots * 18;
                    Line($"DMATRIX {m.X},{m.Y},{area},{area},x{m.ModuleDots},\"{Escape(m.Content)}\"");
                    break;
                case LabelQrCode q:
                    Line($"QRCODE {q.X},{q.Y},L,{q.ModuleDots},A,0,\"{Escape(q.Content)}\"");
                    break;
                case LabelBox b:
                    Line($"BOX {b.X},{b.Y},{b.X2},{b.Y2},{b.Thickness}");
                    break;
                case LabelBar bar:
                    Line($"BAR {bar.X},{bar.Y},{bar.Width},{bar.Height}");
                    break;
            }
        }

        Line($"PRINT {d.Copies},1");
        return sb.ToString();
    }

    public byte[] Render(LabelDocument document) => Encoding.GetEncoding(1254).GetBytes(RenderText(document));

    private static string Mm(double value) => value.ToString("0.##", Inv);

    /// <summary>TSPL'de tırnak \["] ile yazılır; yazıyı hazırlarken zaten ayıklanıyor, yine de güvenlik için.</summary>
    private static string Escape(string text) => text.Replace("\"", "\\[\"]");
}
