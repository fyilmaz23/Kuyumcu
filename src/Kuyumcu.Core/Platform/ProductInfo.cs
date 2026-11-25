namespace Kuyumcu.Platform;

/// <summary>
/// Ürün kimliği. Ürün adı/klasör adı değişecekse SADECE burası değiştirilir.
/// Not: Paket kimlikleri (Store / Play) proje dosyalarında ayrıca tanımlıdır.
/// </summary>
public static class ProductInfo
{
    /// <summary>Kullanıcıya gösterilen ürün adı.</summary>
    public const string Name = "Kuyumcu Defteri";

    /// <summary>Uygulama verilerinin tutulduğu klasör adı (%LOCALAPPDATA%\&lt;bu&gt;).</summary>
    public const string DataFolderName = "KuyumcuDefteri";

    /// <summary>Ana veritabanı dosya adı. Yedek dosyaları da bu uzantıyı kullanır.</summary>
    public const string DatabaseFileName = "Kuyumcu.db3";

    /// <summary>Google Drive'da yedeklerin tutulduğu klasör.</summary>
    public const string DriveFolderName = "Kuyumcu Defteri Yedekleri";

    /// <summary>Drive'daki tarihli yedeklerin adı: önek + yyyy-MM-dd_HHmm.db3</summary>
    public const string DriveSnapshotPrefix = "KuyumcuDefteri_";

    /// <summary>Her yedekte güncellenen tek dosya (telefon bunu okur).</summary>
    public const string DriveLatestFileName = "KuyumcuDefteri_son_yedek.db3";

    /// <summary>Drive dosyalarındaki uygulama özelliği (appProperties) anahtarı: dosyanın türünü ada bakmadan belirtir.</summary>
    public const string DriveKindProperty = "kuyumcuKind";

    /// <summary><see cref="DriveKindProperty"/> değeri: tarihli yedek.</summary>
    public const string DriveKindSnapshot = "snapshot";

    /// <summary><see cref="DriveKindProperty"/> değeri: her yedekte güncellenen "son yedek" (telefon bunu okur).</summary>
    public const string DriveKindLatest = "latest";
}
