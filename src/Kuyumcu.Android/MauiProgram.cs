using System.Globalization;
using Kuyumcu.Droid.Platform;
using Kuyumcu.Droid.Services;
using Kuyumcu.Platform;
using Kuyumcu.Services;
using Microsoft.Extensions.Logging;

namespace Kuyumcu.Droid;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // Tutar ve tarih biçimleri, telefon dili ne olursa olsun Türkçe olsun
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("tr-TR");

        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddMauiBlazorWebView();

        // Platform servisleri (Core'daki görsel üretimi bunları kullanır)
        builder.Services.AddSingleton<IAppEnvironment, AndroidAppEnvironment>();
        builder.Services.AddSingleton<IShareService, AndroidShareService>();
        builder.Services.AddSingleton<IClipboardService, AndroidClipboardService>();

        // Core: sadece okuma ve görsel üretimi. Telefon kendi veritabanına yazmaz, lisans/yedekleme servisleri yoktur.
        builder.Services.AddSingleton<GoldPriceExportService>();
        builder.Services.AddSingleton<CustomerSummaryImageService>();
        builder.Services.AddSingleton(_ => new DriveLatestBackupClient(new HttpClient { Timeout = TimeSpan.FromMinutes(5) }));

        // Uygulama servisleri
        builder.Services.AddSingleton<IGoogleDriveAuthorizer, GoogleDriveAuthorizer>();
        builder.Services.AddSingleton<BackupStore>();
        builder.Services.AddSingleton<BackupSyncService>();
        builder.Services.AddSingleton<UiState>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
