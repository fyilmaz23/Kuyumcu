using Kuyumcu.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Kuyumcu;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Platformdan bağımsız servisleri kaydeder. Platform servisleri (IAppEnvironment, ISecureStore,
    /// IFilePickerService, IClipboardService, IShareService, IReceiptPrinter) her uygulama projesinde ayrıca kaydedilir.
    /// </summary>
    public static IServiceCollection AddKuyumcuCore(this IServiceCollection services)
    {
        services.AddSingleton<DatabaseService>();
        services.AddSingleton<BackupService>();
        services.AddSingleton<GoogleDriveService>();
        services.AddSingleton<LicenseService>();
        services.AddSingleton<GoldPriceExportService>();
        services.AddSingleton<CustomerSummaryImageService>();
        return services;
    }
}
