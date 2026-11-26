using Kuyumcu.Services;

namespace Kuyumcu.UI.Services;

/// <summary>
/// Müşteri detayındaki önceki/sonraki gezintisinin sırası. Müşteriler sayfası (ya da ana sayfadaki bakiye listesi)
/// süzülmüş listeyi buraya yazar; detay sayfası açılan müşteri bu listedeyse onun sırasıyla gezer, değilse
/// bütün müşterilerde alfabetik sırayla. Müşteriler sayfasının süzgeci de burada saklanır (geri dönünce korunur).
/// </summary>
public class CustomerBrowseContext
{
    /// <summary>Müşteriler sayfasının son süzgeci.</summary>
    public CustomerListFilter Filter { get; set; } = new();

    /// <summary>Müşteriler sayfasının son sayfa numarası ve sayfa boyu.</summary>
    public int Page { get; set; }
    public int PageSize { get; set; } = 25;

    /// <summary>Gezinti listesi (sıralı müşteri kimlikleri); null ise alfabetik tüm müşteriler.</summary>
    public IReadOnlyList<int>? Ids { get; private set; }

    /// <summary>Gezinti listesinin kısa adı (ör. "Euro · Giden").</summary>
    public string? Label { get; private set; }

    public void SetList(IReadOnlyList<int> ids, string? label)
    {
        Ids = ids;
        Label = string.IsNullOrWhiteSpace(label) ? null : label;
    }

    public void Clear()
    {
        Ids = null;
        Label = null;
    }
}
