using Kuyumcu.Helpers;
using Kuyumcu.Models;
using SQLite;

namespace Kuyumcu.Stock;

/// <summary>
/// Müşteri defteri → Kasa (kullanıcı kararı, 26.11.2026): müşteriden gelen sarrafiye, döviz, TL ve gram altın kasaya
/// girer, müşteriye giden kasadan çıkar. Emanet hiçbir zaman işlenmez. Hesap sıfırlama da normal işlem gibi işlenir.
///
/// Kurallar:
///  - Sadece <see cref="Transaction.AffectsCash"/> açık işlemler (defter ekranından girilen yeniler; eski kayıtlar ve
///    stok ekranlarının açtığı veresiye işlemleri kapalı — onların kasa hareketini stok ekranı yazar).
///  - Cinsin kasa ürünü <see cref="BulkProduct.LedgerCurrency"/> ile bulunur; ürün silinmişse işlenmez.
///  - Bir işlemin kasadaki etkisi = o işleme bağlı (<see cref="StockMovement.FromLedger"/>) hareketlerin toplamı.
///    İşlem eklenince/düzenlenince/silinince istenen etkiyle fark hesaplanır ve fark kadar hareket yazılır
///    (geçmiş silinmez). Kasa eksiye düşebilir (açılış miktarı "Sayım" ile girilir).
///  - Defter işlemiyle aynı SQLite transaction'ında çalışır: ikisi birlikte yazılır ya da hiçbiri.
/// Saf kısımları birim testli.
/// </summary>
public static class LedgerCash
{
    /// <summary>Defter tutarını kasa birimine çevirir: gram → mg, adet → adet, para → kuruş.</summary>
    public static long ToUnits(BulkProduct product, decimal amount) => product.Unit switch
    {
        BulkUnit.Gram => (long)Math.Round(amount * 1000m, MidpointRounding.AwayFromZero),
        BulkUnit.Money => (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero),
        _ => (long)Math.Round(amount, MidpointRounding.AwayFromZero),
    };

    /// <summary>İşlemin kasada olması gereken etkisi (Gelen +, Giden −); işlenmeyecekse 0.</summary>
    public static long DesiredDelta(Transaction t, BulkProduct product)
    {
        if (t.IsDeleted || t.IsDeposit || !t.AffectsCash || product.IsDeleted || product.LedgerCurrency != (int)t.CurrencyType)
            return 0;
        var units = ToUnits(product, t.Amount);
        return t.Type == TransactionType.StoreDebt ? units : -units;
    }

    /// <summary>
    /// İşlemin kasa etkisini eşitler. <see cref="SQLiteConnection.RunInTransaction"/> içinde çağrılır (işlem zaten
    /// kaydedilmiş ve Id'si dolu olmalı). Yazılan hareket sayısını döndürür.
    /// </summary>
    public static int Sync(SQLiteConnection conn, Transaction t)
    {
        if (t.Id == 0)
            return 0;

        var products = conn.Table<BulkProduct>().ToList();
        var transactionId = t.Id;
        var applied = conn.Table<StockMovement>()
            .Where(m => m.TransactionId == transactionId && m.FromLedger)
            .ToList()
            .GroupBy(m => m.BulkProductId)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.AmountDelta));

        var target = products.FirstOrDefault(p => p.LedgerCurrency == (int)t.CurrencyType && !p.IsDeleted);
        var desired = new Dictionary<int, long>();
        if (target != null)
        {
            var delta = DesiredDelta(t, target);
            if (delta != 0)
                desired[target.Id] = delta;
        }

        var written = 0;
        string? customerName = null;
        foreach (var productId in applied.Keys.Union(desired.Keys))
        {
            var diff = desired.GetValueOrDefault(productId) - applied.GetValueOrDefault(productId);
            if (diff == 0)
                continue;
            var product = products.FirstOrDefault(p => p.Id == productId);
            if (product == null)
                continue;

            customerName ??= conn.Find<Customer>(t.CustomerId)?.Name ?? "müşteri";
            var first = !applied.ContainsKey(productId);
            var movement = new StockMovement
            {
                BulkProductId = productId,
                Type = diff > 0 ? StockMovementType.BulkIn : StockMovementType.BulkOut,
                Date = DateTime.Now,
                AmountDelta = diff,
                Description = Describe(t, customerName, first),
                CustomerId = t.CustomerId,
                TransactionId = t.Id,
                FromLedger = true,
            };
            conn.Insert(movement);
            conn.Execute("UPDATE BulkProduct SET Amount = Amount + ? WHERE Id = ?", diff, productId);
            written++;
        }
        return written;
    }

    private static string Describe(Transaction t, string customerName, bool first)
    {
        string text;
        if (first && !t.IsDeleted)
            text = $"Defter: {customerName} · {(t.Type == TransactionType.StoreDebt ? "Gelen" : "Giden")} " +
                   $"{FormatHelper.FormatPrice(t.Amount)} {t.CurrencyType.GetShortName()}" +
                   (string.IsNullOrWhiteSpace(t.Description) ? "" : $" · {t.Description.Trim()}");
        else if (t.IsDeleted)
            text = $"Defter işlemi silindi: {customerName}";
        else
            text = $"Defter işlemi düzeltildi: {customerName}";
        return text.Length > 255 ? text[..255] : text;
    }

    /// <summary>Para biriminin kısa sembolü (kasa ürünü para ise).</summary>
    public static string MoneySymbol(BulkProduct product) =>
        product.LedgerCurrencyType is { } c ? c.GetSymbol() : "₺";
}
