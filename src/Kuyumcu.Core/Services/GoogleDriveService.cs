using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Kuyumcu.Platform;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace Kuyumcu.Services
{
    public enum DriveConnectionState
    {
        /// <summary>Bu derlemede OAuth istemcisi yok ve kullanıcı da girmemiş.</summary>
        NotConfigured,
        /// <summary>Google hesabı hiç bağlanmamış (veya bağlantı kesilmiş).</summary>
        NotConnected,
        Connected,
        /// <summary>Kayıtlı oturum artık geçmiyor (izin geri alınmış, şifre değişmiş vb.) — yeniden bağlanmalı.</summary>
        NeedsReconnect,
    }

    /// <summary>Drive'a yüklenen bir yedek.</summary>
    public sealed record DriveUploadResult(string FileId, string FileName, long Size);

    /// <summary>
    /// Google Drive erişimi (sadece "drive.file" kapsamı: uygulamanın kendi oluşturduğu dosyalar).
    ///
    /// Giriş SADECE kullanıcı "Bağlan" dediğinde tarayıcıda yapılır (<see cref="ConnectAsync"/>). Otomatik yedek
    /// kayıtlı oturumu sessizce kullanır; oturum yoksa/bozuksa asla tarayıcı açmaz, durumu bildirir.
    ///
    /// Drive'daki düzen: "Kuyumcu Defteri Yedekleri" klasörü
    ///   - KuyumcuDefteri_yyyy-MM-dd_HHmm.db3 : tarihli yedekler (saklama kuralına göre eskileri çöpe atılır)
    ///   - KuyumcuDefteri_son_yedek.db3        : her yedekte güncellenen tek dosya (telefon bunu okuyacak)
    /// </summary>
    public class GoogleDriveService
    {
        private static readonly string[] Scopes = { DriveService.Scope.DriveFile };
        private const string ApplicationName = "Kuyumcu Defteri";
        private const string CredentialFolderName = "kuyumcu-drive-credentials";
        private const string UserKey = "user";
        private const string FolderMimeType = "application/vnd.google-apps.folder";
        private const string BackupMimeType = "application/octet-stream";

        // Drive dosyalarını ayırt etmek için uygulama özelliği (ada bakmadan, güvenle)
        // (Değerler ProductInfo'da: Android uygulaması aynı işaretle arar.)
        private const string KindProperty = ProductInfo.DriveKindProperty;
        private const string KindSnapshot = ProductInfo.DriveKindSnapshot;
        private const string KindLatest = ProductInfo.DriveKindLatest;

        private readonly DatabaseService _databaseService;
        private readonly IAppEnvironment _environment;
        private readonly SemaphoreSlim _serviceLock = new(1, 1);
        private DriveService? _driveService;

        public GoogleDriveService(DatabaseService databaseService, IAppEnvironment environment)
        {
            _databaseService = databaseService;
            _environment = environment;
        }

        private string CredentialFolderPath => Path.Combine(_environment.AppDataDirectory, CredentialFolderName);

        /// <summary>
        /// Drive yedeklemesi kullanılabilir mi? Ürünün kendi OAuth istemcisi varsa her zaman evet;
        /// yoksa kullanıcının Ayarlar > Gelişmiş bölümünde kendi bilgilerini girmiş olması gerekir.
        /// </summary>
        public async Task<bool> IsConfiguredAsync()
        {
            if (ProductSecrets.HasBuiltInGoogleClient)
                return true;
            var settings = await _databaseService.GetSettingsAsync();
            return settings.HasCustomGoogleClient;
        }

        /// <summary>Kayıtlı bir Google oturumu var mı? (Ağa çıkmaz, tarayıcı açmaz.)</summary>
        public async Task<DriveConnectionState> GetConnectionStateAsync()
        {
            if (!await IsConfiguredAsync())
                return DriveConnectionState.NotConfigured;

            var token = await new FileDataStore(CredentialFolderPath, true).GetAsync<TokenResponse>(UserKey);
            return token?.RefreshToken is { Length: > 0 } ? DriveConnectionState.Connected : DriveConnectionState.NotConnected;
        }

        /// <summary>
        /// Google hesabını bağlar: tarayıcıda Google girişi açılır, kullanıcı izin verince oturum kaydedilir.
        /// Bağlanan hesabın e-postasını döndürür.
        /// </summary>
        public async Task<string?> ConnectAsync(CancellationToken cancellationToken)
        {
            var clientSecrets = await GetClientSecretsAsync()
                ?? throw new InvalidOperationException("Google Drive bağlantısı bu derlemede tanımlı değil.");

            Directory.CreateDirectory(CredentialFolderPath);
            var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                clientSecrets, Scopes, UserKey, cancellationToken, new FileDataStore(CredentialFolderPath, true));

            await _serviceLock.WaitAsync(cancellationToken);
            try
            {
                _driveService = CreateService(credential);
            }
            finally
            {
                _serviceLock.Release();
            }

            return await GetAccountEmailAsync(cancellationToken);
        }

        /// <summary>Bağlantıyı keser: kayıtlı oturum silinir (Drive'daki yedekler durur).</summary>
        public async Task DisconnectAsync()
        {
            await _serviceLock.WaitAsync();
            try
            {
                _driveService = null;
                await new FileDataStore(CredentialFolderPath, true).ClearAsync();
            }
            finally
            {
                _serviceLock.Release();
            }
        }

        /// <summary>Bağlı Google hesabının e-postası.</summary>
        public async Task<string?> GetAccountEmailAsync(CancellationToken cancellationToken)
        {
            var service = await GetServiceAsync(cancellationToken);
            var request = service.About.Get();
            request.Fields = "user(emailAddress)";
            var about = await request.ExecuteAsync(cancellationToken);
            return about.User?.EmailAddress;
        }

        /// <summary>Yedek klasörünü bulur, yoksa oluşturur. Klasör kimliğini döndürür.</summary>
        public async Task<string> EnsureBackupFolderAsync(string? knownFolderId, CancellationToken cancellationToken)
        {
            var service = await GetServiceAsync(cancellationToken);

            if (!string.IsNullOrEmpty(knownFolderId))
            {
                try
                {
                    var get = service.Files.Get(knownFolderId);
                    get.Fields = "id, trashed";
                    var existing = await get.ExecuteAsync(cancellationToken);
                    if (existing.Trashed != true)
                        return existing.Id;
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Klasör silinmiş: aşağıda yeniden bul/oluştur
                }
            }

            var list = service.Files.List();
            list.Q = $"mimeType = '{FolderMimeType}' and name = '{Escape(ProductInfo.DriveFolderName)}' and trashed = false";
            list.Fields = "files(id)";
            list.Spaces = "drive";
            var found = await list.ExecuteAsync(cancellationToken);
            if (found.Files is { Count: > 0 })
                return found.Files[0].Id;

            var create = service.Files.Create(new DriveFile { Name = ProductInfo.DriveFolderName, MimeType = FolderMimeType });
            create.Fields = "id";
            return (await create.ExecuteAsync(cancellationToken)).Id;
        }

        /// <summary>
        /// Yedek dosyasını tarihli olarak klasöre yükler ve Drive'ın hesapladığı MD5 ile doğrular.
        /// Doğrulama tutmazsa yüklenen dosya silinir ve hata fırlatılır.
        /// </summary>
        public async Task<DriveUploadResult> UploadSnapshotAsync(string folderId, string localPath, string localMd5,
            DateTime takenAt, CancellationToken cancellationToken)
        {
            var service = await GetServiceAsync(cancellationToken);
            var metadata = new DriveFile
            {
                Name = $"{ProductInfo.DriveSnapshotPrefix}{takenAt:yyyy-MM-dd_HHmm}.db3",
                Parents = new List<string> { folderId },
                MimeType = BackupMimeType,
                AppProperties = new Dictionary<string, string> { [KindProperty] = KindSnapshot },
            };

            await using var stream = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var request = service.Files.Create(metadata, stream, BackupMimeType);
            request.Fields = "id, name, size, md5Checksum";
            var progress = await request.UploadAsync(cancellationToken);
            if (progress.Status != Google.Apis.Upload.UploadStatus.Completed)
                throw progress.Exception ?? new IOException("Yükleme tamamlanamadı.");

            var uploaded = request.ResponseBody;
            if (!string.Equals(uploaded.Md5Checksum, localMd5, StringComparison.OrdinalIgnoreCase))
            {
                try { await service.Files.Delete(uploaded.Id).ExecuteAsync(cancellationToken); } catch { /* en iyi çaba */ }
                throw new IOException("Yüklenen yedek doğrulanamadı (MD5 uyuşmadı); yükleme tekrarlanacak.");
            }

            return new DriveUploadResult(uploaded.Id, uploaded.Name, uploaded.Size ?? 0);
        }

        /// <summary>
        /// Telefonun okuyacağı "son yedek" dosyasını günceller (dosya kimliği sabit kalır). Kimliği döndürür.
        /// </summary>
        public async Task<string> UpdateLatestAsync(string folderId, string? knownFileId, string localPath, CancellationToken cancellationToken)
        {
            var service = await GetServiceAsync(cancellationToken);

            if (string.IsNullOrEmpty(knownFileId))
            {
                // Daha önce oluşturulmuş olabilir (ör. durum dosyası silindi): klasörde ara
                var list = service.Files.List();
                list.Q = $"'{folderId}' in parents and trashed = false and appProperties has {{ key='{KindProperty}' and value='{KindLatest}' }}";
                list.Fields = "files(id)";
                knownFileId = (await list.ExecuteAsync(cancellationToken)).Files?.FirstOrDefault()?.Id;
            }

            await using var stream = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read);

            if (!string.IsNullOrEmpty(knownFileId))
            {
                try
                {
                    var update = service.Files.Update(new DriveFile(), knownFileId, stream, BackupMimeType);
                    update.Fields = "id";
                    var progress = await update.UploadAsync(cancellationToken);
                    if (progress.Status == Google.Apis.Upload.UploadStatus.Completed)
                        return update.ResponseBody.Id;
                    if (progress.Exception is not GoogleApiException { HttpStatusCode: System.Net.HttpStatusCode.NotFound })
                        throw progress.Exception ?? new IOException("Son yedek güncellenemedi.");
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Dosya silinmiş: yeniden oluştur
                }
                stream.Position = 0;
            }

            var metadata = new DriveFile
            {
                Name = ProductInfo.DriveLatestFileName,
                Parents = new List<string> { folderId },
                MimeType = BackupMimeType,
                AppProperties = new Dictionary<string, string> { [KindProperty] = KindLatest },
            };
            var create = service.Files.Create(metadata, stream, BackupMimeType);
            create.Fields = "id";
            var created = await create.UploadAsync(cancellationToken);
            if (created.Status != Google.Apis.Upload.UploadStatus.Completed)
                throw created.Exception ?? new IOException("Son yedek oluşturulamadı.");
            return create.ResponseBody.Id;
        }

        /// <summary>
        /// Saklama kuralı: en yeni <paramref name="keepRecent"/> yedek, son <paramref name="dailyDays"/> günün her gününden
        /// en yenisi ve son <paramref name="monthlyMonths"/> ayın her ayından en yenisi kalır; diğerleri Drive çöp kutusuna
        /// taşınır (Google 30 gün sonra kalıcı siler). Sadece bu uygulamanın tarihli yedeklerine dokunur. Çöpe atılan sayıyı döndürür.
        /// </summary>
        public async Task<int> ApplyRetentionAsync(string folderId, int keepRecent, int dailyDays, int monthlyMonths, CancellationToken cancellationToken)
        {
            var service = await GetServiceAsync(cancellationToken);
            var snapshots = new List<DriveFile>();
            string? pageToken = null;
            do
            {
                var list = service.Files.List();
                list.Q = $"'{folderId}' in parents and trashed = false and appProperties has {{ key='{KindProperty}' and value='{KindSnapshot}' }}";
                list.Fields = "nextPageToken, files(id, createdTime)";
                list.PageSize = 1000;
                list.PageToken = pageToken;
                var page = await list.ExecuteAsync(cancellationToken);
                snapshots.AddRange(page.Files ?? []);
                pageToken = page.NextPageToken;
            } while (pageToken != null);

            var ordered = snapshots
                .Where(f => f.CreatedTimeDateTimeOffset.HasValue)
                .OrderByDescending(f => f.CreatedTimeDateTimeOffset!.Value)
                .ToList();

            var keep = new HashSet<string>(ordered.Take(keepRecent).Select(f => f.Id));
            var now = DateTime.Now;
            foreach (var group in ordered.GroupBy(f => f.CreatedTimeDateTimeOffset!.Value.LocalDateTime.Date))
            {
                if (group.Key >= now.Date.AddDays(-(dailyDays - 1)))
                    keep.Add(group.First().Id); // grup zaten yeniden eskiye sıralı
            }
            var oldestMonth = new DateTime(now.Year, now.Month, 1).AddMonths(-(monthlyMonths - 1));
            foreach (var group in ordered.GroupBy(f => { var d = f.CreatedTimeDateTimeOffset!.Value.LocalDateTime; return new DateTime(d.Year, d.Month, 1); }))
            {
                if (group.Key >= oldestMonth)
                    keep.Add(group.First().Id);
            }

            var trashed = 0;
            foreach (var file in ordered.Where(f => !keep.Contains(f.Id)))
            {
                await service.Files.Update(new DriveFile { Trashed = true }, file.Id).ExecuteAsync(cancellationToken);
                trashed++;
            }
            return trashed;
        }

        /// <summary>
        /// Drive'daki "son yedek" dosyasını indirir (yeniden kurulumda geri yüklemek için).
        /// Yoksa null; varsa yedeğin Drive'daki son değişiklik zamanını döndürür.
        /// </summary>
        public async Task<DateTimeOffset?> DownloadLatestBackupAsync(string destinationPath, CancellationToken cancellationToken)
        {
            var service = await GetServiceAsync(cancellationToken);
            var list = service.Files.List();
            list.Q = $"trashed = false and appProperties has {{ key='{KindProperty}' and value='{KindLatest}' }}";
            list.Fields = "files(id, modifiedTime)";
            list.OrderBy = "modifiedTime desc";
            list.Spaces = "drive";
            var latest = (await list.ExecuteAsync(cancellationToken)).Files?.FirstOrDefault();
            if (latest == null)
                return null;

            await using (var stream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var progress = await service.Files.Get(latest.Id).DownloadAsync(stream, cancellationToken);
                if (progress.Status != Google.Apis.Download.DownloadStatus.Completed)
                    throw progress.Exception ?? new IOException("Drive'daki yedek indirilemedi.");
            }
            return latest.ModifiedTimeDateTimeOffset;
        }

        /// <summary>Drive klasörünün tarayıcıda açılacak adresi.</summary>
        public static string FolderWebLink(string folderId) => $"https://drive.google.com/drive/folders/{folderId}";

        /// <summary>
        /// Kayıtlı oturumu tekrar yükler (ör. Ayarlar'da OAuth bilgileri değişince).
        /// </summary>
        public void ResetConnection()
        {
            _driveService = null;
        }

        /// <summary>Oturum kayıtlı değil ya da geçersiz.</summary>
        public sealed class DriveNotConnectedException(string message, bool needsReconnect, Exception? inner = null)
            : Exception(message, inner)
        {
            public bool NeedsReconnect { get; } = needsReconnect;
        }

        /// <summary>
        /// Kayıtlı oturumla Drive istemcisi (tarayıcı AÇMAZ). Oturum yoksa <see cref="DriveNotConnectedException"/>.
        /// Erişim jetonu süresi dolunca kütüphane yenileme jetonuyla kendisi yeniler.
        /// </summary>
        private async Task<DriveService> GetServiceAsync(CancellationToken cancellationToken)
        {
            await _serviceLock.WaitAsync(cancellationToken);
            try
            {
                if (_driveService != null)
                    return _driveService;

                var clientSecrets = await GetClientSecretsAsync()
                    ?? throw new DriveNotConnectedException("Google Drive bağlantısı bu derlemede tanımlı değil.", needsReconnect: false);

                var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = clientSecrets,
                    Scopes = Scopes,
                    DataStore = new FileDataStore(CredentialFolderPath, true),
                });

                var token = await flow.LoadTokenAsync(UserKey, cancellationToken);
                if (token?.RefreshToken is not { Length: > 0 })
                    throw new DriveNotConnectedException("Google hesabı bağlı değil.", needsReconnect: false);

                _driveService = CreateService(new UserCredential(flow, UserKey, token));
                return _driveService;
            }
            finally
            {
                _serviceLock.Release();
            }
        }

        private static DriveService CreateService(UserCredential credential) => new(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName,
        });

        /// <summary>
        /// Kullanılacak OAuth istemci bilgileri: önce kullanıcının özel bilgileri, yoksa ürünün kendi istemcisi.
        /// </summary>
        private async Task<ClientSecrets?> GetClientSecretsAsync()
        {
            var settings = await _databaseService.GetSettingsAsync();
            if (settings.HasCustomGoogleClient)
                return new ClientSecrets { ClientId = settings.GoogleClientId, ClientSecret = settings.GoogleClientSecret };

            if (ProductSecrets.HasBuiltInGoogleClient)
                return new ClientSecrets { ClientId = ProductSecrets.GoogleDesktopClientId, ClientSecret = ProductSecrets.GoogleDesktopClientSecret };

            return null;
        }

        private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("'", "\\'");
    }
}
