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
        /// Altın fiyat sitesi (fiyat.ekeo.org.tr) üye kodu. Boşsa fiyat çekme özelliği kapalıdır.
        /// </summary>
        public string GoldPriceSiteCode { get; set; } = string.Empty;

        /// <summary>
        /// Altın fiyat sitesi şifresi.
        /// </summary>
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

        /// <summary>
        /// Altın fiyat sitesi bilgileri girilmiş mi?
        /// </summary>
        [SQLite.Ignore]
        public bool HasGoldPriceSiteCredentials =>
            !string.IsNullOrWhiteSpace(GoldPriceSiteCode) && !string.IsNullOrWhiteSpace(GoldPriceSitePassword);
    }
}
