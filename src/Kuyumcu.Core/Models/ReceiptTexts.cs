namespace Kuyumcu.Models;

/// <summary>
/// Müşteri fişine basılan işletmeye özel metinler.
/// Başlık: ayarlardaki fiş başlığı, yoksa iş yeri adı. Alt yazı: ayarlardaki fiş alt yazısı, yoksa basılmaz.
/// </summary>
public sealed record ReceiptTexts(string Title, string? Footer)
{
    /// <summary>Alt yazı için en fazla satır (58 mm fişte uzun metin kağıdı boşa harcar).</summary>
    public const int MaxFooterLines = 3;

    public static ReceiptTexts From(AppSettings settings)
    {
        var title = Clean(settings.ReceiptTitle) ?? Clean(settings.BusinessName) ?? string.Empty;

        var footer = Clean(settings.ReceiptFooter);
        if (footer != null)
        {
            var lines = footer.Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .Take(MaxFooterLines);
            footer = string.Join("\n", lines);
            if (footer.Length == 0)
                footer = null;
        }

        return new ReceiptTexts(title, footer);
    }

    private static string? Clean(string? value)
    {
        var trimmed = value?.Replace("\r", string.Empty).Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
