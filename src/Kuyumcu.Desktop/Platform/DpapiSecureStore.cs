using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kuyumcu.Platform;
using Microsoft.Win32;

namespace Kuyumcu.Desktop.Platform;

/// <summary>
/// Windows DPAPI ile (sadece bu Windows kullanıcısının çözebileceği şekilde) şifrelenmiş anahtar/değer deposu.
/// Veri hem uygulama klasöründeki dosyada hem kayıt defterinde (HKCU) tutulur: veri klasörü silinse bile
/// deneme başlangıcı gibi bilgiler kaybolmaz. (MSIX kurulumunda kayıt defteri de paket kaldırılınca silinir.)
/// </summary>
public sealed class DpapiSecureStore : ISecureStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("KuyumcuDefteri.SecureStore.v1");
    private static readonly string RegistryKeyPath = $@"Software\{ProductInfo.DataFolderName}";
    private const string RegistryValueName = "SecureStore";
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public DpapiSecureStore(IAppEnvironment environment)
    {
        _filePath = Path.Combine(environment.AppDataDirectory, "secure.dat");
    }

    public async Task<string?> GetAsync(string key)
    {
        await _lock.WaitAsync();
        try
        {
            return Load().TryGetValue(key, out var value) ? value : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SetAsync(string key, string value)
    {
        await _lock.WaitAsync();
        try
        {
            var data = Load();
            data[key] = value;
            var json = JsonSerializer.SerializeToUtf8Bytes(data);
            var encrypted = ProtectedData.Protect(json, Entropy, DataProtectionScope.CurrentUser);
            await File.WriteAllBytesAsync(_filePath, encrypted);
            WriteRegistry(encrypted);
        }
        finally
        {
            _lock.Release();
        }
    }

    private Dictionary<string, string> Load()
    {
        // Önce dosya; yoksa ya da okunamazsa kayıt defterindeki kopya
        var data = Decrypt(File.Exists(_filePath) ? SafeRead(_filePath) : null) ?? Decrypt(ReadRegistry());
        return data ?? new();
    }

    private static Dictionary<string, string>? Decrypt(byte[]? encrypted)
    {
        if (encrypted is not { Length: > 0 })
            return null;
        try
        {
            var decrypted = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(decrypted);
        }
        catch
        {
            // Bozuk veya başka kullanıcıya ait veri: yok say (lisans servisi toleranslıdır)
            return null;
        }
    }

    private static byte[]? SafeRead(string path)
    {
        try { return File.ReadAllBytes(path); }
        catch { return null; }
    }

    private static byte[]? ReadRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            return key?.GetValue(RegistryValueName) as byte[];
        }
        catch
        {
            return null;
        }
    }

    private static void WriteRegistry(byte[] encrypted)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
            key.SetValue(RegistryValueName, encrypted, RegistryValueKind.Binary);
        }
        catch
        {
            // Kayıt defteri yazılamazsa dosya yeterli
        }
    }
}
