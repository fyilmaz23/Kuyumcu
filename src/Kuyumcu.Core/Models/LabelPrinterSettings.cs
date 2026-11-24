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

/// <summary>Yazıcı komut dili. Şimdilik TSPL (TSC); ZPL ileride eklenebilir (bkz. ILabelLanguage).</summary>
public enum LabelPrinterLanguage
{
    Tspl = 0
}

/// <summary>Etiketteki 2D kod türü.</summary>
public enum LabelCodeType
{
    DataMatrix = 0,
    QrCode = 1
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

    // 2D kod (1. kanat)
    public LabelCodeType CodeType { get; set; } = LabelCodeType.DataMatrix;
    /// <summary>Kod modül (kare) boyu, nokta. 3 nokta ≈ 0,375 mm.</summary>
    public int CodeModuleDots { get; set; } = 3;
    public double CodeXMm { get; set; } = 31.5;
    public double CodeYMm { get; set; } = 2.2;

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
}
