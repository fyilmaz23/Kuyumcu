using Kuyumcu.Components.Dialogs;
using Kuyumcu.Models;
using Kuyumcu.Services;
using MudBlazor;

namespace Kuyumcu.UI.Services;

/// <summary>
/// Ürün detayı ve okutma ekranının ortak işlemleri: satış, iade, kayıp, bulundu, silme.
/// Her işlem onay/açıklama ister ve sonucu bildirir; başarılıysa güncel ürünü döndürür.
/// </summary>
public sealed class StockActions(StockService stock, DatabaseService database, IDialogService dialogs, ISnackbar snackbar)
{
    public async Task<StockItem?> SellAsync(StockItem item, StockLookup lookup)
    {
        var sold = await StockSaleDialog.ShowAsync(dialogs, item, lookup);
        if (sold != null)
            snackbar.Add($"{item.Code} satıldı olarak işaretlendi." + (sold.SaleTransactionId.HasValue ? " Müşteri hesabına yazıldı." : ""), Severity.Success);
        return sold;
    }

    public async Task<StockItem?> ReturnAsync(StockItem item)
    {
        var hasTransaction = item.SaleTransactionId.HasValue && item.SaleCustomerId.HasValue;
        var prompt = await TextPromptDialog.ShowAsync(dialogs, "İade al",
            $"{item.Code} vitrine geri alınacak (aynı kodla).",
            label: "İade açıklaması", confirmText: "İade al",
            checkboxText: hasTransaction ? "Müşteri hesabına yazılan satış işlemini de sil" : null, checkboxValue: hasTransaction);
        if (prompt == null)
            return null;

        return await RunAsync(async () =>
        {
            var transactionId = item.SaleTransactionId;
            var customerId = item.SaleCustomerId;
            var returned = await stock.ReturnAsync(item.Id, string.IsNullOrWhiteSpace(prompt.Text) ? null : prompt.Text);

            if (prompt.Checked && transactionId.HasValue && customerId.HasValue)
            {
                var transaction = (await database.GetCustomerTransactionsAsync(customerId.Value)).FirstOrDefault(t => t.Id == transactionId.Value);
                if (transaction != null)
                    await database.DeleteTransactionAsync(transaction);
            }
            snackbar.Add($"{item.Code} iade alındı, vitrinde.", Severity.Success);
            return returned;
        });
    }

    public async Task<StockItem?> MarkLostAsync(StockItem item)
    {
        var prompt = await TextPromptDialog.ShowAsync(dialogs, "Kayıp olarak işaretle",
            $"{item.Code} vitrin toplamından düşülecek. Bulunursa geri alınabilir.",
            label: "Açıklama", confirmText: "Kayıp yap", confirmColor: Color.Error);
        if (prompt == null)
            return null;
        return await RunAsync(async () =>
        {
            var lost = await stock.MarkLostAsync(item.Id, null, string.IsNullOrWhiteSpace(prompt.Text) ? null : prompt.Text);
            snackbar.Add($"{item.Code} kayıp olarak işaretlendi.", Severity.Warning);
            return lost;
        });
    }

    public Task<StockItem?> MarkFoundAsync(StockItem item) => RunAsync(async () =>
    {
        var found = await stock.MarkFoundAsync(item.Id, null);
        snackbar.Add($"{item.Code} bulundu, vitrine geri alındı.", Severity.Success);
        return found;
    });

    public async Task<StockItem?> DeleteAsync(StockItem item)
    {
        var prompt = await TextPromptDialog.ShowAsync(dialogs, "Ürünü sil",
            $"{item.Code} silinecek. Kod bir daha kullanılmaz; hareket geçmişi saklanır.",
            label: "Silme nedeni", required: true, confirmText: "Sil", confirmColor: Color.Error);
        if (prompt == null)
            return null;
        return await RunAsync(async () =>
        {
            var deleted = await stock.DeleteItemAsync(item.Id, prompt.Text!);
            snackbar.Add($"{item.Code} silindi.", Severity.Info);
            return deleted;
        });
    }

    private async Task<StockItem?> RunAsync(Func<Task<StockItem>> action)
    {
        try
        {
            return await action();
        }
        catch (StockException ex)
        {
            snackbar.Add(ex.Message, Severity.Warning);
            return null;
        }
    }
}
