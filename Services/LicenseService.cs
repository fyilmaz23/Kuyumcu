using System.Security.Cryptography;
using System.Text;
using Kuyumcu.Models;

namespace Kuyumcu.Services
{
    public class LicenseService
    {
        private readonly DatabaseService _databaseService;

        // ⚠️ Bu gizli anahtarı değiştirin! Üretim ortamında güçlü ve benzersiz bir anahtar kullanın.
        private const string SECRET_KEY = "KuyumcuApp2026!SecretHMAC#Key@Production";

        // Deneme süresi (gün)
        private const int TRIAL_DAYS = 30;

        // Lisans anahtarı prefix'i
        private const string LICENSE_PREFIX = "KUYM";

        // SecureStorage anahtarları
        private const string SECURE_TRIAL_START = "kuyumcu_trial_start";
        private const string SECURE_LAST_CHECK = "kuyumcu_last_check";
        private const string SECURE_DEVICE_ID = "kuyumcu_device_id";

        public LicenseService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
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
                    // İlk kurulum — deneme süresini başlat
                    var now = DateTime.UtcNow;
                    var deviceId = await GetDeviceIdAsync();

                    licenseInfo = new LicenseInfo
                    {
                        TrialStartDate = now,
                        TrialEndDate = now.AddDays(TRIAL_DAYS),
                        DeviceId = deviceId,
                        LastCheckDate = now,
                        IsLicensed = false
                    };

                    await _databaseService.SaveLicenseInfoAsync(licenseInfo);

                    // SecureStorage'a da kaydet (anti-tampering)
                    await SecureStorage.Default.SetAsync(SECURE_TRIAL_START, now.ToString("O"));
                    await SecureStorage.Default.SetAsync(SECURE_LAST_CHECK, now.ToString("O"));
                    await SecureStorage.Default.SetAsync(SECURE_DEVICE_ID, deviceId);
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
        /// Mevcut lisans durumunu döndürür
        /// </summary>
        public async Task<LicenseStatus> GetLicenseStatusAsync()
        {
            try
            {
                var licenseInfo = await _databaseService.GetLicenseInfoAsync();

                if (licenseInfo == null)
                    return LicenseStatus.Trial; // Henüz initialize edilmemiş, trial varsayalım

                if (licenseInfo.IsLicensed)
                    return LicenseStatus.Licensed;

                if (DateTime.UtcNow <= licenseInfo.TrialEndDate)
                    return LicenseStatus.Trial;

                return LicenseStatus.Expired;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetLicenseStatusAsync hata: {ex.Message}");
                return LicenseStatus.Expired; // Hata durumunda güvenli tarafta kal
            }
        }

        /// <summary>
        /// Kalan deneme gün sayısını döndürür
        /// </summary>
        public async Task<int> GetRemainingTrialDaysAsync()
        {
            try
            {
                var licenseInfo = await _databaseService.GetLicenseInfoAsync();

                if (licenseInfo == null)
                    return TRIAL_DAYS;

                if (licenseInfo.IsLicensed)
                    return -1; // Lisanslı

                var remaining = (licenseInfo.TrialEndDate - DateTime.UtcNow).Days;
                return Math.Max(0, remaining);
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

                // Anahtarı temizle
                key = key.Trim().ToUpperInvariant();

                var licenseInfo = await _databaseService.GetLicenseInfoAsync();
                if (licenseInfo == null)
                    return (false, "Lisans bilgileri bulunamadı. Lütfen uygulamayı yeniden başlatın.");

                // Doğrulama yap
                var validationResult = ValidateLicenseKey(key, licenseInfo.DeviceId);

                if (!validationResult.IsValid)
                    return (false, validationResult.Message);

                // Lisansı aktif et
                licenseInfo.IsLicensed = true;
                licenseInfo.LicenseKey = key;
                licenseInfo.LicenseActivationDate = DateTime.UtcNow;
                licenseInfo.LastCheckDate = DateTime.UtcNow;

                await _databaseService.SaveLicenseInfoAsync(licenseInfo);

                return (true, "Lisans başarıyla etkinleştirildi! Uygulamanın tam sürümüne erişiminiz sağlandı.");
            }
            catch (Exception ex)
            {
                return (false, $"Lisans aktivasyonu sırasında hata oluştu: {ex.Message}");
            }
        }

