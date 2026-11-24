namespace Kuyumcu.Stock;

/// <summary>
/// Mağaza içi ürün kodu: 12 hane, sadece rakam.
/// Yapı: "2" (mağaza içi önek) + 10 haneli sıra numarası + 1 kontrol hanesi (GS1 mod 10).
/// Kodun içinde gram veya fiyat bilgisi yoktur; kod sadece kayda giden bir anahtardır.
/// Kontrol hanesi, kodun elle yazılmasında yapılan tek hane hatalarını ve çoğu yer değiştirmeyi yakalar.
/// </summary>
public static class ProductCode
{
    public const char Prefix = '2';
    public const int Length = 12;
    public const long MaxSequence = 9_999_999_999;

    /// <summary>Sıra numarasından kod üretir. 1 → "200000000011".</summary>
    public static string Format(long sequence)
    {
        if (sequence < 1 || sequence > MaxSequence)
            throw new ArgumentOutOfRangeException(nameof(sequence), "Ürün kodu sıra numarası tükendi veya geçersiz.");
        return Build(sequence);
    }

    /// <summary>
    /// Test ve hizalama etiketlerinde basılan kod: sıra 0 ("200000000004"). Biçimi geçerlidir (okuyucu denemesi için)
    /// ama hiçbir ürüne verilmez; okutulunca "test etiketi" olarak tanınır.
    /// </summary>
    public static readonly string TestCode = Build(0);

    public static bool IsTestCode(string? code) => code == TestCode;

    private static string Build(long sequence)
    {
        var body = Prefix + sequence.ToString("D10");
        return body + ComputeCheckDigit(body);
    }

    /// <summary>GS1 mod 10: sağdan başlayarak 3, 1, 3, … ağırlıklarıyla toplanır.</summary>
    public static char ComputeCheckDigit(string digits)
    {
        var sum = 0;
        var weight = 3;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            sum += (digits[i] - '0') * weight;
            weight = weight == 3 ? 1 : 3;
        }
        return (char)('0' + (10 - sum % 10) % 10);
    }

    /// <summary>Kod biçim ve kontrol hanesi olarak geçerli mi?</summary>
    public static bool IsValid(string? code)
    {
        if (code == null || code.Length != Length || code[0] != Prefix)
            return false;
        foreach (var c in code)
            if (c < '0' || c > '9')
                return false;
        return ComputeCheckDigit(code[..^1]) == code[^1];
    }

    /// <summary>Koddan sıra numarasını çıkarır (geçersizse false).</summary>
    public static bool TryGetSequence(string? code, out long sequence)
    {
        sequence = 0;
        return IsValid(code) && long.TryParse(code![1..^1], out sequence);
    }

    /// <summary>
    /// Okuyucudan / klavyeden gelen girişi denetler. Boşlukları atar; harf veya işaret varsa
    /// (Türkçe Q klavye ayarı yanlışsa olabilir) anlaşılır bir hata döner.
    /// </summary>
    public static ScanParseResult ParseInput(string? input)
    {
        var text = (input ?? string.Empty).Trim().Replace(" ", "");
        if (text.Length == 0)
            return new ScanParseResult(null, "Kod boş.");

        if (text.Any(c => c < '0' || c > '9'))
            return new ScanParseResult(null,
                $"Kodda rakam dışında karakter var (\"{text}\"). Okuyucunun klavye dili ayarını kontrol edin.");

        if (text.Length != Length)
            return new ScanParseResult(null, $"Kod {Length} haneli olmalı, {text.Length} hane okundu (\"{text}\").");

        if (text[0] != Prefix)
            return new ScanParseResult(null, $"Bu bir mağaza ürün kodu değil (\"{text}\" — kodlar {Prefix} ile başlar).");

        if (!IsValid(text))
            return new ScanParseResult(null, $"Kontrol hanesi tutmuyor (\"{text}\"). Kod yanlış yazılmış olabilir.");

        return new ScanParseResult(text, null);
    }
}

/// <summary>Okutma girişi denetim sonucu: geçerliyse <see cref="Code"/>, değilse <see cref="Error"/>.</summary>
public sealed record ScanParseResult(string? Code, string? Error)
{
    public bool IsValid => Code != null;
}
