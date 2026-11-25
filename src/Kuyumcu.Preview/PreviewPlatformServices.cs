using Kuyumcu.Models;
using Kuyumcu.Platform;

namespace Kuyumcu.Preview;

/// <summary>Önizleme verileri ayrı bir klasörde tutulur; gerçek kurulumlara dokunmaz.</summary>
public sealed class PreviewAppEnvironment : IAppEnvironment
{
    public PreviewAppEnvironment(string dataDirectory)
    {
        AppDataDirectory = Directory.CreateDirectory(dataDirectory).FullName;
    }

    public string AppDataDirectory { get; }
    public string BackupDirectory => Path.Combine(AppDataDirectory, "Backups");
    public string CacheDirectory => Directory.CreateDirectory(Path.Combine(AppDataDirectory, "cache")).FullName;
    public string PicturesDirectory => CacheDirectory;
    public bool IsReadOnlyClient => false;
    public bool IsMobile => false;
    public string GetDeviceFingerprint() => "preview-" + Environment.MachineName;
}

public sealed class InMemorySecureStore : ISecureStore
{
    private readonly Dictionary<string, string> _values = new();
    public Task<string?> GetAsync(string key) => Task.FromResult(_values.TryGetValue(key, out var v) ? v : null);
    public Task SetAsync(string key, string value) { _values[key] = value; return Task.CompletedTask; }
}

/// <summary>Tarayıcıda yerel dosya seçilemediği için önizlemede devre dışı.</summary>
public sealed class NoFilePicker : IFilePickerService
{
    public Task<PickedFile?> PickFileAsync(string title, params string[] extensions) => Task.FromResult<PickedFile?>(null);
}

public sealed class NoClipboard : IClipboardService
{
    public Task SetTextAsync(string text) => Task.CompletedTask;
    public Task<bool> TrySetImageAsync(string imageFilePath) => Task.FromResult(false);
}

public sealed class NoShare : IShareService
{
    public Task<bool> ShareFileAsync(string filePath, string title) => Task.FromResult(false);
}

public sealed class NoPrinter : IReceiptPrinter
{
    public bool IsSupported => false;
    public void SetPrinterName(string printerName) { }
    public void PrintCustomerReceipt(Customer customer, List<TransactionGroup> transactions, ReceiptTexts texts) { }
    public List<string> GetAvailablePrinters() => new();
}

public sealed class NoLegacyData : ILegacyDataLocator
{
    public string? FindLegacyDatabase() => null;
    public void MarkImported() { }
}

/// <summary>
/// Önizlemede gerçek yazıcı yok: etiket komutları .preview-data/labels altına dosya olarak yazılır
/// (TSPL çıktısını incelemek için). Yazıcı listesinde tek sahte yazıcı görünür.
/// </summary>
public sealed class FileLabelPrinter : ILabelPrinter
{
    public const string FakePrinterName = "Önizleme (dosyaya yazar)";
    private readonly string _directory;

    public FileLabelPrinter(string directory)
    {
        _directory = directory;
    }

    public bool IsSupported => true;
    public List<string> GetAvailablePrinters() => new() { FakePrinterName };

    public async Task SendRawAsync(string printerName, byte[] data, string documentName)
    {
        Directory.CreateDirectory(_directory);
        var safe = string.Concat(documentName.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
        await File.WriteAllBytesAsync(Path.Combine(_directory, $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{safe}.tspl"), data);
    }
}
