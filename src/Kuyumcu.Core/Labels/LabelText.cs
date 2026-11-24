using System.Text;

namespace Kuyumcu.Labels;

/// <summary>Etiket yazısı hazırlama: Türkçe harf sadeleştirme ve yazıcı komutunu bozabilecek karakterleri ayıklama.</summary>
public static class LabelTextHelper
{
    /// <summary>Ş→S, ş→s, Ğ→G, ğ→g, İ→I, ı→i, Ç→C, ç→c, Ö→O, ö→o, Ü→U, ü→u.</summary>
    public static string Transliterate(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            sb.Append(c switch
            {
                'Ş' => 'S', 'ş' => 's',
                'Ğ' => 'G', 'ğ' => 'g',
                'İ' => 'I', 'ı' => 'i',
                'Ç' => 'C', 'ç' => 'c',
                'Ö' => 'O', 'ö' => 'o',
                'Ü' => 'U', 'ü' => 'u',
                _ => c
            });
        }
        return sb.ToString();
    }

    /// <summary>
    /// Etikete gidecek metni hazırlar: tırnak ve kontrol karakterlerini atar, gerekirse Türkçe harfleri sadeleştirir.
    /// </summary>
    public static string Prepare(string? text, bool transliterate)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var cleaned = new string(text.Where(c => c != '"' && !char.IsControl(c)).ToArray()).Trim();
        return transliterate ? Transliterate(cleaned) : cleaned;
    }
}
