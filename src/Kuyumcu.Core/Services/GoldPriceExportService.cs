using Kuyumcu.Models;
using Kuyumcu.Platform;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Kuyumcu.Services;

public class GoldPriceExportService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly ILogger<GoldPriceExportService> _logger;
    private readonly IAppEnvironment _environment;
    private readonly IShareService _share;
    private readonly IGoldPriceSource _source;

    private List<GoldPriceDto>? _cache;
    private DateTime _cacheTime;

    public GoldPriceExportService(ILogger<GoldPriceExportService> logger, IAppEnvironment environment, IShareService share, IGoldPriceSource source)
    {
        _logger = logger;
        _environment = environment;
        _share = share;
        _source = source;
    }

    /// <summary>Fiyatların alındığı kaynağın adı (ekranda gösterilir).</summary>
    public string SourceName => _source.DisplayName;

    /// <summary>
    /// Güncel altın fiyatlarını döndürür (herkese açık kaynaktan; üyelik/şifre gerekmez).
    /// Son 5 dakikada alınan fiyatlar yeniden kullanılır. Alınamazsa null döner.
    /// </summary>
    public async Task<List<GoldPriceDto>?> GetGoldPricesAsync(bool forceRefresh = false)
    {
        if (!forceRefresh && _cache != null && DateTime.Now - _cacheTime < CacheDuration)
            return _cache;

        _logger.LogInformation("Altın fiyatları çekiliyor: {Source}", _source.DisplayName);
        var prices = await _source.FetchAsync();
        if (prices is { Count: > 0 })
        {
            _cache = prices;
            _cacheTime = DateTime.Now;
        }
        return prices;
    }

    /// <summary>Has altın (24 ayar, gram) satış fiyatı. Satış fiyatı önerisinde kullanılır.</summary>
    public async Task<GoldPriceDto?> GetHasGoldPriceAsync(bool forceRefresh = false)
    {
        var prices = await GetGoldPricesAsync(forceRefresh);
        return prices?.FirstOrDefault(p => p.Type.Trim().Equals("24 Ayar", StringComparison.OrdinalIgnoreCase))
               ?? prices?.FirstOrDefault(p => p.Type.Contains("24", StringComparison.Ordinal) && !p.Type.Contains("Gram", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Görseldeki bir satır: "22 Ayar: 5.415-5.960". Kaynak ayrı bir alış fiyatı vermiyorsa
    /// (alış = satış, örn. 14 Ayar) yalnızca satış yazılır: "14 Ayar: 5.010".
    /// </summary>
    public static string FormatPriceLine(string displayName, decimal buyPrice, decimal sellPrice)
        => buyPrice > 0 && buyPrice != sellPrice
            ? $"{displayName}: {buyPrice:N0}-{sellPrice:N0}"
            : $"{displayName}: {sellPrice:N0}";

    /// <summary>
    /// Altın fiyatlarını şablon görsel üzerine yerleştirip kaydeder
    /// </summary>
    /// <param name="goldPrices">Altın fiyat listesi</param>
    /// <param name="share">True ise görseli paylaşım için hazırlar</param>
    /// <returns>Kaydedilen görselin yolu</returns>
    /// <param name="templateImage">İşletmeye özel arka plan şablonu; null ise sade bir varsayılan arka plan üretilir</param>
    /// <param name="businessName">Varsayılan arka planda başlık olarak yazılacak işletme adı</param>
    public async Task<string> CreateGoldPriceImageAsync(List<GoldPriceDto> goldPrices, byte[]? templateImage, string? businessName, bool share = false)
    {
        try
        {
            if (goldPrices == null || !goldPrices.Any())
            {
                _logger.LogError("Altın fiyat listesi boş!");
                return null;
            }
            // Arka plan: yüklenen/seçilen resim (yönü düzeltilmiş, 1080 px genişliğe getirilmiş); okunamazsa sade varsayılan
            using var bitmap = (templateImage is { Length: > 0 } ? LoadTemplate(templateImage) : null)
                               ?? CreateDefaultTemplate(businessName);

            using var surface = SKSurface.Create(new SKImageInfo(bitmap.Width, bitmap.Height));
            var canvas = surface.Canvas;

            // Şablonu çiz
            canvas.DrawBitmap(bitmap, 0, 0);

            // İstenen formatta metin için stil oluştur (Gotham fontu, 49 punto, #644e3a renk, kalın)
            using var textPaint = new SKPaint
            {
                Color = new SKColor(0x64, 0x4E, 0x3A), // #644e3a kahverengi tonu
                TextSize = 67,
                IsAntialias = true,
                Typeface = SKTypeface.FromFamilyName("Gotham", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
            };

            // Gotham fontu yoksa, Arial veya başka bir sistem fontunu yedek olarak kullan
            if (textPaint.Typeface == null || textPaint.Typeface.FamilyName != "Gotham")
            {
                textPaint.Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);

                // Android için sans-serif dene
                if (_environment.IsMobile)
                {
                    textPaint.Typeface = SKTypeface.FromFamilyName("sans-serif", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
                }
            }

            // Fiyat verileri için başlangıç Y konumu - şablon görselin orta kısmı
            float startX = bitmap.Width * 0.2f; // Sol kenardan %20 içeride
            float currentY = bitmap.Height * 0.25f; // Üst kenardan %33 aşağıda
            float rowHeight = textPaint.TextSize * 1.66f; // Line spacing 1.66 oranında (font büyüklüğüne göre)

            // Altın fiyatlarını ilgili isimleriyle eşleştirelim
            // Bu listeyi tam olarak istenen sırada dolduracağız
            var displayItems = new List<(string DisplayName, decimal BuyPrice, decimal SellPrice)>();

            // İstenen sıra - kesin isimlendirme ve sıra
            var goldTypeOrder = new[]
            {
                "24 Ayar",
                "22 Ayar",
                "14 Ayar",
                "Beşli",
                "Ata",
                "Yarım",
                "Çeyrek",
                "Gram" // Gram olarak gösterilecek
            };


            // 24 AYAR 1 GRAM için doğru altın nesnesini bulalım
            var gramGold = goldPrices.FirstOrDefault(g => g.Type.Contains("GRAM", StringComparison.OrdinalIgnoreCase));

            // Her bir sıralama için ilgili altın tipini bulup listeye ekleyelim
            foreach (var orderType in goldTypeOrder)
            {
                if (orderType == "Gram" && gramGold != null)
                {
                    // Gram için özel durum - 24 AYAR 1 GRAM tipini al
                    displayItems.Add(("Gram", gramGold.BuyPrice, gramGold.SellPrice));
                    continue;
                }

                // Altın tiplerini daha esnek arama
                bool found = false;
                
                // Özel durumlar
                if (orderType == "Beşli")
                {
                    var besl = goldPrices.FirstOrDefault(g => 
                        g.Type.Contains("beşl", StringComparison.OrdinalIgnoreCase) || 
                        g.Type.Contains("besli", StringComparison.OrdinalIgnoreCase));
                    
                    if (besl != null)
                    {
                        displayItems.Add((orderType, besl.BuyPrice, besl.SellPrice));
                        found = true;
                    }
                }
                else if (orderType == "Yarım")
                {
                    var yarim = goldPrices.FirstOrDefault(g => 
                        g.Type.Contains("yarım", StringComparison.OrdinalIgnoreCase) ||
                        g.Type.Contains("yarim", StringComparison.OrdinalIgnoreCase));
                    
                    if (yarim != null)
                    {
                        displayItems.Add((orderType, yarim.BuyPrice, yarim.SellPrice));
                        found = true;
                    }
                }
                
                // Genel eşleştirme
                if (!found)
                {
                    var matchingGold = goldPrices.FirstOrDefault(g =>
                        g.Type.Contains(orderType, StringComparison.OrdinalIgnoreCase));

                    if (matchingGold != null)
                    {
                        displayItems.Add((orderType=="Ata"?"Tam":orderType, matchingGold.BuyPrice, matchingGold.SellPrice));
                    }
                }
            }

            // Altın fiyatlarını istenen formatta yazdır
            foreach (var (displayText, buyPrice, sellPrice) in displayItems)
            {
                canvas.DrawText(FormatPriceLine(displayText, buyPrice, sellPrice), startX, currentY, textPaint);
                currentY += rowHeight;
            }

            // Görseli kaydet
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95);

            // Kayıt dizini
            string saveDir = _environment.PicturesDirectory;
            Directory.CreateDirectory(saveDir); // Dizin yoksa oluştur

            // Dosya adı
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"AltinFiyatlari_{timestamp}.jpg";
            string filePath = Path.Combine(saveDir, fileName);

            // Dosyaya kaydet
            using (var stream = File.OpenWrite(filePath))
            {
                data.SaveTo(stream);
            }

            _logger.LogInformation($"Altın fiyat görseli kaydedildi: {filePath}");

            // Eğer paylaşım isteniyorsa
            if (share)
            {
                await _share.ShareFileAsync(filePath, "Altın Fiyatları");
            }

            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Altın fiyat görseli oluşturulurken hata!");
            return null;
        }
    }

    /// <summary>
    /// Fiyatlar arka plana göre konumlanır (sol %20, üst %25) ve yazı boyutu sabittir (67 px); bu ölçüler 1080 px
    /// genişliğindeki şablona göredir. Bu yüzden her arka plan bu genişliğe getirilir.
    /// </summary>
    public const int TemplateWidth = 1080;

    /// <summary>
    /// Arka plan resmini çizime hazırlar: telefon fotoğraflarındaki EXIF yönü uygulanır ve en-boy oranı korunarak
    /// genişlik <see cref="TemplateWidth"/> yapılır (1080 px şablonlar aynen kalır). Resim okunamazsa null.
    /// </summary>
    public static SKBitmap? LoadTemplate(byte[] imageData)
    {
        try
        {
            using var codec = SKCodec.Create(new SKMemoryStream(imageData));
            if (codec == null)
                return null;

            var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var decoded = new SKBitmap(info);
            var result = codec.GetPixels(info, decoded.GetPixels());
            if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
                return null;

            using var oriented = ApplyOrientation(decoded, codec.EncodedOrigin);
            if (oriented.Width == TemplateWidth)
                return oriented.Copy();

            var height = Math.Max(1, (int)Math.Round(oriented.Height * (double)TemplateWidth / oriented.Width));
            return oriented.Resize(new SKImageInfo(TemplateWidth, height, SKColorType.Rgba8888, SKAlphaType.Premul),
                new SKSamplingOptions(SKCubicResampler.Mitchell));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Seçilen arka plan resmini saklamaya uygun hale getirir (yönü düzeltilmiş, 1080 px genişlik, JPEG).
    /// Telefonda seçilen büyük fotoğraflar böylece küçük yer kaplar. Resim okunamazsa null.
    /// </summary>
    public static byte[]? NormalizeTemplate(byte[] imageData)
    {
        using var bitmap = LoadTemplate(imageData);
        if (bitmap == null)
            return null;
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 92);
        return data?.ToArray();
    }

    private static SKBitmap ApplyOrientation(SKBitmap source, SKEncodedOrigin origin)
    {
        // Yatay/dikey aynalı (nadir) yönler döndürme olarak ele alınır
        var degrees = origin switch
        {
            SKEncodedOrigin.BottomRight or SKEncodedOrigin.BottomLeft => 180,
            SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom => 90,
            SKEncodedOrigin.LeftBottom or SKEncodedOrigin.LeftTop => 270,
            _ => 0,
        };
        if (degrees == 0)
            return source.Copy();

        var swap = degrees != 180;
        var rotated = new SKBitmap(swap ? source.Height : source.Width, swap ? source.Width : source.Height);
        using var canvas = new SKCanvas(rotated);
        canvas.Translate(rotated.Width / 2f, rotated.Height / 2f);
        canvas.RotateDegrees(degrees);
        canvas.Translate(-source.Width / 2f, -source.Height / 2f);
        canvas.DrawBitmap(source, 0, 0);
        return rotated;
    }

    /// <summary>
    /// İşletme şablon yüklemediyse kullanılan sade arka plan (1080x1350, krem zemin, altın çerçeve).
    /// </summary>
    private static SKBitmap CreateDefaultTemplate(string? businessName)
    {
        const int width = 1080, height = 1350;
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(0xFB, 0xF7, 0xEE));

        using var frame = new SKPaint { Color = new SKColor(0xB8, 0x86, 0x0B), Style = SKPaintStyle.Stroke, StrokeWidth = 12, IsAntialias = true };
        canvas.DrawRoundRect(new SKRect(30, 30, width - 30, height - 30), 24, 24, frame);

        using var titleFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) ?? SKTypeface.Default, 64);
        using var subFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal) ?? SKTypeface.Default, 40);
        using var titlePaint = new SKPaint { Color = new SKColor(0x64, 0x4E, 0x3A), IsAntialias = true };

        var title = string.IsNullOrWhiteSpace(businessName) ? "Güncel Altın Fiyatları" : businessName!;
        canvas.DrawText(title, width / 2f, 150, SKTextAlign.Center, titleFont, titlePaint);
        if (!string.IsNullOrWhiteSpace(businessName))
            canvas.DrawText("Güncel Altın Fiyatları", width / 2f, 215, SKTextAlign.Center, subFont, titlePaint);
        canvas.DrawText(DateTime.Now.ToString("dd.MM.yyyy HH:mm"), width / 2f, height - 80, SKTextAlign.Center, subFont, titlePaint);

        return bitmap;
    }
}
