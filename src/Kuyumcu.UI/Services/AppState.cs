namespace Kuyumcu.UI.Services;

/// <summary>
/// Uygulama genelinde paylaşılan arayüz durumu (yönlendirme kararları için).
/// </summary>
public class AppState
{
    /// <summary>Durum veritabanından yüklendi mi?</summary>
    public bool IsLoaded { get; set; }

    /// <summary>İlk kurulum tamamlandı mı?</summary>
    public bool IsSetupCompleted { get; set; }

    /// <summary>Deneme süresi dolmuş ve lisans yok mu?</summary>
    public bool IsLicenseExpired { get; set; }

    /// <summary>Ek ücretli barkod modülü (vitrin stoğu, etiket, okutma, sayım, kasa) açık mı?</summary>
    public bool HasBarcodeModule { get; set; }

    /// <summary>Lisans durumundan yönlendirme bilgilerini günceller (açılışta ve anahtar girilince).</summary>
    public void ApplyLicense(Kuyumcu.Models.LicenseState state)
    {
        IsLicenseExpired = state.Status == Kuyumcu.Models.LicenseStatus.Expired;
        HasBarcodeModule = state.HasBarcodeModule;
    }

    /// <summary>Lisans etkinleştirildiğinde tetiklenir (üst menüdeki rozet yenilenir).</summary>
    public event Action? LicenseChanged;

    public void NotifyLicenseChanged() => LicenseChanged?.Invoke();
}
