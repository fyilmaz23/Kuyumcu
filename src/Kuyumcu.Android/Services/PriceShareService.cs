using Kuyumcu.Models;
using Kuyumcu.Platform;
using Kuyumcu.Services;

namespace Kuyumcu.Droid.Services;

/// <summary>
/// Altın fiyatı görselini üretir ve paylaşır. Fiyatlar sayfası ve ana sayfadaki "Fiyat listesini paylaş" düğmesi
/// aynı yolu kullanır: güncel fiyatlar seçili arka plana (telefon > bilgisayar şablonu > sade) yazılır.
/// Hatalar kullanıcıya gösterilecek Türkçe mesajla <see cref="PriceShareException"/> olarak fırlatılır.
/// </summary>
public sealed class PriceShareService(
    BackupStore store,
    GoldPriceExportService goldPrices,
    PriceBackgroundStore background,
    IShareService share)
{
    /// <summary>Güncel fiyatları getirir (son 5 dakikadakiler yeniden kullanılır), görseli üretir ve paylaşım menüsünü açar.</summary>
    public async Task ShareLatestAsync()
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            throw new PriceShareException("İnternet yok; fiyatlar getirilemedi.");

        var prices = await Task.Run(() => goldPrices.GetGoldPricesAsync(forceRefresh: false));
        if (prices is not { Count: > 0 })
            throw new PriceShareException($"Fiyatlar alınamadı ({goldPrices.SourceName}). İnterneti kontrol edip tekrar deneyin.");

        await ShareAsync(await CreateImageAsync(prices));
    }

    /// <summary>Verilen fiyatları seçili arka plana yazar; görselin yolunu döndürür.</summary>
    public async Task<string> CreateImageAsync(List<GoldPriceDto> prices)
    {
        var data = store.Snapshot ?? throw new PriceShareException("Önce yedek yüklenmeli.");
        var (image, _) = background.Resolve(data.Settings.GoldPriceTemplateImage);
        var path = await Task.Run(() => goldPrices.CreateGoldPriceImageAsync(prices, image, data.Settings.BusinessName, share: false));
        return string.IsNullOrEmpty(path) ? throw new PriceShareException("Görsel oluşturulamadı.") : path;
    }

    public async Task ShareAsync(string imagePath)
    {
        if (!await share.ShareFileAsync(imagePath, "Altın Fiyatları"))
            throw new PriceShareException("Paylaşım menüsü açılamadı.");
    }
}

public sealed class PriceShareException(string message) : Exception(message);
