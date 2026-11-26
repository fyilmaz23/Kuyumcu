using System.Text;
using Kuyumcu.Models;

namespace Kuyumcu.Labels;

/// <summary>
/// Zebra yazıcıların ZPL II dili (GK420t, ZD220/ZD420, GC420t vb.; ZPL taklit eden başka markalar da).
/// Yazılar UTF-8 (^CI28) ile gider. Yazıcının ölçeklenebilir font 0'ı Türkçe harfleri her modelde basmayabilir:
/// test etiketinde "ÇĞİŞÖÜ" bozuk çıkarsa Ayarlar'da "Türkçe harfleri sadeleştir" açılır.
/// Etiket boşluğu (gap) yazıcının sensörüyle algılanır (^MNY); ayarlardaki boşluk değeri ZPL'de kullanılmaz.
/// </summary>
public sealed class ZplLanguage : ILabelLanguage
{
    public LabelPrinterLanguage Language => LabelPrinterLanguage.Zpl;
    public string DisplayName => "ZPL (Zebra)";

    /// <summary>Komut metni (test ve hata ayıklama için).</summary>
    public string RenderText(LabelDocument d)
    {
        var sb = new StringBuilder();
        void Line(string s) => sb.Append(s).Append("\r\n");

        // Koyuluk: TSPL ölçeği 0–15 → ZPL ~SD 00–30
        Line($"~SD{Math.Clamp(d.Density * 2, 0, 30):00}");
        Line("^XA");
        Line("^CI28");                              // UTF-8
        Line($"^PW{d.WidthDots}");
        Line($"^LL{d.HeightDots}");
        Line("^LH0,0");
        Line("^MNY");                               // boşluklu (gap) etiket
        Line($"^PR{d.Speed}");
        Line(d.Direction == 0 ? "^POI" : "^PON");   // TSPL DIRECTION 1 = normal, 0 = ters (180°)

        foreach (var e in d.Elements)
        {
            switch (e)
            {
                case LabelText t:
                    var (cw, ch) = LabelUnits.FontCellSize(t.Font);
                    Line($"^FO{t.X},{t.Y}^A0N,{ch},{cw}^FH_^FD{Escape(t.Text)}^FS");
                    break;
                case LabelDataMatrix m:
                    // ECC 200, satır/sütun sayısını yazıcı seçer (12 hane → 14×14)
                    Line($"^FO{m.X},{m.Y}^BXN,{m.ModuleDots},200^FH_^FD{Escape(m.Content)}^FS");
                    break;
                case LabelQrCode q:
                    // Model 2, büyütme = modül boyu (1–10); "LA," = düşük hata düzeltme, otomatik giriş (TSPL'deki L, A gibi)
                    Line($"^FO{q.X},{q.Y}^BQN,2,{Math.Clamp(q.ModuleDots, 1, 10)}^FH_^FDLA,{Escape(q.Content)}^FS");
                    break;
                case LabelCode128 c:
                    // İnsan okunur satır yok (N,N,N); rakamlar ayrı yazı olarak basılır.
                    // ">;" C alt kümesiyle başlatır (iki rakam bir karakter, TSPL'nin otomatik seçimiyle aynı genişlik),
                    // ">:" B alt kümesiyle; B'de ">" karakteri ">0" olarak yazılır.
                    var data = LabelUnits.IsCode128SetC(c.Content) ? ">;" + c.Content : ">:" + c.Content.Replace(">", ">0");
                    Line($"^FO{c.X},{c.Y}^BY{c.NarrowDots}^BCN,{c.HeightDots},N,N,N^FH_^FD{Escape(data)}^FS");
                    break;
                case LabelBox b:
                    Line($"^FO{b.X},{b.Y}^GB{b.X2 - b.X + 1},{b.Y2 - b.Y + 1},{b.Thickness}^FS");
                    break;
                case LabelBar bar:
                    // Kalınlık = kısa kenar → dolu dikdörtgen
                    Line($"^FO{bar.X},{bar.Y}^GB{bar.Width},{bar.Height},{Math.Min(bar.Width, bar.Height)}^FS");
                    break;
            }
        }

        Line($"^PQ{d.Copies}");
        Line("^XZ");
        return sb.ToString();
    }

    public byte[] Render(LabelDocument document) => new UTF8Encoding(false).GetBytes(RenderText(document));

    /// <summary>
    /// ^FH_ ile alan verisinde "_XX" onaltılık kaçış kullanılır: ZPL komut karakterleri (^ ~) ve kaçış karakterinin
    /// kendisi (_) bu şekilde yazılır, böylece yazı komutu bozamaz.
    /// </summary>
    private static string Escape(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            switch (c)
            {
                case '_': sb.Append("_5F"); break;
                case '^': sb.Append("_5E"); break;
                case '~': sb.Append("_7E"); break;
                default:
                    if (!char.IsControl(c))
                        sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }
}
