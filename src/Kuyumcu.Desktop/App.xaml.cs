using System.Globalization;
using System.Windows;
using Kuyumcu.Desktop.Platform;
using Kuyumcu.Platform;
using Kuyumcu.Services;
using Kuyumcu.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

namespace Kuyumcu.Desktop;

public partial class App : Application
{
    // Aynı anda iki pencere açılıp aynı veritabanına yazılmasını engeller
    private Mutex? _singleInstanceMutex;

    public static IServiceProvider Services { get; private set; } = default!;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Tutar ve tarih biçimleri, Windows dili ne olursa olsun Türkçe olsun (1.250,50 ₺ / 11.11.2026)
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("tr-TR");

        _singleInstanceMutex = new Mutex(initiallyOwned: true, $"Local\\{ProductInfo.DataFolderName}.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show($"{ProductInfo.Name} zaten açık.", ProductInfo.Name, MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show($"Beklenmeyen bir hata oluştu:\n{args.Exception.Message}", ProductInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        Services = ConfigureServices();

        // WebView2 verileri yazılabilir bir klasörde tutulmalı (MSIX kurulum klasörü salt okunurdur)
        var environment = Services.GetRequiredService<IAppEnvironment>();
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", Path.Combine(environment.AppDataDirectory, "WebView2"));
        base.OnStartup(e);

        // Veritabanı şemasını güncelle, lisans denemesini başlat ve yerel yedeklemeyi çalıştır
        _ = Task.Run(async () =>
        {
            await Services.GetRequiredService<DatabaseService>().UpdateDatabaseSchemaAsync();
            await Services.GetRequiredService<LicenseService>().InitializeTrialAsync();
            Services.GetRequiredService<BackupService>().Start();
        });

        new MainWindow().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        (Services as IDisposable)?.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddWpfBlazorWebView();
        services.AddMudServices();
        services.AddLogging(b =>
        {
#if DEBUG
            b.AddDebug();
#endif
        });
#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif

        // Windows'a özel platform servisleri
        services.AddSingleton<IAppEnvironment, WindowsAppEnvironment>();
        services.AddSingleton<ISecureStore, DpapiSecureStore>();
        services.AddSingleton<IFilePickerService, WindowsFilePickerService>();
        services.AddSingleton<IClipboardService, WindowsClipboardService>();
        services.AddSingleton<IShareService, WindowsShareService>();
        services.AddSingleton<IReceiptPrinter, WindowsReceiptPrinter>();
        services.AddSingleton<ILegacyDataLocator, WindowsLegacyDataLocator>();

        // Ortak servisler ve arayüz durumu
        services.AddKuyumcuCore();
        services.AddScoped<AppState>();

        return services.BuildServiceProvider();
    }
}
