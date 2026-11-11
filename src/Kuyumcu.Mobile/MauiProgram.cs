using System.Globalization;
using Kuyumcu.Mobile.Platform;
using Kuyumcu.Platform;
using Kuyumcu.Services;
using Kuyumcu.UI.Services;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

namespace Kuyumcu.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // Tutar ve tarih biçimleri, telefon dili ne olursa olsun Türkçe olsun
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("tr-TR");

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"));

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddMudServices();

        // Android'e özel platform servisleri (salt okunur istemci)
        builder.Services.AddSingleton<IAppEnvironment, MauiAppEnvironment>();
        builder.Services.AddSingleton<ISecureStore, MauiSecureStore>();
        builder.Services.AddSingleton<IFilePickerService, MauiFilePickerService>();
        builder.Services.AddSingleton<IClipboardService, MauiClipboardService>();
        builder.Services.AddSingleton<IShareService, MauiShareService>();
        builder.Services.AddSingleton<IReceiptPrinter, UnsupportedReceiptPrinter>();
        builder.Services.AddSingleton<ILegacyDataLocator, NoLegacyDataLocator>();

        // Ortak servisler. Mobil uygulama kendi veritabanına yazmaz; yedek dosyasını salt okunur açar.
        builder.Services.AddKuyumcuCore();
        builder.Services.AddSingleton<DatabaseImportService>();
        builder.Services.AddScoped<AppState>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
