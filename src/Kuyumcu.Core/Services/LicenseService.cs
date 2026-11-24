using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Kuyumcu.Licensing;
using Kuyumcu.Models;
using Kuyumcu.Platform;

namespace Kuyumcu.Services
{
    /// <summary>
    /// Deneme süresi ve lisans.
    ///
    /// Lisans anahtarları geliştiricinin özel anahtarıyla imzalanır (bkz. Licensing/LicenseCodec); uygulamada sadece
    /// açık anahtar vardır. Lisans durumu her sorguda kayıtlı anahtarın imzası, cihaz kimliği ve süresi doğrulanarak
    /// hesaplanır — veritabanındaki IsLicensed alanına güvenilmez.
    /// </summary>
    public partial class LicenseService
    {
        private readonly DatabaseService _databaseService;
        private readonly ISecureStore _secureStore;
        private readonly IAppEnvironment _environment;

        // Deneme süresi (gün)
        private const int TRIAL_DAYS = 30;

        // Güvenli depo anahtarları (Windows: DPAPI dosyası + kayıt defteri yansısı)
        private const string SECURE_TRIAL_START = "kuyumcu_trial_start";
        private const string SECURE_LAST_CHECK = "kuyumcu_last_check";
        private const string SECURE_DEVICE_ID = "kuyumcu_device_id";

        public LicenseService(DatabaseService databaseService, ISecureStore secureStore, IAppEnvironment environment)
        {
            _databaseService = databaseService;
            _secureStore = secureStore;
            _environment = environment;
        }

        /// <summary>
        /// Uygulama başlangıcında deneme süresini başlatır veya mevcut durumu kontrol eder
        /// </summary>
        public async Task InitializeTrialAsync()
        {
            try
            {
                var licenseInfo = await _databaseService.GetLicenseInfoAsync();

                if (licenseInfo == null)
                {
                    var now = DateTime.UtcNow;
                    var deviceId = await GetDeviceIdAsync();

                    // Mevcut kullanıcı mı yoksa yeni kurulum mu kontrol et
                    var isExistingUser = await _databaseService.IsSetupCompletedAsync();

                    if (isExistingUser)
                    {
                        // Kasıtlı kural: kurulumu tamamlanmış ama lisans kaydı olmayan veritabanı, güncelleme yapan
                        // eski kullanıcıdır → deneme süresi bitmiş başlar, hemen lisans anahtarı gerekir.
                        licenseInfo = new LicenseInfo
                        {
                            TrialStartDate = now.AddDays(-TRIAL_DAYS - 1),
                            TrialEndDate = now.AddDays(-1),
                            DeviceId = deviceId,
                            LastCheckDate = now,
                            IsLicensed = false
                        };
                    }
                    else
                    {
                        // Yeni kurulum. Bu Windows kullanıcısında daha önce deneme başlamışsa (veri klasörü silinip
                        // uygulama yeniden kurulmuş olabilir) o tarih kullanılır: deneme sıfırlanamasın.
                        var start = now;
                        if (TryParseUtc(await _secureStore.GetAsync(SECURE_TRIAL_START), out var previousStart) && previousStart <= now)
                            start = previousStart;

                        licenseInfo = new LicenseInfo
                        {
                            TrialStartDate = start,
                            TrialEndDate = start.AddDays(TRIAL_DAYS),
                            DeviceId = deviceId,
                            LastCheckDate = now,
                            IsLicensed = false
                        };
                    }

                    await _databaseService.SaveLicenseInfoAsync(licenseInfo);

                    // Güvenli depoya da kaydet (anti-tampering)
                    await _secureStore.SetAsync(SECURE_TRIAL_START, licenseInfo.TrialStartDate.ToString("O"));
                    await _secureStore.SetAsync(SECURE_LAST_CHECK, now.ToString("O"));
                }
                else
                {
                    // Mevcut kurulum — anti-tampering kontrolü yap
                    await PerformAntiTamperingCheckAsync(licenseInfo);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LicenseService.InitializeTrialAsync hata: {ex.Message}");
            }
        }

        /// <summary>
        /// Lisans/deneme durumunun tamamı (ekranlar için).
        /// </summary>
        public async Task<LicenseState> GetLicenseStateAsync()
        {
            var deviceId = await GetDeviceIdAsync();
            var licenseInfo = await _databaseService.GetLicenseInfoAsync();

            if (licenseInfo == null)
                return new LicenseState { Status = LicenseStatus.Trial, DeviceId = deviceId, RemainingTrialDays = TRIAL_DAYS };

            var nowUtc = DateTime.UtcNow;
            var keyCheck = CheckStoredKey(licenseInfo, deviceId, out var today);

            if (keyCheck is { IsValid: true })
            {
                var expires = keyCheck.Payload!.ExpiresOn;
                return new LicenseState
                {
                    Status = LicenseStatus.Licensed,
                    DeviceId = deviceId,
                    LicenseExpiresOn = expires,
                    DaysUntilLicenseExpiry = expires is { } e ? e.DayNumber - today.DayNumber : null,
                    ActivatedAtUtc = licenseInfo.LicenseActivationDate,
                };
            }

            var keyProblem = keyCheck?.Message;

            if (nowUtc <= licenseInfo.TrialEndDate)
            {
                return new LicenseState
                {
                    Status = LicenseStatus.Trial,
                    DeviceId = deviceId,
                    RemainingTrialDays = Math.Max(0, (licenseInfo.TrialEndDate - nowUtc).Days),
                    KeyProblem = keyProblem,
                };
            }

            return new LicenseState
            {
                Status = LicenseStatus.Expired,
                DeviceId = deviceId,
                LockReason = keyCheck == null ? LicenseLockReason.TrialEnded
                    : keyCheck.Error == LicenseKeyError.Expired ? LicenseLockReason.LicenseEnded
                    : LicenseLockReason.LicenseInvalid,
                LicenseExpiresOn = keyCheck?.Payload?.ExpiresOn,
                KeyProblem = keyProblem,
            };
        }

        /// <summary>
        /// Mevcut lisans durumunu döndürür
        /// </summary>
        public async Task<LicenseStatus> GetLicenseStatusAsync()
        {
            try
            {
                return (await GetLicenseStateAsync()).Status;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetLicenseStatusAsync hata: {ex.Message}");
                return LicenseStatus.Expired; // Hata durumunda güvenli tarafta kal
            }
        }

        /// <summary>
        /// Kalan deneme gün sayısını döndürür (lisanslıysa -1)
        /// </summary>
        public async Task<int> GetRemainingTrialDaysAsync()
        {
            try
            {
                var state = await GetLicenseStateAsync();
                return state.Status == LicenseStatus.Licensed ? -1 : state.RemainingTrialDays;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Lisans anahtarını doğrular ve aktif eder
        /// </summary>
        public async Task<(bool Success, string Message)> ActivateLicenseAsync(string key)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(key))
                    return (false, "Lisans anahtarı boş olamaz.");

                var licenseInfo = await _databaseService.GetLicenseInfoAsync();
                if (licenseInfo == null)
                    return (false, "Lisans bilgileri bulunamadı. Lütfen uygulamayı yeniden başlatın.");

                // Anahtar, veritabanındaki (aktarılmış olabilecek) kimliğe değil bu bilgisayarın kimliğine göre doğrulanır
                var deviceId = await GetDeviceIdAsync();
                using var publicKey = LicensePublicKey.Create();
                var result = LicenseCodec.Verify(key, deviceId, EffectiveToday(licenseInfo), publicKey);
                if (!result.IsValid)
                    return (false, result.Message);

                var now = DateTime.UtcNow;
                licenseInfo.LicenseKey = key.Trim();
                licenseInfo.IsLicensed = true;
                licenseInfo.DeviceId = deviceId;
                licenseInfo.LicenseActivationDate = now;
                if (now > licenseInfo.LastCheckDate)
                    licenseInfo.LastCheckDate = now;

                await _databaseService.SaveLicenseInfoAsync(licenseInfo);

                return (true, result.Payload!.ExpiresOn is { } expires
                    ? $"Lisans etkinleştirildi. {expires:dd.MM.yyyy} tarihine kadar (dahil) tam sürümü kullanabilirsiniz."
                    : "Lisans etkinleştirildi. Uygulamanın tam sürümüne süresiz erişiminiz var.");
            }
            catch (Exception ex)
            {
                return (false, $"Lisans aktivasyonu sırasında hata oluştu: {ex.Message}");
            }
        }

