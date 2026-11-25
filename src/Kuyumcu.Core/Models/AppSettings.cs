using SQLite;

namespace Kuyumcu.Models
{
    public class AppSettings
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>
        /// İş yeri adı (örn: "Kuyumcu Mehmet")
        /// </summary>
        public string BusinessName { get; set; } = string.Empty;

        /// <summary>
        /// Müşteri fişinin başlığı. Boşsa <see cref="BusinessName"/> basılır (bkz. <see cref="ReceiptTexts"/>).
        /// </summary>
        public string? ReceiptTitle { get; set; }

        /// <summary>
        /// Müşteri fişinin alt yazısı (ör. Instagram adı, telefon, adres). Boşsa basılmaz.
        /// </summary>
        public string? ReceiptFooter { get; set; }

        /// <summary>
        /// Google API OAuth Client ID (Google Cloud Console'dan alınır)
        /// </summary>
        public string GoogleClientId { get; set; } = string.Empty;

        /// <summary>
        /// Google API OAuth Client Secret (Google Cloud Console'dan alınır)
        /// </summary>
        public string GoogleClientSecret { get; set; } = string.Empty;

        /// <summary>
        /// İlk kurulum tamamlandı mı?
        /// </summary>
        public bool IsSetupCompleted { get; set; } = false;

        /// <summary>
        /// Eski altın fiyat sitesi (fiyat.ekeo.org.tr) üye kodu. Artık kullanılmıyor: fiyatlar herkese açık
        /// kaynaktan alınıyor. Kolon eski veritabanlarıyla uyum için duruyor.
        /// </summary>
        public string GoldPriceSiteCode { get; set; } = string.Empty;

        /// <summary>Eski altın fiyat sitesi şifresi. Artık kullanılmıyor (bkz. <see cref="GoldPriceSiteCode"/>).</summary>
        public string GoldPriceSitePassword { get; set; } = string.Empty;

        /// <summary>
        /// Altın fiyat görseli için işletmeye özel arka plan şablonu (JPG/PNG).
        /// Boşsa sade bir varsayılan tasarım üretilir.
        /// </summary>
        public byte[]? GoldPriceTemplateImage { get; set; }

        /// <summary>
        /// Google Drive bağlantısı yapılabilir mi? (Ürünün kendi OAuth istemcisi veya kullanıcının girdiği bilgiler)
        /// </summary>
        [SQLite.Ignore]
        public bool HasCustomGoogleClient =>
            !string.IsNullOrWhiteSpace(GoogleClientId) && !string.IsNullOrWhiteSpace(GoogleClientSecret);
    }
}
