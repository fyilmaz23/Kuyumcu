using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kuyumcu.Platform;

namespace Kuyumcu.Services;

/// <summary>Drive'daki "son yedek" dosyasının bilgileri.</summary>
/// <param name="ModifiedTime">Dosyanın Drive'da son güncellendiği an = bilgisayarın son yedek aldığı an.</param>
public sealed record DriveBackupInfo(string FileId, string Name, DateTimeOffset ModifiedTime, long Size, string? Md5Checksum);

/// <summary>Drive erişim izni yok ya da süresi doldu (yeniden yetki istenmeli).</summary>
public sealed class DriveAuthorizationException(string message) : Exception(message);

/// <summary>
/// Telefonun, bilgisayarın Drive'a yüklediği <see cref="ProductInfo.DriveLatestFileName"/> dosyasını bulup indirmesi.
/// Erişim belirteci (access token) platformdan gelir (Android: Google Authorization API, "drive.file" kapsamı).
///
/// "drive.file" kapsamı sadece AYNI Google Cloud projesinin istemcilerinin oluşturduğu dosyaları gösterir; bu yüzden
/// Android OAuth istemcisi masaüstü istemcisiyle aynı projede olmalıdır (bkz. docs/YAYIN.md).
/// Google API istemci kütüphanesi yerine düz REST kullanılır: telefonda gereken iki çağrı için yeterli ve hafif.
/// </summary>
public sealed class DriveLatestBackupClient
{
    public const string DriveFileScope = "https://www.googleapis.com/auth/drive.file";

    private const string FilesEndpoint = "https://www.googleapis.com/drive/v3/files";
    private const string FileFields = "files(id,name,modifiedTime,size,md5Checksum)";

    private readonly HttpClient _http;

    public DriveLatestBackupClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// En son güncellenen "son yedek" dosyasını bulur; yoksa null. Önce uygulama işaretine (appProperties),
    /// bulunamazsa dosya adına bakar.
    /// </summary>
    public async Task<DriveBackupInfo?> FindLatestAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var byKind = $"appProperties has {{ key='{ProductInfo.DriveKindProperty}' and value='{ProductInfo.DriveKindLatest}' }} and trashed = false";
        var found = await ListAsync(accessToken, byKind, cancellationToken);
        if (found.Count == 0)
        {
            var byName = $"name = '{ProductInfo.DriveLatestFileName}' and trashed = false";
            found = await ListAsync(accessToken, byName, cancellationToken);
        }

        return found
            .Where(f => !string.IsNullOrEmpty(f.Id))
            .OrderByDescending(f => f.ModifiedTime)
            .Select(f => new DriveBackupInfo(
                f.Id!,
                f.Name ?? ProductInfo.DriveLatestFileName,
                f.ModifiedTime ?? DateTimeOffset.MinValue,
                long.TryParse(f.Size, out var size) ? size : 0,
                f.Md5Checksum))
            .FirstOrDefault();
    }

    /// <summary>Dosyayı <paramref name="destinationPath"/> yoluna indirir (varsa üzerine yazar).</summary>
    public async Task DownloadAsync(string accessToken, string fileId, string destinationPath, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{FilesEndpoint}/{Uri.EscapeDataString(fileId)}?alt=media");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(target, cancellationToken);
    }

    /// <summary>Bağlı Google hesabının e-posta adresi (ekranda göstermek için). Alınamazsa null.</summary>
    public async Task<string?> GetAccountEmailAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/drive/v3/about?fields=user(emailAddress)");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            var about = await JsonSerializer.DeserializeAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken), DriveJsonContext.Default.AboutResponse, cancellationToken);
            return about?.User?.EmailAddress;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            return null;
        }
    }

    /// <summary>Uygulamaya verilen Drive iznini Google tarafında geri alır (en iyi çaba).</summary>
    public async Task RevokeAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        try
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = accessToken });
            using var response = await _http.PostAsync("https://oauth2.googleapis.com/revoke", content, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // İnternet yoksa yerel bağlantı yine de kesilir; kullanıcı izni Google hesabından da kaldırabilir.
        }
    }

    private async Task<List<DriveFileDto>> ListAsync(string accessToken, string query, CancellationToken cancellationToken)
    {
        var url = $"{FilesEndpoint}?spaces=drive&pageSize=10&orderBy=modifiedTime%20desc" +
                  $"&fields={Uri.EscapeDataString(FileFields)}&q={Uri.EscapeDataString(query)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var list = await JsonSerializer.DeserializeAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), DriveJsonContext.Default.FileListResponse, cancellationToken);
        return list?.Files ?? new List<DriveFileDto>();
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = string.Empty;
        try { body = await response.Content.ReadAsStringAsync(cancellationToken); } catch { /* yok say */ }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new DriveAuthorizationException("Google Drive izni geçersiz ya da süresi doldu.");

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            // En sık neden: Cloud projesinde Drive API kapalı ya da izin kaldırıldı
            if (body.Contains("accessNotConfigured", StringComparison.OrdinalIgnoreCase) ||
                body.Contains("SERVICE_DISABLED", StringComparison.OrdinalIgnoreCase))
                throw new HttpRequestException("Google Drive API bu uygulama için etkin değil (geliştiriciye bildirin).", null, response.StatusCode);
            if (body.Contains("insufficient", StringComparison.OrdinalIgnoreCase))
                throw new DriveAuthorizationException("Google Drive izni yetersiz; yeniden bağlanın.");
        }

        throw new HttpRequestException($"Google Drive isteği başarısız ({(int)response.StatusCode}).", null, response.StatusCode);
    }

    internal sealed class FileListResponse
    {
        [JsonPropertyName("files")] public List<DriveFileDto>? Files { get; set; }
    }

    internal sealed class DriveFileDto
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("modifiedTime")] public DateTimeOffset? ModifiedTime { get; set; }
        [JsonPropertyName("size")] public string? Size { get; set; }
        [JsonPropertyName("md5Checksum")] public string? Md5Checksum { get; set; }
    }

    internal sealed class AboutResponse
    {
        [JsonPropertyName("user")] public AboutUser? User { get; set; }
    }

    internal sealed class AboutUser
    {
        [JsonPropertyName("emailAddress")] public string? EmailAddress { get; set; }
    }
}

/// <summary>JSON kaynak üretici bağlamı: Android'in kırpılmış (trimmed) sürümünde yansıma gerektirmeden çalışır.</summary>
[JsonSerializable(typeof(DriveLatestBackupClient.FileListResponse))]
[JsonSerializable(typeof(DriveLatestBackupClient.AboutResponse))]
internal sealed partial class DriveJsonContext : JsonSerializerContext;