        /// <summary>
        /// Lisans bilgilerini getirir
        /// </summary>
        public async Task<LicenseInfo> GetLicenseInfoAsync()
        {
            return await _databaseService.GetLicenseInfoAsync();
        }

        /// <summary>
        /// Bu bilgisayarın cihaz kimliği (16 onaltılık karakter). Lisans anahtarı buna bağlanır.
        /// Her seferinde donanım/kurulum parmak izinden yeniden hesaplanır; güvenli depodaki değer sadece
        /// parmak izi okunamazsa kullanılır (depodaki değeri değiştirip başka bilgisayarın lisansını kullanmak olmasın).
        /// </summary>
        public async Task<string> GetDeviceIdAsync()
        {
            string? derived = null;
            try
            {
                var fingerprint = _environment.GetDeviceFingerprint();
                if (!string.IsNullOrWhiteSpace(fingerprint))
                {
                    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint));
                    derived = Convert.ToHexString(hash)[..16];
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cihaz parmak izi okunamadı: {ex.Message}");
            }

            try
            {
                var storedId = await _secureStore.GetAsync(SECURE_DEVICE_ID);
                if (derived != null)
                {
                    if (storedId != derived)
                        await _secureStore.SetAsync(SECURE_DEVICE_ID, derived);
                    return derived;
                }

                if (!string.IsNullOrEmpty(storedId))
                    return storedId;

                var fallbackId = Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();
                await _secureStore.SetAsync(SECURE_DEVICE_ID, fallbackId);
                return fallbackId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetDeviceIdAsync hata: {ex.Message}");
                return derived ?? Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();
            }
        }

        #region Private Methods

