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

// Önizleme verileri: proje klasöründeki .preview-data (git'e girmez)
var dataDir = Path.Combine(builder.Environment.ContentRootPath, ".preview-data");
builder.Services.AddSingleton<IAppEnvironment>(new PreviewAppEnvironment(dataDir));
builder.Services.AddSingleton<ISecureStore, InMemorySecureStore>();
builder.Services.AddSingleton<IFilePickerService, NoFilePicker>();
builder.Services.AddSingleton<IClipboardService, NoClipboard>();
builder.Services.AddSingleton<IShareService, NoShare>();
builder.Services.AddSingleton<IReceiptPrinter, NoPrinter>();
builder.Services.AddSingleton<ILegacyDataLocator, NoLegacyData>();
builder.Services.AddKuyumcuCore();
builder.Services.AddScoped<AppState>();
builder.Services.AddScoped<ShortcutService>();

var app = builder.Build();

await SampleData.SeedAsync(app.Services.GetRequiredService<DatabaseService>(), app.Services.GetRequiredService<LicenseService>());

app.UseStaticFiles();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<Kuyumcu.Preview.Components.App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Kuyumcu.Components.Routes).Assembly);

Console.WriteLine("Önizleme hazır: http://localhost:5080");
app.Run();
