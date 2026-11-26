using SQLite;

namespace Kuyumcu.Models;

/// <summary>Etiket yazıcısına nasıl ulaşılır.</summary>
public enum LabelPrinterConnection
{
    /// <summary>Windows'ta kurulu yazıcı (USB), ham komut (RAW) olarak gönderilir.</summary>
    WindowsPrinter = 0,
    /// <summary>Ethernet: TCP (genelde 9100 portu).</summary>
    Network = 1
}

/// <summary>Yazıcı komut dili (bkz. ILabelLanguage).</summary>
public enum LabelPrinterLanguage
{
    /// <summary>TSC yazıcılar (TE210 vb.).</summary>
    Tspl = 0,
    /// <summary>Zebra yazıcılar (ZPL II) ve ZPL taklit eden yazıcılar.</summary>
    Zpl = 1
}

/// <summary>Etiketteki kod türü.</summary>
public enum LabelCodeType
{
    DataMatrix = 0,
    QrCode = 1,
    /// <summary>Çizgi barkod (Code 128). Sadece 1D okuyan (lazer) okuyucular için; 2D koddan çok daha geniştir.</summary>
    Code128 = 2
}

/// <summary>
/// Etiket yazıcısı ve etiket yerleşimi ayarları (tek satır). Cihaza özeldir: yedek geri yüklenince
/// bu bilgisayarın ayarı korunur. Ölçüler milimetre; varsayılanlar 72 × 10 mm kelebek kuyumcu etiketi içindir.
/// Yazıcı etiketi kuyruk solda besler (X = 0 kuyruğun ucu). TSC TE210 + 72 × 10 mm kuyruklu etikette ölçülen:
/// kuyruk 0–30 mm, 1. kanat 30–50 mm (2D kod + kod rakamları), 2. kanat 50–70 mm (gram, ayar, kısa kategori).
/// </summary>
[Table("LabelPrinterSettings")]
public class LabelPrinterSettings
{
    [PrimaryKey]
    public int Id { get; set; } = 1;

    // Bağlantı
    public LabelPrinterConnection Connection { get; set; } = LabelPrinterConnection.WindowsPrinter;
    public string? PrinterName { get; set; }
    public string? NetworkHost { get; set; }
    public int NetworkPort { get; set; } = 9100;
    public LabelPrinterLanguage Language { get; set; } = LabelPrinterLanguage.Tspl;

    /// <summary>Yazıcı çözünürlüğü (TE210: 203 dpi → 1 mm = 8 nokta).</summary>
    public int Dpi { get; set; } = 203;

    // Etiket
    public double WidthMm { get; set; } = 72;
    public double HeightMm { get; set; } = 10;
    /// <summary>Etiketler arası boşluk.</summary>
    public double GapMm { get; set; } = 3;

    /// <summary>Tüm içeriği sağa (+) / sola (−) kaydırır.</summary>
    public double OffsetXMm { get; set; }
    /// <summary>Tüm içeriği aşağı (+) / yukarı (−) kaydırır.</summary>
    public double OffsetYMm { get; set; }

    /// <summary>Baskı yönü (TSPL DIRECTION 0/1). Etiket ters basılıyorsa değiştirilir.</summary>
    public int Direction { get; set; } = 1;
    /// <summary>Koyuluk 0–15 (resin ribonda 8–12 iyi sonuç verir).</summary>
    public int Density { get; set; } = 10;
    /// <summary>Hız (inç/sn). Küçük etikette 2–3 önerilir.</summary>
    public int Speed { get; set; } = 3;

    // Kod (1. kanat)
    public LabelCodeType CodeType { get; set; } = LabelCodeType.DataMatrix;
    /// <summary>2D kod modül (kare) boyu, nokta. 3 nokta ≈ 0,375 mm.</summary>
    public int CodeModuleDots { get; set; } = 3;
    public double CodeXMm { get; set; } = 31.5;
    public double CodeYMm { get; set; } = 2.2;

    // Çizgi barkod (Code 128) — sadece CodeType = Code128 iken kullanılır.
    // Bu kolonlardan önceki satırlarda değer 0 gelir; 0 "varsayılan" sayılır (bkz. Effective*).
    /// <summary>En ince çizginin genişliği, nokta (1–4). 203 dpi'de 1 nokta ≈ 0,125 mm.</summary>
    public int BarcodeNarrowDots { get; set; } = DefaultBarcodeNarrowDots;
    /// <summary>Çizgi yüksekliği.</summary>
    public double BarcodeHeightMm { get; set; } = DefaultBarcodeHeightMm;

