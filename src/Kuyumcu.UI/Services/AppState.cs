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
}
