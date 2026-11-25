namespace Kuyumcu.Droid.Services;

public enum CustomerFilter
{
    All,
    /// <summary>En az bir para biriminde Giden bakiyesi olan (müşteri borçlu).</summary>
    Giden,
    /// <summary>En az bir para biriminde Gelen bakiyesi olan (müşteri alacaklı).</summary>
    Gelen,
}

public enum StockView
{
    /// <summary>Vitrindeki etiketli ürünler.</summary>
    InStock,
    Sold,
    Lost,
    /// <summary>Kasa (etiketsiz) ürünleri.</summary>
    Bulk,
}

/// <summary>Sayfalar arasında korunan ekran durumu (müşteri detayından listeye dönünce arama kaybolmasın).</summary>
public sealed class UiState
{
    public string CustomerSearch { get; set; } = string.Empty;
    public CustomerFilter CustomerFilter { get; set; } = CustomerFilter.All;

    public string StockSearch { get; set; } = string.Empty;
    public StockView StockView { get; set; } = StockView.InStock;
    /// <summary>Vitrin toplamında dokunulan ayar; null = tüm ayarlar.</summary>
    public int? StockKarat { get; set; }
}
