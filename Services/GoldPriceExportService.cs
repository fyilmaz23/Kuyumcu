using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Kuyumcu.Models;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Kuyumcu.Services;

public class GoldPriceExportService
{
    private readonly ILogger<GoldPriceExportService> _logger;
    private const string BaseUrl = "https://fiyat.ekeo.org.tr";
    private const string LoginUrl = "https://fiyat.ekeo.org.tr/login";
    private const string DashboardUrl = "https://fiyat.ekeo.org.tr/dashboard";

    // Sabit kullanıcı bilgileri
    private const string Code = "1084";
    private const string Password = "12345";

    public GoldPriceExportService(ILogger<GoldPriceExportService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Her istek için yeni bir HttpClient oluşturur
    /// </summary>
    private HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = new CookieContainer(),
            AllowAutoRedirect = true
        };

        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        return client;
    }

    /// <summary>
    /// Altın fiyatlarını web sitesinden çekip döndürür
    /// </summary>
    public async Task<List<GoldPriceDto>> GetGoldPricesAsync()
    {
        using var httpClient = CreateHttpClient();
        try
        {
            _logger.LogInformation("Altın fiyatları çekiliyor...");

            // Siteye giriş yapma
            bool loginSuccess = await LoginToWebsiteAsync(httpClient);
            if (!loginSuccess)
            {
                _logger.LogError("Web sitesine giriş yapılamadı!");
                return null;
            }

            // Dashboard sayfasını çekme
            var dashboardHtml = await httpClient.GetStringAsync(DashboardUrl);
            return ExtractGoldPrices(dashboardHtml);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Altın fiyatları çekilirken hata oluştu!");
            return null;
        }
    }

    /// <summary>
    /// Web sitesine giriş yapar
    /// </summary>
    private async Task<bool> LoginToWebsiteAsync(HttpClient httpClient)
    {
        try
        {
            // Önce login sayfasını çekerek csrf token'ı alalım
            var loginPageResponse = await httpClient.GetStringAsync(LoginUrl);
            var csrfToken = ExtractCsrfToken(loginPageResponse);

            if (string.IsNullOrEmpty(csrfToken))
            {
                _logger.LogError("CSRF token bulunamadı!");
                return false;
            }

            // Form verilerini hazırlama
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("_token", csrfToken),
                new KeyValuePair<string, string>("code", Code),
                new KeyValuePair<string, string>("password", Password)
            });

            // Login isteğini gönderme
            var response = await httpClient.PostAsync(LoginUrl, formContent);
            return response.IsSuccessStatusCode && response.RequestMessage.RequestUri.AbsolutePath.Contains("dashboard");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login işlemi sırasında hata oluştu!");
            return false;
        }
    }

    /// <summary>
    /// HTML içeriğinden CSRF token'ı çıkarır
    /// </summary>
    private string ExtractCsrfToken(string html)
    {
        try
        {
            var csrfMatch = Regex.Match(html, "<input type=\"hidden\" name=\"_token\" value=\"([^\"]+)\"");
            return csrfMatch.Success ? csrfMatch.Groups[1].Value : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Dashboard HTML içeriğinden altın fiyatlarını çıkarır
    /// </summary>
    private List<GoldPriceDto> ExtractGoldPrices(string html)
    {
        var result = new List<GoldPriceDto>();
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(html);

        // Altın fiyat verilerini seçme
        var goldItems = htmlDoc.DocumentNode.SelectNodes("//li[@class='veri']");
        if (goldItems == null || goldItems.Count == 0)
        {
            _logger.LogWarning("HTML içeriğinde altın fiyat bilgisi bulunamadı!");
            return result;
        }

        foreach (var item in goldItems)
        {
            try
            {
                // Altın türü
                var typeNode = item.SelectSingleNode(".//li[@class='v_a']");
                string goldType = typeNode?.InnerText?.Trim();

                if (string.IsNullOrEmpty(goldType))
                    continue;

                // Satış fiyatı
                var sellNode = item.SelectSingleNode(".//li[@class='v_d2 odometer']//span");
                string sellPriceText = sellNode?.InnerText?.Trim().Replace(".", "").Replace(",", ".");

                // 14 Ayar ve 24 Ayar için sadece satış fiyatını kullan
                if (goldType.Contains("14 AYAR") || goldType.Equals("24 AYAR"))
                {
                    // Sadece satış fiyatını kullan (hem alış hem satış için aynı değer)
                    if (!string.IsNullOrEmpty(sellPriceText) && decimal.TryParse(sellPriceText, out decimal sellPrice))
                    {
                        result.Add(new GoldPriceDto(goldType, sellPrice, sellPrice));
                    }
                }
                else
                {
                    // Diğer altın türleri için hem alış hem satış fiyatı kullan
                    var buyNode = item.SelectSingleNode(".//li[@class='v_d1 odometer']//span[not(contains(@class, 'iscilik'))]");
                    string buyPriceText = buyNode?.InnerText?.Trim().Replace(".", "").Replace(",", ".");

                    if (!string.IsNullOrEmpty(buyPriceText) && !string.IsNullOrEmpty(sellPriceText) &&
                        decimal.TryParse(buyPriceText, out decimal buyPrice) &&
                        decimal.TryParse(sellPriceText, out decimal sellPrice))
                    {
                        result.Add(new GoldPriceDto(goldType, buyPrice, sellPrice));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Altın fiyat bilgisi çıkarılırken hata oluştu!");
            }
        }

        return result;
    }

    /// <summary>
    /// Altın fiyatlarını şablon görsel üzerine yerleştirip kaydeder
    /// </summary>
    /// <param name="goldPrices">Altın fiyat listesi</param>
    /// <param name="share">True ise görseli paylaşım için hazırlar</param>
    /// <returns>Kaydedilen görselin yolu</returns>
    public async Task<string> CreateGoldPriceImageAsync(List<GoldPriceDto> goldPrices, bool share = false)
    {
        try
        {
            if (goldPrices == null || !goldPrices.Any())
            {
                _logger.LogError("Altın fiyat listesi boş!");
                return null;
            }
            var assembly = typeof(GoldPriceExportService).Assembly;
            var resourceName = "Kuyumcu.Assets.carsi_kuyumculuk_template.jpg";
            using var stream2 = assembly.GetManifestResourceStream(resourceName);


            // Örneğin SkiaSharp ile bitmap'e çevir:
            using var bitmap = SKBitmap.Decode(stream2);

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
                if (DeviceInfo.Platform == DevicePlatform.Android)
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
                string text = $"{displayText}: {(displayText.Contains("14 Ayar") || displayText.Contains("24 Ayar") ? sellPrice.ToString("N0") : $"{buyPrice:N0}-{sellPrice:N0}")}";
                canvas.DrawText(text, startX, currentY, textPaint);
                currentY += rowHeight;
            }

            // Görseli kaydet
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95);

            // Kayıt dizini
            string saveDir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
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
                await Share.RequestAsync(new ShareFileRequest
                {
                    Title = "Altın Fiyatları",
                    File = new ShareFile(filePath)
                });
            }

            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Altın fiyat görseli oluşturulurken hata!");
            return null;
        }
    }
}
