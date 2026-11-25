using Kuyumcu.Models;

namespace Kuyumcu.Stock;

/// <summary>Stok durumlarının ve hareketlerinin ekrandaki adları (masaüstü ve Android ortak).</summary>
public static class StockText
{
    public static string StatusName(StockItemStatus status) => status switch
    {
        StockItemStatus.Sold => "Satıldı",
        StockItemStatus.Lost => "Kayıp",
        _ => "Vitrinde"
    };

    public static string MovementName(StockMovementType type) => type switch
    {
        StockMovementType.Entry => "Giriş",
        StockMovementType.Sale => "Satış",
        StockMovementType.Return => "İade",
        StockMovementType.Edit => "Düzeltme",
        StockMovementType.Delete => "Silme",
        StockMovementType.Lost => "Kayıp (sayım farkı)",
        StockMovementType.Found => "Bulundu",
        StockMovementType.BulkIn => "Giriş",
        StockMovementType.BulkOut => "Çıkış",
        StockMovementType.BulkAdjust => "Sayım düzeltmesi",
        _ => type.ToString()
    };
}
