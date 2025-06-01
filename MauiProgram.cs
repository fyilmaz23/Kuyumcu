using Microsoft.Extensions.Logging;
using Kuyumcu.Services;
using MudBlazor.Services;
using System.Net;
using System.Net.Http;
#if ANDROID
using Kuyumcu.Platforms.Android.Services;
#endif

namespace Kuyumcu;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();
		builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<BackupService>();
        builder.Services.AddSingleton<PrintService>();
        builder.Services.AddSingleton<GoogleDriveService>();
        
        // CookieHandler ile HttpClient oluştur
        var cookieContainer = new CookieContainer();
        var handler = new HttpClientHandler
        {
            CookieContainer = cookieContainer,
            UseCookies = true,
            AllowAutoRedirect = true
        };
        builder.Services.AddSingleton<HttpClient>(new HttpClient(handler));
        builder.Services.AddSingleton<GoldPriceExportService>();
        
        // Register File Import Services
#if ANDROID
        builder.Services.AddSingleton<IFileImportService, AndroidFileImportService>();
        builder.Services.AddSingleton<DatabaseImportService>();
#endif
        
        builder.Services.AddMudServices();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();
        
        // Veritabanı yedekleme servisini başlat
        var backupService = app.Services.GetService<BackupService>();
        backupService?.Start();
        
        // Veritabanı şemasını güncelle
        Task.Run(async () => {
            var dbService = app.Services.GetService<DatabaseService>();
            if (dbService != null)
            {
                await dbService.UpdateDatabaseSchemaAsync();
            }
        });

		return app;
	}
}
