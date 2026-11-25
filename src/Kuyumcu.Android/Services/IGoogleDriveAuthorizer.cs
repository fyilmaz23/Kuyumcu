namespace Kuyumcu.Droid.Services;

/// <summary>Google Drive ("drive.file") için erişim belirteci sağlar.</summary>
public interface IGoogleDriveAuthorizer
{
    /// <summary>
    /// Erişim belirteci döndürür. İzin daha önce verildiyse ekran açmadan döner.
    /// İzin gerekiyorsa: <paramref name="interactive"/> true ise Google hesap/izin ekranını açar,
    /// false ise <see cref="GoogleConsentRequiredException"/> fırlatır.
    /// Kullanıcı vazgeçerse <see cref="OperationCanceledException"/>.
    /// </summary>
    Task<string> GetAccessTokenAsync(bool interactive, CancellationToken cancellationToken = default);
}

/// <summary>Kullanıcının Google hesabını seçip izin vermesi gerekiyor (sessiz yenileme yapılamadı).</summary>
public sealed class GoogleConsentRequiredException() : Exception("Google hesabıyla bağlanmak gerekiyor.");

/// <summary>Google Play Hizmetleri/yetkilendirme hatası (ör. OAuth istemcisi yanlış yapılandırılmış).</summary>
public sealed class GoogleAuthorizationFailedException(string message, Exception? inner = null) : Exception(message, inner);
