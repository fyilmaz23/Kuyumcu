using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kuyumcu.Platform;

namespace Kuyumcu.Desktop.Platform;

/// <summary>
/// Windows DPAPI ile (sadece bu Windows kullanıcısının çözebileceği şekilde) şifrelenmiş anahtar/değer deposu.
/// </summary>
public sealed class DpapiSecureStore : ISecureStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("KuyumcuDefteri.SecureStore.v1");
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
        }
        finally
        {
            _lock.Release();
        }
    }

    private Dictionary<string, string> Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new();

            var decrypted = ProtectedData.Unprotect(File.ReadAllBytes(_filePath), Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(decrypted) ?? new();
        }
        catch
        {
            // Bozuk veya başka kullanıcıya ait dosya: boş kabul et (lisans servisi toleranslıdır)
            return new();
        }
    }
}
