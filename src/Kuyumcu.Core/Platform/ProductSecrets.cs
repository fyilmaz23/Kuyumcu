namespace Kuyumcu.Platform;

/// <summary>
/// Ürüne ait (müşteriye değil) gizli yapılandırmalar.
/// Gerçek değerler CI tarafından GitHub Secrets'tan ProductSecrets.Secrets.cs olarak üretilir.
/// </summary>
public static partial class ProductSecrets
{
    /// <summary>Ürünün kendi Google OAuth istemcisi tanımlı mı?</summary>
    public static bool HasBuiltInGoogleClient =>
        !string.IsNullOrWhiteSpace(GoogleDesktopClientId) && !string.IsNullOrWhiteSpace(GoogleDesktopClientSecret);
}