        /// <summary>
        /// Lisans anahtarını doğrular
        /// </summary>
        public (bool IsValid, string Message) ValidateLicenseKey(string key, string deviceId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(key))
                    return (false, "Lisans anahtarı boş olamaz.");

                key = key.Trim().ToUpperInvariant();

                // Format kontrolü: KUYM-XXXXX-XXXXX-XXXXX-CHECKSUM
                var parts = key.Split('-');
                if (parts.Length != 5)
                    return (false, "Geçersiz lisans anahtarı formatı.");

                if (parts[0] != LICENSE_PREFIX)
                    return (false, "Geçersiz lisans anahtarı.");

                // Payload (ilk 4 bölüm)
                var payload = $"{parts[0]}-{parts[1]}-{parts[2]}-{parts[3]}";

                // Checksum doğrulaması
                var expectedChecksum = ComputeChecksum(payload);

                if (parts[4] != expectedChecksum)
                    return (false, "Lisans anahtarı geçersiz veya bu cihaz için üretilmemiş.");

                // Cihaz ID kontrolü — payload içinde device ID hash'i bulunmalı
                var deviceHash = ComputeDeviceHash(deviceId);
                if (!parts[1].Contains(deviceHash.Substring(0, 5)))
                    return (false, "Bu lisans anahtarı bu cihaz için geçerli değil.");

                return (true, "Geçerli lisans anahtarı.");
            }
            catch (Exception ex)
            {
                return (false, $"Doğrulama hatası: {ex.Message}");
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
        /// Cihaza özgü benzersiz kimlik oluşturur
        /// </summary>
        public async Task<string> GetDeviceIdAsync()
        {
            try
            {
                // Önce SecureStorage'dan kontrol et
                var storedId = await SecureStorage.Default.GetAsync(SECURE_DEVICE_ID);
                if (!string.IsNullOrEmpty(storedId))
                    return storedId;

                // Cihaz bilgilerinden benzersiz bir kimlik oluştur
                var deviceInfo = $"{DeviceInfo.Name}-{DeviceInfo.Model}-{DeviceInfo.Manufacturer}-{DeviceInfo.Platform}";
                
                using var sha256 = SHA256.Create();
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(deviceInfo));
                var deviceId = Convert.ToHexString(hashBytes).Substring(0, 16).ToUpperInvariant();

                // SecureStorage'a kaydet
                await SecureStorage.Default.SetAsync(SECURE_DEVICE_ID, deviceId);

                return deviceId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetDeviceIdAsync hata: {ex.Message}");
                // Fallback: rastgele ID oluştur
                var fallbackId = Guid.NewGuid().ToString("N").Substring(0, 16).ToUpperInvariant();
                try
                {
                    await SecureStorage.Default.SetAsync(SECURE_DEVICE_ID, fallbackId);
                }
                catch { }
                return fallbackId;
            }
        }

