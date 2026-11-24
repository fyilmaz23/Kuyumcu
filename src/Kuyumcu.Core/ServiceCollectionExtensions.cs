using Kuyumcu.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Kuyumcu;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Platformdan bağımsız servisleri kaydeder. Platform servisleri (IAppEnvironment, ISecureStore,
    /// IFilePickerService, IClipboardService, IShareService, IReceiptPrinter, ILabelPrinter, ILegacyDataLocator)
    /// her uygulama projesinde ayrıca kaydedilir.
    /// </summary>
    public static IServiceCollection AddKuyumcuCore(this IServiceCollection services)
    {
        services.AddSingleton<DatabaseService>();
        services.AddSingleton<BackupService>();
        services.AddSingleton<GoogleDriveService>();
        services.AddSingleton<LicenseService>();
        services.AddSingleton<IGoldPriceSource, ElazigAltinFiyatlariSource>();
        services.AddSingleton<GoldPriceExportService>();
        services.AddSingleton<CustomerSummaryImageService>();

        // Barkodlu vitrin stoğu, kasa, sayım ve etiket
        services.AddSingleton<StockService>();
        services.AddSingleton<StockCountService>();
        services.AddSingleton<LabelPrintService>();
        services.AddSingleton<ReportExportService>();
        return services;
    }
}
