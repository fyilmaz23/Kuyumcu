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
        /// Son etkinleştirmede anahtar geçerli miydi? SADECE bilgi amaçlı: lisans durumu her açılışta
        /// LicenseKey'in imzası doğrulanarak hesaplanır, bu alana güvenilmez (veritabanından elle değiştirilebilir).
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

    /// <summary>Ekranlarda gösterilecek lisans durumu (LicenseService.GetLicenseStateAsync).</summary>
    public sealed class LicenseState
    {
        public LicenseStatus Status { get; init; }

        /// <summary>Status = Expired ise nedeni.</summary>
        public LicenseLockReason LockReason { get; init; }

        /// <summary>Bu bilgisayarın cihaz kimliği (lisans istemek için müşteri bunu iletir).</summary>
        public string DeviceId { get; init; } = string.Empty;

        /// <summary>Deneme sürümünde kalan gün.</summary>
        public int RemainingTrialDays { get; init; }

        /// <summary>Lisanslıysa son geçerlilik günü; null ise süresiz.</summary>
        public DateOnly? LicenseExpiresOn { get; init; }

        /// <summary>Lisansın bitmesine kalan gün (süreli lisanslarda).</summary>
        public int? DaysUntilLicenseExpiry { get; init; }

        public DateTime? ActivatedAtUtc { get; init; }

        /// <summary>Kayıtlı anahtar varsa ama geçmiyorsa nedeni (ör. eski biçim, başka cihaz).</summary>
        public string? KeyProblem { get; init; }

        /// <summary>Süreli lisans bitmek üzere mi (uyarı gösterilir)?</summary>
        public bool IsLicenseExpiringSoon => DaysUntilLicenseExpiry is >= 0 and <= 15;

        /// <summary>Lisansın açtığı ek ücretli modüller. Deneme sürümünde ve lisans yokken hiçbiri açık değildir.</summary>
        public LicenseFeatures Features { get; init; }

        /// <summary>Barkodlu vitrin stoğu (etiket basma, okutarak satış, sayım, kasa) kullanılabilir mi?</summary>
        public bool HasBarcodeModule => Status == LicenseStatus.Licensed && Features.HasFlag(LicenseFeatures.Barcode);
    }

    /// <summary>
    /// Lisans anahtarına imzayla gömülen ek ücretli modüller (anahtarda 1 bayt: en fazla 8 modül).
    /// Bit değerleri ASLA değiştirilmez; değişirse daha önce verilmiş anahtarlar başka modül açar.
    /// </summary>
    [Flags]
    public enum LicenseFeatures : byte
    {
        None = 0,

        /// <summary>Barkodlu vitrin stoğu: etiket yazıcısı, okutarak satış, sayım, kasa ve stok raporları.</summary>
        Barcode = 1 << 0,
    }

    public enum LicenseLockReason
    {
        None,
        /// <summary>Deneme süresi bitti, lisans girilmedi.</summary>
        TrialEnded,
        /// <summary>Süreli lisansın süresi doldu.</summary>
        LicenseEnded,
        /// <summary>Kayıtlı anahtar geçersiz (eski biçim, başka cihaz, bozuk).</summary>
        LicenseInvalid,
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
