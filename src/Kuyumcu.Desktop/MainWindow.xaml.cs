using System.Windows;
using Kuyumcu.Desktop.Platform;
using Kuyumcu.Platform;

namespace Kuyumcu.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        Resources.Add("services", App.Services);
        InitializeComponent();
        Title = ProductInfo.Name;

        // Boş ekran gibi sorunlarda neden görülebilsin diye WebView2 olaylarını günlüğe yaz
        BlazorWebView.BlazorWebViewInitialized += (_, args) =>
        {
            var core = args.WebView.CoreWebView2;
            AppLog.Info($"WebView2 hazır: {core.Environment.BrowserVersionString}");
            core.NavigationCompleted += (_, nav) =>
            {
                if (nav.IsSuccess)
                    AppLog.Info("Sayfa yüklendi.");
                else
                    AppLog.Error($"Sayfa yüklenemedi: {nav.WebErrorStatus} (HTTP {nav.HttpStatusCode})");
            };
            core.ProcessFailed += (_, failed) =>
                AppLog.Error($"WebView2 süreci çöktü: {failed.ProcessFailedKind}, {failed.Reason}, çıkış kodu {failed.ExitCode}");

            // Sayfadaki JavaScript hataları (Blazor açılamazsa sebebi burada görünür)
            core.GetDevToolsProtocolEventReceiver("Runtime.exceptionThrown").DevToolsProtocolEventReceived +=
                (_, ev) => AppLog.Error($"Sayfa hatası: {Shorten(ev.ParameterObjectAsJson)}");
            core.GetDevToolsProtocolEventReceiver("Runtime.consoleAPICalled").DevToolsProtocolEventReceived += (_, ev) =>
            {
                if (ev.ParameterObjectAsJson.Contains("\"type\":\"error\"", StringComparison.Ordinal))
                    AppLog.Error($"Sayfa konsolu: {Shorten(ev.ParameterObjectAsJson)}");
            };
            _ = core.CallDevToolsProtocolMethodAsync("Runtime.enable", "{}");
        };
    }

    private static string Shorten(string json) => json.Length <= 2000 ? json : json[..2000] + "…";
}
