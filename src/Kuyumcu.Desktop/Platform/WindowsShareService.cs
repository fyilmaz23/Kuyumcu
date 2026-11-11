using System.Diagnostics;
using Kuyumcu.Platform;

namespace Kuyumcu.Desktop.Platform;

/// <summary>
/// Windows'ta "paylaş": dosyayı Dosya Gezgini'nde seçili olarak gösterir
/// (kullanıcı oradan WhatsApp'a sürükleyebilir veya e-postaya ekleyebilir).
/// </summary>
public sealed class WindowsShareService : IShareService
{
    public Task<bool> ShareFileAsync(string filePath, string title)
    {
        try
        {
            if (!File.Exists(filePath))
                return Task.FromResult(false);

            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"") { UseShellExecute = true });
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
}
