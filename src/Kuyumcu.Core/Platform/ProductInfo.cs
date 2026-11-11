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

    /// <summary>Google Drive yedek dosyalarının adında geçen sabit kısım.</summary>
    public const string DriveBackupSuffix = "_Kuyumcu_Yedek.db3";
}