    public const int DefaultBarcodeNarrowDots = 1;
    public const double DefaultBarcodeHeightMm = 4.5;

    [Ignore]
    public int EffectiveBarcodeNarrowDots => BarcodeNarrowDots <= 0 ? DefaultBarcodeNarrowDots : Math.Min(BarcodeNarrowDots, 4);
    [Ignore]
    public double EffectiveBarcodeHeightMm => BarcodeHeightMm <= 0 ? DefaultBarcodeHeightMm : BarcodeHeightMm;

    // Kod rakamları (1. kanat, kodun sağında)
    public double CodeTextXMm { get; set; } = 38;
    public double CodeTextYMm { get; set; } = 2;
    /// <summary>Yazıcının yerleşik font numarası (TSPL "1"–"5").</summary>
    public string CodeTextFont { get; set; } = "2";
    /// <summary>12 haneyi 6+6 iki satıra böl (dar kanatta okunaklı olur).</summary>
    public bool CodeTextTwoLines { get; set; } = true;

    // Bilgi yazısı (2. kanat): gram, ayar, kategori
    public double InfoXMm { get; set; } = 51.5;
    public double InfoYMm { get; set; } = 2;
    public string InfoFont { get; set; } = "2";
    public bool ShowCategory { get; set; } = true;

    // İsteğe bağlı dükkân adı (varsayılan kapalı; 72 × 10 etikette yer dar)
    public bool ShowBusinessName { get; set; }
    public string? BusinessText { get; set; }
    public double BusinessXMm { get; set; } = 51.5;
    public double BusinessYMm { get; set; } = 7.5;
    public string BusinessFont { get; set; } = "1";

    /// <summary>
    /// Türkçe harfleri sadeleştir (Ş→S, Ğ→G, İ→I…). TE210 yerleşik fontu CODEPAGE 1254 ile Türkçe harfleri düzgün
    /// basıyor (denendi); başka bir yazıcıda bozuk çıkarsa açılır.
    /// </summary>
    public bool TransliterateTurkish { get; set; }

    /// <summary>Gönderim için gereken bilgiler girilmiş mi?</summary>
    [Ignore]
    public bool IsConfigured => Connection == LabelPrinterConnection.Network
        ? !string.IsNullOrWhiteSpace(NetworkHost) && NetworkPort > 0
        : !string.IsNullOrWhiteSpace(PrinterName);

    public LabelPrinterSettings Clone() => (LabelPrinterSettings)MemberwiseClone();

    /// <summary>
    /// Kod tipini değiştirir ve 1. kanattaki kod/rakam konumlarını o tipe uygun varsayılanlara getirir.
    /// 2D kod (≈ 5 mm kare): rakamlar sağında 6+6. Çizgi barkod (≈ 13 mm): kanadın ortasında, rakamlar altında tek satır.
    /// Ölçüler 72 × 10 mm kelebek etiket içindir (1. kanat 30–50 mm). Kaydırma ve diğer alanlar değişmez.
    /// </summary>
    public void ApplyCodeLayoutDefaults(LabelCodeType type)
    {
        var d = new LabelPrinterSettings();
        CodeType = type;
        if (type == LabelCodeType.Code128)
        {
            // 12 hane Code 128 (C alt kümesi) = 101 modül → 1 noktada 12,6 mm; kanat ortasına (30 + (20 − 12,6) / 2)
            CodeXMm = 33.7;
            CodeYMm = 1;
            BarcodeNarrowDots = DefaultBarcodeNarrowDots;
            BarcodeHeightMm = DefaultBarcodeHeightMm;
            CodeTextXMm = 34;
            CodeTextYMm = 6.2;
            CodeTextFont = "1";
            CodeTextTwoLines = false;
        }
        else
        {
            CodeXMm = d.CodeXMm;
            CodeYMm = d.CodeYMm;
            CodeTextXMm = d.CodeTextXMm;
            CodeTextYMm = d.CodeTextYMm;
            CodeTextFont = d.CodeTextFont;
            CodeTextTwoLines = d.CodeTextTwoLines;
        }
    }
}