        /// <summary>
        /// Lisans anahtarı oluşturur (Geliştirici aracı)
        /// </summary>
        public string GenerateLicenseKey(string deviceId)
        {
            try
            {
                // Cihaz hash'i
                var deviceHash = ComputeDeviceHash(deviceId);

                // Payload parçalarını oluştur
                var part1 = deviceHash.Substring(0, 5).ToUpperInvariant();
                var part2 = GenerateRandomAlphanumeric(5);
                var part3 = GenerateRandomAlphanumeric(5);

                var payload = $"{LICENSE_PREFIX}-{part1}-{part2}-{part3}";

                // Checksum hesapla
                var checksum = ComputeChecksum(payload);

                return $"{payload}-{checksum}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GenerateLicenseKey hata: {ex.Message}");
                return string.Empty;
            }
        }

        #region Private Methods

        /// <summary>
        /// Anti-tampering kontrolü yapar
        /// </summary>
        private async Task PerformAntiTamperingCheckAsync(LicenseInfo licenseInfo)
        {
            try
            {
                if (licenseInfo.IsLicensed)
                {
                    // Lisanslı kullanıcı — sadece last check güncelle
                    licenseInfo.LastCheckDate = DateTime.UtcNow;
                    await _databaseService.SaveLicenseInfoAsync(licenseInfo);
                    return;
                }

                var now = DateTime.UtcNow;

                // SecureStorage'dan kayıtlı tarihi al
                var secureTrialStartStr = await SecureStorage.Default.GetAsync(SECURE_TRIAL_START);
                var secureLastCheckStr = await SecureStorage.Default.GetAsync(SECURE_LAST_CHECK);

                // 1. Kontrol: SecureStorage'da kayıt yoksa ama DB'de varsa → tampering
                if (string.IsNullOrEmpty(secureTrialStartStr) && licenseInfo.TrialStartDate != default)
                {
                    // SecureStorage temizlenmiş olabilir — toleranslı davran ama yeniden kaydet
                    await SecureStorage.Default.SetAsync(SECURE_TRIAL_START, licenseInfo.TrialStartDate.ToString("O"));
                    await SecureStorage.Default.SetAsync(SECURE_LAST_CHECK, now.ToString("O"));
                }

                // 2. Kontrol: Sistem saati geri alınmış mı?
                if (!string.IsNullOrEmpty(secureLastCheckStr))
                {
                    if (DateTime.TryParse(secureLastCheckStr, out var lastCheck))
                    {
                        if (now < lastCheck.AddMinutes(-5)) // 5 dakika tolerans
                        {
                            // Saat geri alınmış! Deneme süresini bitir.
                            licenseInfo.TrialEndDate = DateTime.UtcNow;
                            await _databaseService.SaveLicenseInfoAsync(licenseInfo);
                            Console.WriteLine("Anti-tampering: Sistem saati geri alınmış tespit edildi.");
                            return;
                        }
                    }
                }

                // 3. Kontrol: DB ve SecureStorage arasındaki tutarsızlık
                if (!string.IsNullOrEmpty(secureTrialStartStr))
                {
                    if (DateTime.TryParse(secureTrialStartStr, out var secureStart))
                    {
                        // DB'deki tarih SecureStorage'dan farklıysa → DB manipüle edilmiş
                        if (Math.Abs((licenseInfo.TrialStartDate - secureStart).TotalHours) > 1)
                        {
                            // DB manipüle edilmiş! SecureStorage'daki tarihi kullan ve süreyi bitir
                            licenseInfo.TrialStartDate = secureStart;
                            licenseInfo.TrialEndDate = secureStart.AddDays(TRIAL_DAYS);
                            await _databaseService.SaveLicenseInfoAsync(licenseInfo);
                            Console.WriteLine("Anti-tampering: Veritabanı manipülasyonu tespit edildi.");
                        }
                    }
                }

                // Last check tarihini güncelle
                licenseInfo.LastCheckDate = now;
                await _databaseService.SaveLicenseInfoAsync(licenseInfo);
                await SecureStorage.Default.SetAsync(SECURE_LAST_CHECK, now.ToString("O"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PerformAntiTamperingCheckAsync hata: {ex.Message}");
            }
        }

        /// <summary>
        /// HMAC-SHA256 ile checksum hesaplar
        /// </summary>
        private string ComputeChecksum(string payload)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SECRET_KEY));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(hash).Substring(0, 5).ToUpperInvariant();
        }

        /// <summary>
        /// Cihaz ID'sinden kısa hash oluşturur
        /// </summary>
        private string ComputeDeviceHash(string deviceId)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(deviceId + SECRET_KEY));
            return Convert.ToHexString(hashBytes).Substring(0, 10).ToUpperInvariant();
        }

        /// <summary>
        /// Rastgele alfanumerik karakter dizisi oluşturur
        /// </summary>
        private string GenerateRandomAlphanumeric(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Range(0, length).Select(_ => chars[random.Next(chars.Length)]).ToArray());
        }

        #endregion
    }
}
