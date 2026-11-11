using Kuyumcu.Models;
using Kuyumcu.Platform;

namespace Kuyumcu.Mobile.Platform;

public sealed class MauiAppEnvironment : IAppEnvironment
{
    public string AppDataDirectory => FileSystem.AppDataDirectory;
    public string BackupDirectory => Path.Combine(FileSystem.AppDataDirectory, "Backups");
    public string CacheDirectory => FileSystem.CacheDirectory;

    // Android'de görseller paylaşım menüsüyle gönderildiği için önbellek klasörü yeterlidir
    public string PicturesDirectory => FileSystem.CacheDirectory;

    public bool IsReadOnlyClient => true;
    public bool IsMobile => true;

    public string GetDeviceFingerprint() =>
        $"{DeviceInfo.Name}-{DeviceInfo.Model}-{DeviceInfo.Manufacturer}-{DeviceInfo.Platform}";
}

public sealed class MauiSecureStore : ISecureStore
{
    public Task<string?> GetAsync(string key) => SecureStorage.Default.GetAsync(key);
    public Task SetAsync(string key, string value) => SecureStorage.Default.SetAsync(key, value);
}

public sealed class MauiFilePickerService : IFilePickerService
{
    public async Task<PickedFile?> PickFileAsync(string title, params string[] extensions)
    {
        // Android dosya seçicisi uzantıya göre filtreleyemez (yedek dosyaları "octet-stream" görünür);
        // uzantı kontrolü seçimden sonra yapılır.
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = title,
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.Android, extensions.Any(e => e is ".jpg" or ".jpeg" or ".png")
                    ? new[] { "image/*" }
                    : new[] { "application/octet-stream", "application/x-sqlite3", "application/vnd.sqlite3" } }
            })
        });

        if (result == null)
            return null;

        if (extensions.Length > 0 && !extensions.Any(e => result.FileName.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
        {
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page != null)
                await page.DisplayAlertAsync("Hata", $"Lütfen {string.Join(", ", extensions)} uzantılı bir dosya seçin.", "Tamam");
            return null;
        }

        return new PickedFile(result.FileName, result.FullPath);
    }
}

public sealed class MauiClipboardService : IClipboardService
{
    public Task SetTextAsync(string text) => Clipboard.Default.SetTextAsync(text);

    // Android panosu görsel kopyalamayı desteklemez; çağıran taraf paylaşım menüsüne geçer
    public Task<bool> TrySetImageAsync(string imageFilePath) => Task.FromResult(false);
}

public sealed class MauiShareService : IShareService
{
    public async Task<bool> ShareFileAsync(string filePath, string title)
    {
        try
        {
            await Share.Default.RequestAsync(new ShareFileRequest { Title = title, File = new ShareFile(filePath) });
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class UnsupportedReceiptPrinter : IReceiptPrinter
{
    public bool IsSupported => false;
    public void SetPrinterName(string printerName) { }
    public void PrintCustomerReceipt(Customer customer, List<TransactionGroup> transactions) { }
    public List<string> GetAvailablePrinters() => new();
}

public sealed class NoLegacyDataLocator : ILegacyDataLocator
{
    public string? FindLegacyDatabase() => null;
    public void MarkImported() { }
}
