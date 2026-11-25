namespace Kuyumcu.Droid.Services;

/// <summary>Google Drive ("drive.file") için erişim belirteci sağlar.</summary>
public interface IGoogleDriveAuthorizer
{
    /// <summary>
    /// Erişim belirteci döndürür. İzin daha önce verildiyse ekran açmadan döner.
    /// İzin gerekiyorsa: <paramref name="interactive"/> true ise Google hesap/izin ekranını açar,
    /// false ise <see cref="GoogleConsentRequiredException"/> fırlatır.
    /// Kullanıcı vazgeçerse <see cref="OperationCanceledException"/>.
    /// <paramref name="chooseAccount"/> true ise (yalnızca interactive ile) izin daha önce verilmiş olsa bile
    /// Google hesap seçici açılır; "Google hesabıyla bağlan" düğmesi bunu kullanır.
    /// </summary>
    Task<string> GetAccessTokenAsync(bool interactive, bool chooseAccount = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bu uygulamaya verilen Drive iznini Google tarafında geri alır ve önbellekteki belirteci siler.
    /// İzin yoksa bir şey yapmaz.
    /// </summary>
    Task SignOutAsync(CancellationToken cancellationToken = default);

    /// <summary>Drive'ın reddettiği (401) belirteci önbellekten siler; sonraki istek yenisini alır.</summary>
    Task InvalidateTokenAsync(string accessToken);
}

/// <summary>Kullanıcının Google hesabını seçip izin vermesi gerekiyor (sessiz yenileme yapılamadı).</summary>
public sealed class GoogleConsentRequiredException() : Exception("Google hesabıyla bağlanmak gerekiyor.");

/// <summary>Google Play Hizmetleri/yetkilendirme hatası (ör. OAuth istemcisi yanlış yapılandırılmış).</summary>
public sealed class GoogleAuthorizationFailedException(string message, Exception? inner = null) : Exception(message, inner);
