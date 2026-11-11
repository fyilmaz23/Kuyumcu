namespace Kuyumcu.Platform;

/// <summary>
/// Uygulamanın çalıştığı ortam hakkında bilgiler.
/// Windows (WPF) ve Android (MAUI) projeleri kendi uygulamalarını sağlar.
/// </summary>
public interface IAppEnvironment
{
    /// <summary>Kalıcı uygulama verilerinin klasörü (veritabanı, yedekler).</summary>
    string AppDataDirectory { get; }

    /// <summary>
    /// Günlük otomatik yerel yedeklerin klasörü. Windows'ta Belgeler altında tutulur ki
    /// uygulama kaldırılsa bile yedekler silinmesin.
    /// </summary>
    string BackupDirectory { get; }

    /// <summary>Geçici dosyalar için klasör.</summary>
    string CacheDirectory { get; }

    /// <summary>Kullanıcının resimler klasörü (dışa aktarılan görseller için).</summary>
    string PicturesDirectory { get; }

    /// <summary>
    /// Salt okunur istemci mi? (Android: sadece görüntüleme, veri girişi yok.)
    /// </summary>
    bool IsReadOnlyClient { get; }

    /// <summary>Mobil cihaz mı? (Font ve yerleşim seçimleri için.)</summary>
    bool IsMobile { get; }

    /// <summary>Cihazı tanımlayan, zamanla değişmeyen bir metin (lisans cihaz kimliği için).</summary>
    string GetDeviceFingerprint();

    /// <summary>Ana veritabanı dosyasının tam yolu.</summary>
    string DatabasePath => Path.Combine(AppDataDirectory, ProductInfo.DatabaseFileName);
}

/// <summary>Şifreli anahtar/değer deposu (lisans deneme süresi koruması vb.).</summary>
public interface ISecureStore
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
}

/// <summary>Dosya seçme penceresi.</summary>
public interface IFilePickerService
{
    /// <summary>
    /// Kullanıcıya dosya seçtirir. İptal edilirse null döner.
    /// </summary>
    /// <param name="title">Pencere başlığı</param>
    /// <param name="extensions">İzin verilen uzantılar (örn: ".db3")</param>
    Task<PickedFile?> PickFileAsync(string title, params string[] extensions);
}

/// <summary>Seçilen dosya.</summary>
public sealed record PickedFile(string FileName, string FullPath);

/// <summary>Pano işlemleri.</summary>
public interface IClipboardService
{
    Task SetTextAsync(string text);

    /// <summary>Görseli panoya kopyalar. Platform desteklemiyorsa false döner.</summary>
    Task<bool> TrySetImageAsync(string imageFilePath);
}

/// <summary>Dosya paylaşma (Android paylaşım menüsü, Windows'ta dosyayı Gezgin'de gösterme).</summary>
public interface IShareService
{
    /// <summary>Dosyayı paylaşır. Başarılı olursa true döner.</summary>
    Task<bool> ShareFileAsync(string filePath, string title);
}

/// <summary>Termal fiş yazıcısı (sadece Windows'ta desteklenir).</summary>
public interface IReceiptPrinter
{
    bool IsSupported { get; }
    void SetPrinterName(string printerName);
    void PrintCustomerReceipt(Kuyumcu.Models.Customer customer, List<Kuyumcu.Models.TransactionGroup> transactions);
    List<string> GetAvailablePrinters();
}

/// <summary>
/// Ürünün eski sürümünden (MAUI tabanlı "Kuyumcu" uygulaması) kalan veritabanını bulur.
/// </summary>
public interface ILegacyDataLocator
{
    /// <summary>
    /// Eski sürümün veritabanı dosyası bulunursa ve daha önce aktarılmadıysa yolunu döndürür, yoksa null.
    /// </summary>
    string? FindLegacyDatabase();

    /// <summary>Eski veriler aktarıldı olarak işaretlenir; teklif bir daha gösterilmez.</summary>
    void MarkImported();
}
