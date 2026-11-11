using SQLite;

namespace Kuyumcu.Models
{
    public class LicenseInfo
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>
        /// Lisans anahtarı (aktivasyon sonrası doldurulur)
        /// </summary>
        public string LicenseKey { get; set; } = string.Empty;

        /// <summary>
        /// Lisans aktif mi?
        /// </summary>
        public bool IsLicensed { get; set; } = false;

        /// <summary>
        /// Deneme süresi başlangıç tarihi (UTC)
        /// </summary>
        public DateTime TrialStartDate { get; set; }

        /// <summary>
        /// Deneme süresi bitiş tarihi (UTC)
        /// </summary>
        public DateTime TrialEndDate { get; set; }

        /// <summary>
        /// Cihaz benzersiz kimliği
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Lisans aktivasyon tarihi (UTC)
        /// </summary>
        public DateTime? LicenseActivationDate { get; set; }

        /// <summary>
        /// Son lisans kontrol tarihi (anti-tampering için)
        /// </summary>
        public DateTime LastCheckDate { get; set; }
    }

    /// <summary>
    /// Lisans durumu
    /// </summary>
    public enum LicenseStatus
    {
        /// <summary>
        /// Deneme sürümünde
        /// </summary>
        Trial,

        /// <summary>
        /// Lisanslı (tam sürüm)
        /// </summary>
        Licensed,

        /// <summary>
        /// Deneme süresi dolmuş, lisans gerekli
        /// </summary>
        Expired
    }
}