        /// <summary>Kayıtlı anahtarı doğrular; anahtar yoksa null.</summary>
        private static LicenseCheckResult? CheckStoredKey(LicenseInfo licenseInfo, string deviceId, out DateOnly today)
        {
            today = EffectiveToday(licenseInfo);
            if (string.IsNullOrWhiteSpace(licenseInfo.LicenseKey))
                return null;

            using var publicKey = LicensePublicKey.Create();
            return LicenseCodec.Verify(licenseInfo.LicenseKey, deviceId, today, publicKey);
        }

        /// <summary>
        /// Süreli lisans kontrolünde kullanılan "bugün": bilgisayar saati geri alınarak lisans uzatılamasın diye
        /// şimdiki zaman ile son kontrol zamanının büyüğü alınır.
        /// </summary>
        private static DateOnly EffectiveToday(LicenseInfo licenseInfo)
        {
            var now = DateTime.UtcNow;
            var lastCheck = DateTime.SpecifyKind(licenseInfo.LastCheckDate, DateTimeKind.Utc);
            var effective = lastCheck > now ? lastCheck : now;
            return DateOnly.FromDateTime(effective.ToLocalTime());
        }

        /// <summary>
        /// Anti-tampering kontrolü yapar
        /// </summary>
        private async Task PerformAntiTamperingCheckAsync(LicenseInfo licenseInfo)
        {
            try
            {
                var now = DateTime.UtcNow;

                if (CheckStoredKey(licenseInfo, await GetDeviceIdAsync(), out _) is { IsValid: true })
                {
                    // Geçerli lisans — sadece son kontrol zamanını ileri taşı (asla geri almaz)
                    if (now > licenseInfo.LastCheckDate)
                    {
                        licenseInfo.LastCheckDate = now;
                        await _databaseService.SaveLicenseInfoAsync(licenseInfo);
                    }
                    await _secureStore.SetAsync(SECURE_LAST_CHECK, now.ToString("O"));
                    return;
                }

                // Güvenli depodan kayıtlı tarihi al
                var secureTrialStartStr = await _secureStore.GetAsync(SECURE_TRIAL_START);
                var secureLastCheckStr = await _secureStore.GetAsync(SECURE_LAST_CHECK);

                // 1. Kontrol: Güvenli depoda kayıt yoksa ama DB'de varsa → toleranslı davran ama yeniden kaydet
                if (string.IsNullOrEmpty(secureTrialStartStr) && licenseInfo.TrialStartDate != default)
                {
                    await _secureStore.SetAsync(SECURE_TRIAL_START, licenseInfo.TrialStartDate.ToString("O"));
                    await _secureStore.SetAsync(SECURE_LAST_CHECK, now.ToString("O"));
                }

                // 2. Kontrol: Sistem saati geri alınmış mı?
                if (TryParseUtc(secureLastCheckStr, out var lastCheck) && now < lastCheck.AddMinutes(-5)) // 5 dakika tolerans
                {
                    // Saat geri alınmış! Deneme süresini bitir.
                    licenseInfo.TrialEndDate = DateTime.UtcNow;
                    await _databaseService.SaveLicenseInfoAsync(licenseInfo);
                    Console.WriteLine("Anti-tampering: Sistem saati geri alınmış tespit edildi.");
                    return;
                }

                // 3. Kontrol: DB ve güvenli depo arasındaki tutarsızlık → DB manipüle edilmiş, depodaki tarih esas
                if (TryParseUtc(secureTrialStartStr, out var secureStart)
                    && Math.Abs((licenseInfo.TrialStartDate - secureStart).TotalHours) > 1)
                {
                    licenseInfo.TrialStartDate = secureStart;
                    licenseInfo.TrialEndDate = secureStart.AddDays(TRIAL_DAYS);
                    await _databaseService.SaveLicenseInfoAsync(licenseInfo);
                    Console.WriteLine("Anti-tampering: Veritabanı manipülasyonu tespit edildi.");
                }

                // Son kontrol tarihini güncelle (geri almaz)
                if (now > licenseInfo.LastCheckDate)
                    licenseInfo.LastCheckDate = now;
                await _databaseService.SaveLicenseInfoAsync(licenseInfo);
                await _secureStore.SetAsync(SECURE_LAST_CHECK, now.ToString("O"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PerformAntiTamperingCheckAsync hata: {ex.Message}");
            }
        }

        /// <summary>
        /// Güvenli depoya "O" biçiminde (UTC, sonu "Z") yazılan tarihi UTC olarak okur.
        /// Düz DateTime.TryParse "Z"li tarihi yerel saate çevirir (Türkiye'de +3 saat); bu durumda
        /// UTC "şimdi" ile karşılaştırma yanlış sonuç verir ve deneme süresi haksız yere biterdi.
        /// </summary>
        private static bool TryParseUtc(string? value, out DateTime result)
        {
            if (!string.IsNullOrEmpty(value)
                && DateTime.TryParse(value, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out result))
            {
                result = DateTime.SpecifyKind(result, DateTimeKind.Utc);
                return true;
            }
            result = default;
            return false;
        }

        #endregion
    }
}
