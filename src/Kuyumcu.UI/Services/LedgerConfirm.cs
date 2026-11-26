using Kuyumcu.Helpers;
using Kuyumcu.Models;
using Kuyumcu.Services;
using MudBlazor;

namespace Kuyumcu.UI.Services;

/// <summary>
/// Stok ekranlarından deftere işlem yazılmadan önce defterin kuralları (müşteri detayındaki işlem penceresiyle aynı):
/// aynı gün mükerrer kontrolü, sonra kaydetmeden önce onay. Etiketli ürün satışı ve kasa satışı bunu kullanır.
/// </summary>
public static class LedgerConfirm
{
    /// <summary>Yeni işlem için mükerrer uyarısı ve onay. Kullanıcı ikisini de onaylarsa true.</summary>
    public static async Task<bool> ConfirmNewAsync(IDialogService dialogs, DatabaseService database, Customer customer, Transaction transaction)
    {
        var existing = await database.GetCustomerTransactionsAsync(customer.Id);
        var duplicate = existing.FirstOrDefault(t => t.Date.Date == transaction.Date.Date && t.Type == transaction.Type
            && t.CurrencyType == transaction.CurrencyType && t.Amount == transaction.Amount && t.IsDeposit == transaction.IsDeposit);
        if (duplicate != null)
        {
            var dup = await dialogs.ShowMessageBox("Mükerrer işlem uyarısı",
                $"Bunu bugün saat {duplicate.Date:HH:mm}'de zaten eklemiştiniz, yine de eklenmesini ister misiniz?",
                yesText: "Evet, yine de ekle", noText: "Hayır, iptal et");
            if (dup != true)
                return false;
        }

        var kind = transaction.Type == TransactionType.CustomerDebt ? "Giden" : "Gelen";
        var confirm = await dialogs.ShowMessageBox("Aşağıdaki işlemi kaydetmek istediğinize emin misiniz?",
            $"{customer.Name}\n{transaction.Date:dd.MM.yyyy HH:mm}\n{kind}: {FormatHelper.FormatPrice(transaction.Amount)} " +
            $"{transaction.CurrencyType.GetSymbol()} {transaction.CurrencyType.GetDisplayName()}\n{transaction.Description}",
            yesText: "Evet, kaydet", noText: "Hayır");
        return confirm == true;
    }
}
