using System.Globalization;

namespace Kuyumcu.Stock;

/// <summary>
/// Gram girişi ve gösterimi. Ağırlıklar tam sayı miligram (mg) tutulur.
/// Girişte virgül de nokta da ondalık ayıracı kabul edilir (gramda binlik ayıracı kullanılmaz), en çok 3 ondalık.
/// </summary>
public static class WeightFormat
{
    private static readonly CultureInfo Turkish = new("tr-TR");

    /// <summary>En büyük kabul edilen tek ürün ağırlığı (gram).</summary>
    public const int MaxGram = 10_000;

    /// <summary>"4,25" / "4.255" / "4,25 gr" → mg. Hatalıysa false ve açıklama.</summary>
    public static bool TryParseGram(string? input, out int milligrams, out string? error)
    {
        milligrams = 0;
        error = null;

        var text = (input ?? string.Empty).Trim().ToLowerInvariant();
        if (text.EndsWith("gr")) text = text[..^2].TrimEnd();
        else if (text.EndsWith("g")) text = text[..^1].TrimEnd();

        if (text.Length == 0)
        {
            error = "Gram girin.";
            return false;
        }

        var separators = text.Count(c => c == ',' || c == '.');
        if (separators > 1 || text.Any(c => !char.IsAsciiDigit(c) && c != ',' && c != '.'))
        {
            error = $"Geçersiz gram: \"{input?.Trim()}\". Örnek: 4,25";
            return false;
        }

        var parts = text.Replace('.', ',').Split(',');
        var whole = parts[0].Length == 0 ? "0" : parts[0];
        var fraction = parts.Length > 1 ? parts[1] : string.Empty;

        if (fraction.Length > 3)
        {
            error = "En çok 3 ondalık girilebilir.";
            return false;
        }

        if (!long.TryParse(whole, NumberStyles.None, CultureInfo.InvariantCulture, out var g) || g > MaxGram)
        {
            error = $"Gram {MaxGram:N0}'den küçük olmalı.";
            return false;
        }

        var mg = g * 1000 + (fraction.Length == 0 ? 0 : int.Parse(fraction.PadRight(3, '0'), CultureInfo.InvariantCulture));
        if (mg <= 0)
        {
            error = "Gram 0'dan büyük olmalı.";
            return false;
        }
        if (mg > MaxGram * 1000L)
        {
            error = $"Gram {MaxGram:N0}'den küçük olmalı.";
            return false;
        }

        milligrams = (int)mg;
        return true;
    }

    /// <summary>Taş gramı gibi isteğe bağlı alanlar: boş → 0.</summary>
    public static bool TryParseOptionalGram(string? input, out int milligrams, out string? error)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Trim() is "0" or "0,0" or "0,00" or "0.0" or "0.00")
        {
            milligrams = 0;
            error = null;
            return true;
        }
        return TryParseGram(input, out milligrams, out error);
    }

    /// <summary>mg → "4,25" (2 ondalık, etiket ve listeler).</summary>
    public static string Gram2(long milligrams) => (milligrams / 1000m).ToString("N2", Turkish);

    /// <summary>mg → "4,255" ya da "4,25" (3. ondalık varsa gösterilir; detay ekranı).</summary>
    public static string Gram(long milligrams) =>
        milligrams % 10 == 0 ? Gram2(milligrams) : (milligrams / 1000m).ToString("N3", Turkish);

    /// <summary>mg → düzenleme alanına yazılacak metin ("4,255").</summary>
    public static string ToInput(long milligrams) =>
        milligrams == 0 ? string.Empty : (milligrams / 1000m).ToString("0.###", Turkish);

    /// <summary>Binde milyem → "0,916".</summary>
    public static string Milyem(int perMille) => (perMille / 1000m).ToString("0.000", Turkish);

    /// <summary>"0,916" / "916" / "0.916" → 916. Boşsa null.</summary>
    public static bool TryParseMilyem(string? input, out int? perMille)
    {
        perMille = null;
        var text = (input ?? string.Empty).Trim().Replace('.', ',');
        if (text.Length == 0)
            return true;

        if (!decimal.TryParse(text, NumberStyles.Number, Turkish, out var value) || value < 0)
            return false;

        // 0,916 gibi kesirli ya da 916 gibi binde yazılmış olabilir
        var result = value < 2 ? (int)Math.Round(value * 1000) : (int)Math.Round(value);
        if (result > 1500)
            return false;
        perMille = result;
        return true;
    }
}
