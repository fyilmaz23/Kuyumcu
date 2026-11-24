using System.Security.Cryptography;

namespace Kuyumcu.Licensing;

/// <summary>
/// Geliştiricinin ÖZEL imza anahtarı (lisans üretmek için). Bu dosya SADECE geliştiricinin bilgisayarında durur;
/// depoya, CI'a ve uygulama paketine asla girmez. Kaybolursa mevcut açık anahtarla yeni lisans üretilemez
/// (uygulama güncellemesiyle yeni anahtar çiftine geçmek gerekir) — mutlaka yedekleyin.
/// </summary>
public static class LicenseSigningKey
{
    /// <summary>Varsayılan yolu değiştirmek için bu ortam değişkeni kullanılabilir.</summary>
    public const string PathEnvironmentVariable = "KUYUMCU_LISANS_OZEL_ANAHTAR";

    public static string DefaultPath =>
        Environment.GetEnvironmentVariable(PathEnvironmentVariable) is { Length: > 0 } custom
            ? custom
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kuyumcu-lisans", "ozel-anahtar.pem");

    public static bool Exists(string? path = null) => File.Exists(path ?? DefaultPath);

    public static ECDsa Load(string? path = null)
    {
        var key = ECDsa.Create();
        key.ImportFromPem(File.ReadAllText(path ?? DefaultPath));
        return key;
    }

    /// <summary>Yeni bir P-256 anahtar çifti üretir, özel anahtarı PEM olarak yazar ve açık anahtarı döndürür.</summary>
    public static string Create(string? path = null)
    {
        path ??= DefaultPath;
        if (File.Exists(path))
            throw new IOException($"Özel anahtar zaten var: {path}. Üzerine yazılmaz (eski lisanslar geçersiz olurdu).");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        File.WriteAllText(path, key.ExportPkcs8PrivateKeyPem());
        return ExportPublicKey(key);
    }

    /// <summary>Açık anahtarı LicensePublicKey.cs'e yazılacak biçimde (Base64 SubjectPublicKeyInfo) verir.</summary>
    public static string ExportPublicKey(ECDsa key) => Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
}
