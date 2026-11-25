using System.Net.Sockets;
using Kuyumcu.Labels;
using Kuyumcu.Models;
using Kuyumcu.Platform;
using Microsoft.Extensions.Logging;

namespace Kuyumcu.Services;

/// <summary>Etiket basma sonucu.</summary>
public sealed record LabelPrintResult(bool Success, string Message)
{
    public static LabelPrintResult Ok(string message = "Etiket gönderildi.") => new(true, message);
    public static LabelPrintResult Fail(string message) => new(false, message);
}

/// <summary>
/// Etiket basma: ayarları okur, yerleşimi kurar, yazıcı diline çevirir ve USB (Windows yazıcısı) ya da
/// ağ (TCP) üzerinden gönderir.
/// </summary>
public class LabelPrintService
{
    private readonly StockService _stock;
    private readonly ILabelPrinter _printer;
    private readonly ILogger<LabelPrintService>? _logger;

    public LabelPrintService(StockService stock, ILabelPrinter printer, ILogger<LabelPrintService>? logger = null)
    {
        _stock = stock;
        _printer = printer;
        _logger = logger;
    }

    /// <summary>Bu cihazda etiket basılabilir mi? (Android'de hayır.)</summary>
    public bool IsSupported => _printer.IsSupported;

    public List<string> GetAvailablePrinters()
    {
        try { return _printer.IsSupported ? _printer.GetAvailablePrinters() : new List<string>(); }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Yazıcı listesi alınamadı");
            return new List<string>();
        }
    }

    /// <summary>Ürün etiketini basar ve ürünün baskı sayısını günceller.</summary>
    public async Task<LabelPrintResult> PrintItemAsync(StockItem item, int copies = 1)
    {
        var settings = await _stock.GetLabelSettingsAsync();
        var content = await BuildContentAsync(item);
        var result = await SendAsync(settings, LabelLayout.BuildItemLabel(settings, content, copies), $"Etiket {item.Code}");
        if (result.Success)
            await _stock.RecordLabelPrintedAsync(item.Id);
        return result;
    }

    /// <summary>Ekrandaki önizleme için ürün etiketinin belgesi.</summary>
    public async Task<LabelDocument> BuildItemDocumentAsync(StockItem item, LabelPrinterSettings? settings = null)
    {
        settings ??= await _stock.GetLabelSettingsAsync();
        return LabelLayout.BuildItemLabel(settings, await BuildContentAsync(item));
    }

    public Task<LabelPrintResult> PrintTestAsync(LabelPrinterSettings settings) =>
        SendAsync(settings, LabelLayout.BuildTestLabel(settings), "Test etiketi");

    public Task<LabelPrintResult> PrintAlignmentAsync(LabelPrinterSettings settings) =>
        SendAsync(settings, LabelLayout.BuildAlignmentLabel(settings), "Hizalama etiketi");

    private async Task<LabelContent> BuildContentAsync(StockItem item)
    {
        var category = await _stock.GetCategoryAsync(item.CategoryId);
        return new LabelContent(item.Code, item.WeightMg, item.Karat, category?.ShortName);
    }

    /// <summary>Belgeyi ayarlardaki yazıcıya gönderir.</summary>
    public async Task<LabelPrintResult> SendAsync(LabelPrinterSettings settings, LabelDocument document, string jobName)
    {
        if (!settings.IsConfigured)
            return LabelPrintResult.Fail("Etiket yazıcısı ayarlanmamış (Ayarlar → Etiket Yazıcısı).");

        byte[] data;
        try
        {
            data = LabelLanguages.Get(settings.Language).Render(document);
        }
        catch (Exception ex)
        {
            return LabelPrintResult.Fail($"Etiket hazırlanamadı: {ex.Message}");
        }

        try
        {
            if (settings.Connection == LabelPrinterConnection.Network)
            {
                await SendTcpAsync(settings.NetworkHost!.Trim(), settings.NetworkPort, data, TimeSpan.FromSeconds(5));
            }
            else
            {
                if (!_printer.IsSupported)
                    return LabelPrintResult.Fail("Bu cihazda etiket yazıcısı desteklenmiyor.");
                await _printer.SendRawAsync(settings.PrinterName!, data, jobName);
            }
            return LabelPrintResult.Ok();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Etiket gönderilemedi");
            return LabelPrintResult.Fail($"Etiket yazıcıya gönderilemedi: {ex.Message}");
        }
    }

    /// <summary>Ağ yazıcısına TCP ile ham veri gönderir (TSC/Zebra: 9100 portu).</summary>
    public static async Task SendTcpAsync(string host, int port, byte[] data, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(host, port, cts.Token);
            await using var stream = client.GetStream();
            await stream.WriteAsync(data, cts.Token);
            await stream.FlushAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"{host}:{port} adresine {timeout.TotalSeconds:0} saniyede bağlanılamadı.");
        }
    }
}
