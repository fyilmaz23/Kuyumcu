using Kuyumcu.Platform;

namespace Kuyumcu.Droid.Platform;

public sealed class AndroidAppEnvironment : IAppEnvironment
{
    public string AppDataDirectory => FileSystem.AppDataDirectory;

    // Telefon kendi yedeğini almaz; arayüz gereği tanımlı
    public string BackupDirectory => Path.Combine(FileSystem.AppDataDirectory, "Backups");
    public string CacheDirectory => FileSystem.CacheDirectory;

    // Görseller paylaşım menüsüyle gönderildiği için önbellek klasörü yeterlidir
    public string PicturesDirectory => FileSystem.CacheDirectory;

    public bool IsReadOnlyClient => true;
    public bool IsMobile => true;

    public string GetDeviceFingerprint() =>
        $"{DeviceInfo.Name}-{DeviceInfo.Model}-{DeviceInfo.Manufacturer}-{DeviceInfo.Platform}";
}

public sealed class AndroidShareService : IShareService
{
    public async Task<bool> ShareFileAsync(string filePath, string title)
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                Share.Default.RequestAsync(new ShareFileRequest { Title = title, File = new ShareFile(filePath, ContentTypeOf(filePath)) }));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ContentTypeOf(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => "application/octet-stream",
    };
}

public sealed class AndroidClipboardService : IClipboardService
{
    public Task SetTextAsync(string text) => Clipboard.Default.SetTextAsync(text);

    // Android panosu görsel kopyalamayı desteklemez; çağıran taraf paylaşım menüsüne geçer
    public Task<bool> TrySetImageAsync(string imageFilePath) => Task.FromResult(false);
}
