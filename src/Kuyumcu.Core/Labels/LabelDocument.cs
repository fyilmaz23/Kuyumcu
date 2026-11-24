namespace Kuyumcu.Labels;

// Yazıcı dilinden bağımsız etiket modeli. Konumlar yazıcı noktası (dot) cinsindendir.
// LabelLayout bunu ayarlardan üretir; ILabelLanguage (TSPL, ileride ZPL) yazıcı komutuna çevirir.

/// <summary>Bir etiket baskı işi.</summary>
public sealed record LabelDocument(
    double WidthMm,
    double HeightMm,
    double GapMm,
    int Dpi,
    int Direction,
    int Density,
    int Speed,
    IReadOnlyList<LabelElement> Elements,
    int Copies = 1)
{
    public int WidthDots => LabelUnits.ToDots(WidthMm, Dpi);
    public int HeightDots => LabelUnits.ToDots(HeightMm, Dpi);
}

public abstract record LabelElement(int X, int Y);

/// <summary>Yazıcının yerleşik fontuyla yazı. Font: TSPL font numarası ("1"–"5").</summary>
public sealed record LabelText(int X, int Y, string Font, string Text) : LabelElement(X, Y);

/// <summary>DataMatrix 2D kod. ModuleDots: bir karenin kenarı (nokta).</summary>
public sealed record LabelDataMatrix(int X, int Y, int ModuleDots, string Content) : LabelElement(X, Y);

/// <summary>QR kod.</summary>
public sealed record LabelQrCode(int X, int Y, int ModuleDots, string Content) : LabelElement(X, Y);

/// <summary>Çerçeve.</summary>
public sealed record LabelBox(int X, int Y, int X2, int Y2, int Thickness) : LabelElement(X, Y);

/// <summary>Dolu dikdörtgen / çizgi.</summary>
public sealed record LabelBar(int X, int Y, int Width, int Height) : LabelElement(X, Y);

public static class LabelUnits
{
    /// <summary>Milimetreyi yazıcı noktasına çevirir (203 dpi'de 1 mm ≈ 8 nokta).</summary>
    public static int ToDots(double mm, int dpi) => (int)Math.Round(mm * dpi / 25.4, MidpointRounding.AwayFromZero);

    public static double ToMm(int dots, int dpi) => dots * 25.4 / dpi;

    /// <summary>
    /// TSC yerleşik fontlarının karakter boyutu (nokta, 203 dpi): "1" 8×12, "2" 12×20, "3" 16×24, "4" 24×32, "5" 32×48.
    /// Önizleme ve yerleşim hesabı için.
    /// </summary>
    public static (int Width, int Height) FontCellSize(string font) => font switch
    {
        "1" => (8, 12),
        "2" => (12, 20),
        "3" => (16, 24),
        "4" => (24, 32),
        "5" => (32, 48),
        _ => (12, 20)
    };

    /// <summary>12 haneli kodun DataMatrix sembolü 14×14 modül; QR sürüm 1 ise 21×21 modül.</summary>
    public static int CodeSymbolModules(bool qr) => qr ? 21 : 14;
}
