using System.Security.Cryptography;

namespace Kuyumcu.Licensing;

/// <summary>
/// Lisans imzalarını doğrulayan AÇIK anahtar. Gizli değildir; depoda durabilir.
/// Karşılığı olan özel anahtar sadece geliştiricide durur (bkz. LicenseSigningKey, tools/Kuyumcu.LicenseTool).
/// Bu değer değişirse daha önce verilmiş TÜM lisanslar geçersiz olur.
/// </summary>
internal static class LicensePublicKey
{
    // Üretim anahtarı (24.11.2026, tools/Kuyumcu.LicenseTool anahtar-olustur)
    public const string Base64 =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEqVAQSMbbhKDZ4345aw+sNRL/GC1+CSOuhlKHuPUMr/diBqoq4JxBa7rxOFOsi6doqEurzvtn38jKBRWahPN9PQ==";

    public static ECDsa Create()
    {
        var key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(Base64), out _);
        return key;
    }
}
