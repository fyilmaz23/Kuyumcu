using Kuyumcu.Components.Helper;
using Kuyumcu.Models;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Kuyumcu.Services;

/// <summary>
/// Müşteri özetinin kopyalanma/paylaşılma sonucunu belirtir.
/// </summary>
public enum CustomerSummaryCopyResult
{
    /// <summary>Görsel panoya kopyalandı (ör. WhatsApp'a doğrudan yapıştırılabilir).</summary>
    ImageCopied,

    /// <summary>Görsel paylaşım menüsü ile paylaşıma açıldı.</summary>
    ImageShared,

    /// <summary>Görsel kopyalanamadı, özet metin olarak panoya kopyalandı.</summary>
    TextCopied,

    /// <summary>Özet hiçbir şekilde kopyalanamadı.</summary>
    Failed
}

/// <summary>
/// Müşteri detay ekranındaki özet bilgileri (bakiyeler ve emanetler) görsel olarak üretir,
/// panoya kopyalar veya paylaşım menüsüne gönderir.
/// </summary>
public class CustomerSummaryImageService
{
    private const int ImageWidth = 760;
    private const int HorizontalPadding = 36;
    private const int RowHeight = 62;
    private const int RowGap = 10;
    private const int TopPadding = 28;
    private const int BottomPadding = 24;
    private const string FilePrefix = "MusteriOzet_";

    private static readonly SKColor TextColor = new SKColor(0x21, 0x21, 0x21);
    private static readonly SKColor MutedColor = new SKColor(0x75, 0x75, 0x75);
    private static readonly SKColor DebtColor = new SKColor(0xD3, 0x2F, 0x2F);     // Giden
    private static readonly SKColor CreditColor = new SKColor(0x2E, 0x7D, 0x32);   // Gelen
    private static readonly SKColor DebtRowColor = new SKColor(0xF4, 0x43, 0x36, 30);
    private static readonly SKColor CreditRowColor = new SKColor(0x4C, 0xAF, 0x50, 30);
    private static readonly SKColor NeutralRowColor = new SKColor(0x9E, 0x9E, 0x9E, 30);
    private static readonly SKColor DividerColor = new SKColor(0xE0, 0xE0, 0xE0);

    private readonly ILogger<CustomerSummaryImageService> _logger;

