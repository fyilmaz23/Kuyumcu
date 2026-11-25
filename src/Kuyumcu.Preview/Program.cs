using System.Globalization;
using Kuyumcu;
using Kuyumcu.Platform;
using Kuyumcu.Preview;
using Kuyumcu.Services;
using Kuyumcu.UI.Services;
using MudBlazor.Services;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("tr-TR");

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["urls"] ?? "http://localhost:5080");
// Önizleme her zaman kaynak koddan çalışır: UI kütüphanesinin css/js dosyalarını doğrudan sun
builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddMudServices();

// Önizleme verileri: proje klasöründeki .preview-data (git'e girmez).
// "--bos true": örnek veri yüklenmeden, ayrı klasörde (.preview-data-bos) yeni kurulum gibi başlar (kurulum ekranını denemek için).
var startEmpty = string.Equals(builder.Configuration["bos"], "true", StringComparison.OrdinalIgnoreCase);
var dataDir = Path.Combine(builder.Environment.ContentRootPath, startEmpty ? ".preview-data-bos" : ".preview-data");
builder.Services.AddSingleton<IAppEnvironment>(new PreviewAppEnvironment(dataDir));
builder.Services.AddSingleton<ISecureStore, InMemorySecureStore>();
builder.Services.AddSingleton<IFilePickerService, NoFilePicker>();
builder.Services.AddSingleton<IClipboardService, NoClipboard>();
builder.Services.AddSingleton<IShareService, NoShare>();
builder.Services.AddSingleton<IReceiptPrinter, NoPrinter>();
builder.Services.AddSingleton<ILabelPrinter>(new FileLabelPrinter(Path.Combine(dataDir, "labels")));
builder.Services.AddSingleton<ILegacyDataLocator, NoLegacyData>();
builder.Services.AddKuyumcuCore();
builder.Services.AddScoped<AppState>();
builder.Services.AddScoped<ShortcutService>();

var app = builder.Build();

if (!startEmpty)
{
    await SampleData.SeedAsync(app.Services.GetRequiredService<DatabaseService>(), app.Services.GetRequiredService<LicenseService>(),
        app.Services.GetRequiredService<StockService>(), FileLabelPrinter.FakePrinterName);
}

app.UseStaticFiles();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<Kuyumcu.Preview.Components.App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Kuyumcu.Components.Routes).Assembly);

Console.WriteLine("Önizleme hazır: http://localhost:5080");
app.Run();
