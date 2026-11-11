using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using Kuyumcu.Platform;

namespace Kuyumcu.Desktop.Platform;

public sealed class WindowsClipboardService : IClipboardService
{
    public Task SetTextAsync(string text) =>
        RunWithRetryAsync(() => Clipboard.SetText(text));

    public async Task<bool> TrySetImageAsync(string imageFilePath)
    {
        try
        {
            await RunWithRetryAsync(() =>
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad; // dosya kilidini hemen bırak
                image.UriSource = new Uri(imageFilePath);
                image.EndInit();
                image.Freeze();
                Clipboard.SetImage(image);
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Pano başka bir uygulama tarafından kısa süreliğine kilitlenebilir; birkaç kez dener.
    /// </summary>
    private static Task RunWithRetryAsync(Action action)
    {
        return Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    action();
                    return;
                }
                catch (COMException) when (attempt < 5)
                {
                    await Task.Delay(100);
                }
            }
        }).Task.Unwrap();
    }
}