    public CustomerSummaryImageService(ILogger<CustomerSummaryImageService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Müşteri özetini görsel olarak üretip panoya kopyalar. Pano görsel kopyalamayı
    /// desteklemiyorsa (mobil platformlar) paylaşım menüsünü açar.
    /// </summary>
    public async Task<CustomerSummaryCopyResult> CopySummaryAsync(
        Customer customer,
        IEnumerable<TransactionGroup>? groups,
        IEnumerable<Transaction>? deposits,
        string? businessName = null)
    {
        if (customer == null)
        {
            return CustomerSummaryCopyResult.Failed;
        }

        var groupList = groups?.ToList() ?? new List<TransactionGroup>();
        var depositList = deposits?.ToList() ?? new List<Transaction>();

        string? filePath = null;
        try
        {
            filePath = CreateSummaryImage(customer, groupList, depositList, businessName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Müşteri özet görseli oluşturulurken hata!");
        }

        if (!string.IsNullOrEmpty(filePath))
        {
            try
            {
                if (await TryCopyImageToClipboardAsync(filePath))
                {
                    return CustomerSummaryCopyResult.ImageCopied;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Özet görseli panoya kopyalanamadı!");
            }

            try
            {
                await Share.RequestAsync(new ShareFileRequest
                {
                    Title = $"{customer.Name} - Hesap Özeti",
                    File = new ShareFile(filePath)
                });
                return CustomerSummaryCopyResult.ImageShared;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Özet görseli paylaşılamadı!");
            }
        }

        // Son çare: özeti düz metin olarak panoya kopyala
        try
        {
            await Clipboard.Default.SetTextAsync(BuildSummaryText(customer, groupList, depositList, businessName));
            return CustomerSummaryCopyResult.TextCopied;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Özet metni panoya kopyalanamadı!");
            return CustomerSummaryCopyResult.Failed;
        }
    }

    /// <summary>
    /// Müşteri özetini PNG olarak üretir ve kaydedilen dosyanın yolunu döndürür.
    /// </summary>
    public string CreateSummaryImage(
        Customer customer,
        List<TransactionGroup> groups,
        List<Transaction> deposits,
        string? businessName = null)
    {
        var content = BuildContent(customer, groups, deposits, businessName);

        using var fonts = new SummaryFonts();

        // Birinci geçiş: yüksekliği hesapla, ikinci geçiş: çiz
        int height = (int)Math.Ceiling(Render(null, null, content, fonts)) + BottomPadding;

        using var surface = SKSurface.Create(new SKImageInfo(ImageWidth, height));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        using var paint = new SKPaint { IsAntialias = true };
        Render(canvas, paint, content, fonts);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        CleanUpOldFiles();

        string fileName = $"{FilePrefix}{customer.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        string filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
        using (var stream = File.Create(filePath))
        {
            data.SaveTo(stream);
        }

        _logger.LogInformation($"Müşteri özet görseli oluşturuldu: {filePath}");
        return filePath;
    }

    /// <summary>
    /// Müşteri özetinin düz metin karşılığını üretir.
    /// </summary>
    public string BuildSummaryText(
        Customer customer,
        List<TransactionGroup> groups,
        List<Transaction> deposits,
        string? businessName = null)
    {
        var content = BuildContent(customer, groups, deposits, businessName);
        var lines = new List<string>();

        if (content.BusinessName != null)
        {
            lines.Add(content.BusinessName);
        }

        lines.Add(content.CustomerName);

        if (content.Phone != null)
        {
            lines.Add(content.Phone);
        }

        lines.Add(content.Date);

        if (content.BalanceRows.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Güncel Durum:");
            lines.AddRange(content.BalanceRows.Select(r => $"{r.Label} {r.Value}"));
        }
        else
        {
            lines.Add(string.Empty);
            lines.Add(NoTransactionText);
        }

        if (content.DepositRows.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Emanetler:");
            lines.AddRange(content.DepositRows.Select(r => $"{r.Label} {r.Value}"));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private const string NoTransactionText = "Bu müşteriye ait işlem kaydı bulunmamaktadır.";

    private static SummaryContent BuildContent(
        Customer customer,
        List<TransactionGroup> groups,
        List<Transaction> deposits,
        string? businessName)
    {
        var balanceRows = groups
            .Select(g => new SummaryRow(
                $"{g.Key.GetDisplayName()}:",
                FormatBalance(g),
                g.Balance == 0 ? RowKind.Neutral : g.Balance > 0 ? RowKind.Debt : RowKind.Credit))
            .ToList();

        var depositRows = deposits
            .GroupBy(t => t.CurrencyType)
            .Select(g => new SummaryRow(
                $"{g.Key.GetDisplayName()}:",
                $"{HelperMethods.FormatPrice(g.Sum(t => t.Amount))} {g.Key.GetSymbol()} (Emanet)",
                RowKind.Neutral))
            .ToList();

        return new SummaryContent(
            string.IsNullOrWhiteSpace(businessName) ? null : businessName!.Trim(),
            customer.Name,
            string.IsNullOrWhiteSpace(customer.PhoneNumber) ? null : $"Tel: {customer.PhoneNumber.FormatPhoneNumber()}",
            DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
            balanceRows,
            depositRows);
    }

    /// <summary>
    /// Özeti çizer. <paramref name="canvas"/> null ise sadece yükseklik hesaplanır.
    /// Her iki durumda da içeriğin bittiği Y konumunu döndürür.
    /// </summary>
    private static float Render(SKCanvas? canvas, SKPaint? paint, SummaryContent content, SummaryFonts fonts)
    {
        float y = TopPadding;
        float left = HorizontalPadding;
        float right = ImageWidth - HorizontalPadding;
        float centerX = ImageWidth / 2f;

        if (content.BusinessName != null)
        {
            y += LineHeight(fonts.Business);
            if (canvas != null && paint != null)
            {
                paint.Style = SKPaintStyle.Fill;
                paint.Color = TextColor;
                canvas.DrawText(content.BusinessName, centerX, y, SKTextAlign.Center, fonts.Business, paint);
            }

            y += 14;
            if (canvas != null && paint != null)
            {
                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = 2;
                paint.Color = DividerColor;
                canvas.DrawLine(left, y, right, y, paint);
                paint.Style = SKPaintStyle.Fill;
            }

            y += 20;
        }

        y += LineHeight(fonts.Name);
        DrawText(canvas, paint, content.CustomerName, left, y, SKTextAlign.Left, fonts.Name, TextColor);
        y += 10;

        if (content.Phone != null)
        {
            y += LineHeight(fonts.Info);
            DrawText(canvas, paint, content.Phone, left, y, SKTextAlign.Left, fonts.Info, MutedColor);
            y += 6;
        }

        y += LineHeight(fonts.Info);
        DrawText(canvas, paint, content.Date, left, y, SKTextAlign.Left, fonts.Info, MutedColor);
        y += 22;

        if (content.BalanceRows.Count > 0)
        {
            y = DrawSection(canvas, paint, "Güncel Durum", y, fonts);
            y = DrawRows(canvas, paint, content.BalanceRows, y, fonts);
        }
        else
        {
            y += LineHeight(fonts.Info);
            DrawText(canvas, paint, NoTransactionText, left, y, SKTextAlign.Left, fonts.Info, MutedColor);
            y += 10;
        }

        if (content.DepositRows.Count > 0)
        {
            y = DrawSection(canvas, paint, "Emanetler", y, fonts);
            y = DrawRows(canvas, paint, content.DepositRows, y, fonts);
        }

        return y;
    }

    private static float DrawSection(SKCanvas? canvas, SKPaint? paint, string title, float y, SummaryFonts fonts)
    {
        y += LineHeight(fonts.Section);
        DrawText(canvas, paint, title, HorizontalPadding, y, SKTextAlign.Left, fonts.Section, TextColor);
        return y + 18;
    }

    private static float DrawRows(SKCanvas? canvas, SKPaint? paint, List<SummaryRow> rows, float y, SummaryFonts fonts)
    {
        float left = HorizontalPadding;
        float right = ImageWidth - HorizontalPadding;

        foreach (var row in rows)
        {
            if (canvas != null && paint != null)
            {
                var rect = new SKRect(left, y, right, y + RowHeight);

                paint.Style = SKPaintStyle.Fill;
                paint.Color = row.Kind switch
                {
                    RowKind.Debt => DebtRowColor,
                    RowKind.Credit => CreditRowColor,
                    _ => NeutralRowColor
                };
                canvas.DrawRoundRect(rect, 8, 8, paint);

                // Satır içeriğini dikey olarak ortala
                var metrics = fonts.RowLabel.Metrics;
                float baseline = rect.MidY - (metrics.Ascent + metrics.Descent) / 2f;

                paint.Color = TextColor;
                canvas.DrawText(row.Label, left + 16, baseline, SKTextAlign.Left, fonts.RowLabel, paint);

                paint.Color = row.Kind switch
                {
                    RowKind.Debt => DebtColor,
                    RowKind.Credit => CreditColor,
                    _ => TextColor
                };
                canvas.DrawText(row.Value, right - 16, baseline, SKTextAlign.Right, fonts.RowValue, paint);
            }

            y += RowHeight + RowGap;
        }

        return y;
    }

    private static void DrawText(SKCanvas? canvas, SKPaint? paint, string text, float x, float y,
        SKTextAlign align, SKFont font, SKColor color)
    {
        if (canvas == null || paint == null)
        {
            return;
        }

        paint.Style = SKPaintStyle.Fill;
        paint.Color = color;
        canvas.DrawText(text, x, y, align, font, paint);
    }

    private static string FormatBalance(TransactionGroup group)
    {
        if (group.Balance == 0)
        {
            return "Hesap Sıfır";
        }

        string amount = HelperMethods.FormatPrice(Math.Abs(group.Balance));
        string direction = group.Balance > 0 ? "Giden" : "Gelen";
        return $"{amount} {group.Key.GetSymbol()} ({direction})";
    }

    private static float LineHeight(SKFont font) => -font.Metrics.Ascent + font.Metrics.Descent;

    private static SKTypeface CreateTypeface(SKFontStyle style)
    {
        // Platforma göre Türkçe karakterleri destekleyen bir font bulmaya çalış
        var families = DeviceInfo.Platform == DevicePlatform.Android
            ? new[] { "sans-serif", "Roboto", "Arial" }
            : new[] { "Segoe UI", "Helvetica Neue", "Arial", "sans-serif" };

        foreach (var family in families)
        {
            var typeface = SKTypeface.FromFamilyName(family, style);
            if (typeface != null)
            {
                return typeface;
            }
        }

        return SKTypeface.CreateDefault();
    }

    private void CleanUpOldFiles()
    {
        try
        {
            foreach (var file in Directory.GetFiles(FileSystem.CacheDirectory, $"{FilePrefix}*.png"))
            {
                if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddHours(-1))
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Eski özet görselleri temizlenemedi.");
        }
    }

    /// <summary>
    /// Görseli panoya kopyalar. Sadece panoya görsel kopyalamayı destekleyen platformlarda
    /// (Windows) başarılı olur, diğer platformlarda false döner.
    /// </summary>
    private async Task<bool> TryCopyImageToClipboardAsync(string filePath)
    {
#if WINDOWS
        return await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(filePath);
            var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage
            {
                RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy
            };
            dataPackage.SetBitmap(Windows.Storage.Streams.RandomAccessStreamReference.CreateFromFile(file));

            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
            // Uygulama kapandıktan sonra da yapıştırılabilmesi için panoyu kalıcı hale getir
            Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
            return true;
        });
#else
        await Task.CompletedTask;
        return false;
#endif
    }

    private enum RowKind
    {
        Neutral,
        Debt,
        Credit
    }

    private record SummaryRow(string Label, string Value, RowKind Kind);

    private record SummaryContent(
        string? BusinessName,
        string CustomerName,
        string? Phone,
        string Date,
        List<SummaryRow> BalanceRows,
        List<SummaryRow> DepositRows);

    /// <summary>
    /// Özet görselinde kullanılan fontlar.
    /// </summary>
    private sealed class SummaryFonts : IDisposable
    {
        private readonly SKTypeface _bold;
        private readonly SKTypeface _regular;

        public SummaryFonts()
        {
            _bold = CreateTypeface(SKFontStyle.Bold);
            _regular = CreateTypeface(SKFontStyle.Normal);

            Business = new SKFont(_bold, 30);
            Name = new SKFont(_bold, 40);
            Section = new SKFont(_bold, 26);
            RowLabel = new SKFont(_bold, 28);
            RowValue = new SKFont(_regular, 28);
            Info = new SKFont(_regular, 24);
        }

        public SKFont Business { get; }
        public SKFont Name { get; }
        public SKFont Section { get; }
        public SKFont RowLabel { get; }
        public SKFont RowValue { get; }
        public SKFont Info { get; }

        public void Dispose()
        {
            Business.Dispose();
            Name.Dispose();
            Section.Dispose();
            RowLabel.Dispose();
            RowValue.Dispose();
            Info.Dispose();
            _bold.Dispose();
            _regular.Dispose();
        }
    }
}
