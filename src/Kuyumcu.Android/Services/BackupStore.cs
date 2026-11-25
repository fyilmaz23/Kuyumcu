using System.Text.Json;
using System.Text.Json.Serialization;
using Kuyumcu.Services;

namespace Kuyumcu.Droid.Services;

/// <summary>Telefondaki yedeğin nereden ve ne zaman geldiği.</summary>
public sealed class BackupMeta
{
    /// <summary>"drive" (otomatik indirildi), "file" (dosya seçiciyle açıldı) ya da "demo" (örnek veriler).</summary>
    public string Source { get; set; } = "drive";

    public string? DriveFileId { get; set; }

    /// <summary>Bilgisayarın bu yedeği aldığı an (Drive'daki son değişiklik ya da dosyanın tarihi).</summary>
    public DateTimeOffset? BackupTime { get; set; }

    public string? Md5Checksum { get; set; }
    public long Size { get; set; }
    public string? FileName { get; set; }

    /// <summary>Telefona indirildiği an.</summary>
    public DateTimeOffset DownloadedAt { get; set; }

    /// <summary>Drive'da yeni yedek olup olmadığına son bakılan an (yeni yoksa da güncellenir).</summary>
    public DateTimeOffset? LastCheckedAt { get; set; }

    [JsonIgnore] public bool IsFromDrive => Source == "drive";
    [JsonIgnore] public bool IsDemo => Source == "demo";
}

/// <summary>
/// İndirilen yedeğin telefonda saklanması: uygulamanın özel klasöründe tek dosya + bilgi dosyası.
/// İnternet yokken son indirilen gösterilir. Yedek salt okunur açılır, telefonda hiç değiştirilmez.
/// </summary>
public sealed class BackupStore
{
    private const string DataFileName = "son_yedek.db3";
    private const string MetaFileName = "son_yedek.json";

    private readonly string _folder;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Task? _loadTask;

    public BackupStore()
    {
        _folder = Path.Combine(FileSystem.AppDataDirectory, "yedek");
        Directory.CreateDirectory(_folder);
        Meta = ReadMeta();
    }

    private string DataPath => Path.Combine(_folder, DataFileName);
    private string MetaPath => Path.Combine(_folder, MetaFileName);

    /// <summary>Belleğe alınmış yedek; henüz yoksa ya da okunamadıysa null.</summary>
    public BackupSnapshot? Snapshot { get; private set; }

    public BackupMeta? Meta { get; private set; }

    /// <summary>Telefonda okunabilir bir yedek var mı?</summary>
    public bool HasData => Snapshot != null;

    /// <summary>Kayıtlı yedek okunamadıysa nedeni.</summary>
    public string? LoadError { get; private set; }

    public event Action? Changed;

    /// <summary>Kayıtlı yedeği (bir kez) arka planda belleğe alır.</summary>
    public Task EnsureLoadedAsync() => _loadTask ??= Task.Run(LoadExisting);

    private void LoadExisting()
    {
        if (!File.Exists(DataPath))
            return;

        try
        {
            Snapshot = BackupSnapshot.Load(DataPath);
            LoadError = null;
        }
        catch (Exception ex)
        {
            LoadError = ex.Message;
        }
        Changed?.Invoke();
    }

    /// <summary>
    /// Yeni indirilen/seçilen dosyayı doğrular ve mevcut yedeğin yerine koyar. Dosya geçersizse
    /// <see cref="InvalidDataException"/> fırlatır ve eldeki yedek korunur.
    /// </summary>
    public async Task ReplaceAsync(string newFilePath, BackupMeta meta)
    {
        // Önce doğrula: bozuk/yanlış dosya eldeki çalışan yedeği silmesin
        var snapshot = await Task.Run(() => BackupSnapshot.Load(newFilePath));

        await _lock.WaitAsync();
        try
        {
            File.Move(newFilePath, DataPath, overwrite: true);
            meta.Size = new FileInfo(DataPath).Length;
            Snapshot = snapshot;
            Meta = meta;
            LoadError = null;
            WriteMeta(meta);
        }
        finally
        {
            _lock.Release();
        }
        Changed?.Invoke();
    }

    /// <summary>Drive'da yeni yedek yoktu: sadece "son kontrol" zamanını günceller.</summary>
    public void MarkChecked(DateTimeOffset when)
    {
        if (Meta == null)
            return;
        Meta.LastCheckedAt = when;
        WriteMeta(Meta);
        Changed?.Invoke();
    }

    /// <summary>Telefondaki yedeği ve bilgilerini siler.</summary>
    public async Task ClearAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (File.Exists(DataPath)) File.Delete(DataPath);
            if (File.Exists(MetaPath)) File.Delete(MetaPath);
            Snapshot = null;
            Meta = null;
            LoadError = null;
        }
        finally
        {
            _lock.Release();
        }
        Changed?.Invoke();
    }

    /// <summary>İndirme için geçici dosya yolu (aynı birimde, taşıma atomik olsun diye).</summary>
    public string NewTempPath() => Path.Combine(_folder, $"indiriliyor_{Guid.NewGuid():N}.db3");

    private BackupMeta? ReadMeta()
    {
        try
        {
            return File.Exists(MetaPath)
                ? JsonSerializer.Deserialize(File.ReadAllText(MetaPath), BackupJsonContext.Default.BackupMeta)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private void WriteMeta(BackupMeta meta) =>
        File.WriteAllText(MetaPath, JsonSerializer.Serialize(meta, BackupJsonContext.Default.BackupMeta));
}

[JsonSerializable(typeof(BackupMeta))]
internal sealed partial class BackupJsonContext : JsonSerializerContext;
