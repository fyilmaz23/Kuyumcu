using Microsoft.Extensions.Logging;
using Kuyumcu.Services;
using MudBlazor.Services;

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
        builder.Services.AddMudServices();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();
        
        // Veritabanı yedekleme servisini başlat
        var backupService = app.Services.GetService<BackupService>();
        backupService?.Start();

		return app;
	}
}
