using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System.Text;
using Kuyumcu.Platform;

namespace Kuyumcu.Services
{
    public class GoogleDriveService
    {
        private static string[] Scopes = { DriveService.Scope.DriveFile };
        private static string ApplicationName = "Kuyumcu";
        private static string CredentialFolderName = "kuyumcu-drive-credentials";
        private DriveService? _driveService;

        private readonly DatabaseService _databaseService;
        private readonly IAppEnvironment _environment;

        public GoogleDriveService(DatabaseService databaseService, IAppEnvironment environment)
        {
            _databaseService = databaseService;
            _environment = environment;
        }

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

        public async Task<string> BackupDatabaseAsync()
        {
            try
            {
                if (!await IsConfiguredAsync())
                {
                    return "Google Drive bağlantısı ayarlanmamış. Lütfen Ayarlar sayfasından Google Drive'ı bağlayın.";
                }

                if (!File.Exists(_databaseService.DatabasePath))
                {
                    return "Veritabanı dosyası bulunamadı.";
                }

                // Authenticate the user if not already authenticated
                var driveService = await GetDriveServiceAsync();
                if (driveService == null)
                {
                    return "Google Drive bağlantısı kurulamadı. Lütfen tarayıcıdaki Google girişini tamamlayın.";
                }

                // Create backup file name with timestamp
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupFileName = $"{timestamp}{ProductInfo.DriveBackupSuffix}";
                
                // Create a temporary copy of the database to avoid file access issues
                string tempBackupPath = Path.Combine(
                    Path.GetTempPath(), 
                    backupFileName);
                
                try
                {
                    // Create a temporary copy using SQLite's backup API via the database service
                    await _databaseService.CreateBackupCopyAsync(tempBackupPath);
                    
                    // If backup copy fails, inform the user
                    if (!File.Exists(tempBackupPath))
                    {
                        return "Veritabanı yedek kopyası oluşturulamadı.";
                    }
                    
                    // Create file metadata
                    var fileMetadata = new Google.Apis.Drive.v3.Data.File()
                    {
                        Name = backupFileName,
                        MimeType = "application/octet-stream",
                    };

                    // Upload the temporary copy to Google Drive
                    using (var stream = new FileStream(tempBackupPath, FileMode.Open, FileAccess.Read))
                    {
                        // Create the upload request
                        var request = driveService.Files.Create(fileMetadata, stream, "application/octet-stream");
                        request.Fields = "id, name, webViewLink";
                        
                        // Upload the file
                        var uploadProgress = await request.UploadAsync();
                        
                        if (uploadProgress.Status == Google.Apis.Upload.UploadStatus.Completed)
                        {
                            var file = request.ResponseBody;
                            return $"Yedekleme başarılı. Dosya adı: {file.Name}";
                        }
                        else
                        {
                            return $"Yükleme başarısız oldu: {uploadProgress.Exception?.Message ?? "Bilinmeyen hata"}";
                        }
                    }
                }
                finally
                {
                    // Clean up the temporary file
                    if (File.Exists(tempBackupPath))
                    {
                        try
                        {
                            File.Delete(tempBackupPath);
                        }
                        catch
                        {
                            // Ignore cleanup errors
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return $"Hata oluştu: {ex.Message}";
            }
        }

        private async Task<DriveService?> GetDriveServiceAsync()
        {
            if (_driveService != null)
            {
                return _driveService;
            }

            try
            {
                var clientSecrets = await GetClientSecretsAsync();
                if (clientSecrets == null)
                {
                    Console.WriteLine("Google Drive credentials not configured.");
                    return null;
                }

                // Google oturum bilgileri uygulama klasöründe saklanır
                var credentialFolderPath = Path.Combine(_environment.AppDataDirectory, CredentialFolderName);
                Directory.CreateDirectory(credentialFolderPath);

                // Create the credential
                UserCredential credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    clientSecrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credentialFolderPath, true));

                // Create Drive API service
                _driveService = new DriveService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = ApplicationName,
                });

                return _driveService;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Authentication error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Resets the cached drive service so new credentials take effect
        /// </summary>
        public void ResetConnection()
        {
            _driveService = null;
        }
    }
}
