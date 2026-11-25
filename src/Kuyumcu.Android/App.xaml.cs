using Kuyumcu.Droid.Services;

namespace Kuyumcu.Droid;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage()) { Title = Kuyumcu.Platform.ProductInfo.Name };

        // Uygulamaya her dönüşte Drive'da yeni yedek var mı sessizce bak (bağlıysa; sıklık sınırlı)
        window.Resumed += (_, _) => StartAutoRefresh();
        window.Created += (_, _) => StartAutoRefresh();
        return window;
    }

    private static void StartAutoRefresh()
    {
        var sync = IPlatformApplication.Current?.Services.GetService<BackupSyncService>();
        if (sync != null)
            _ = sync.AutoRefreshAsync();
    }
}
