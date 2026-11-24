using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Kuyumcu.Models;
using Microsoft.Extensions.Logging;

namespace Kuyumcu.Services;

/// <summary>
/// Canlı altın fiyatı kaynağı. Ürün başka şehirlere satıldığında her şehrin kaynağı ayrı bir uygulama olarak eklenir.
/// </summary>
public interface IGoldPriceSource
{
    /// <summary>Kaynağın kullanıcıya gösterilen adı.</summary>
    string DisplayName { get; }

    /// <summary>Güncel fiyatları çeker. Alınamazsa null döner.</summary>
    Task<List<GoldPriceDto>?> FetchAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Elazığ altın fiyatları: https://elazigaltinfiyatlari.com/ (herkese açık, giriş gerekmez).
/// Sayfadaki "BUGÜN" tablosu okunur. Satır adları: 24 Ayar, 22 Ayar, 14 Ayar, Beşli, Tam (Ata), Yarım, Çeyrek, Gram Altın.
/// 14 Ayar için sadece satış fiyatı verilir (alış = satış kabul edilir).
/// </summary>
public sealed class ElazigAltinFiyatlariSource : IGoldPriceSource
{
    public const string Url = "https://elazigaltinfiyatlari.com/";
    private static readonly HttpClient Http = CreateHttpClient();
    private readonly ILogger<ElazigAltinFiyatlariSource>? _logger;

    public ElazigAltinFiyatlariSource(ILogger<ElazigAltinFiyatlariSource>? logger = null)
    {
        _logger = logger;
    }

    public string DisplayName => "elazigaltinfiyatlari.com";

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (KuyumcuDefteri)");
        return client;
    }

    public async Task<List<GoldPriceDto>?> FetchAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var html = await Http.GetStringAsync(Url, cancellationToken);
            var prices = Parse(html, DateTime.Now);
            if (prices.Count == 0)
                _logger?.LogWarning("Fiyat sayfasında fiyat tablosu bulunamadı.");
            return prices;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Altın fiyatları alınamadı");
            return null;
        }
    }

    /// <summary>Sayfa HTML'inden fiyatları çıkarır (test edilebilsin diye ayrı).</summary>
    public static List<GoldPriceDto> Parse(string html, DateTime now)
    {
        var result = new List<GoldPriceDto>();
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Önce "tahta" içindeki günlük tablo; yoksa başlığında Alış/Satış geçen ilk tablo
        var table = doc.DocumentNode.SelectSingleNode("//div[contains(concat(' ', normalize-space(@class), ' '), ' tahta ')]//table")
                    ?? doc.DocumentNode.SelectSingleNode("//table[.//th[contains(., 'Alış')] and .//th[contains(., 'Satış')]]");
        if (table == null)
            return result;

        var updateTime = ParseUpdateTime(doc, now);

        foreach (var row in table.SelectNodes(".//tr") ?? Enumerable.Empty<HtmlNode>())
        {
            var nameCell = row.SelectSingleNode("./td[contains(@class,'ad')]");
            if (nameCell == null)
                continue;

            var nameNode = nameCell.SelectSingleNode(".//a") ?? nameCell;
            var name = WebUtility.HtmlDecode(nameNode.InnerText).Trim();
            if (string.IsNullOrEmpty(name))
                continue;

            var sell = ParsePrice(row.SelectSingleNode(".//span[contains(@class,'satis')]")?.InnerText);
            var buy = ParsePrice(row.SelectSingleNode(".//span[contains(@class,'alis')]")?.InnerText);
            if (sell == null)
                continue;

            result.Add(new GoldPriceDto(name, buy ?? sell.Value, sell.Value) { UpdateTime = updateTime });
        }

        return result;
    }

    /// <summary>"6.160 ₺" → 6160, "5.140,50" → 5140.50</summary>
    public static decimal? ParsePrice(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var cleaned = Regex.Replace(WebUtility.HtmlDecode(text), @"[^\d,\.]", "");
        cleaned = cleaned.Replace(".", "").Replace(",", ".");
        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : null;
    }

    private static DateTime ParseUpdateTime(HtmlDocument doc, DateTime now)
    {
        // "Son güncelleme <b class="tab">16:47</b>"
        var node = doc.DocumentNode.SelectSingleNode("//*[contains(@class,'canli')]//b");
        if (node != null && TimeSpan.TryParseExact(node.InnerText.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var time))
            return now.Date + time;
        return now;
    }
}
