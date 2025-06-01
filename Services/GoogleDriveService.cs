using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System.Text;
using Microsoft.Maui.Storage;

namespace Kuyumcu.Services
{
    public class GoogleDriveService
    {
        private static string[] Scopes = { DriveService.Scope.DriveFile };
        private static string ApplicationName = "Kuyumcu";
        private static string CredentialFolderName = "kuyumcu-drive-credentials";
        private DriveService? _driveService;

        private readonly DatabaseService _databaseService;

        public GoogleDriveService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<string> BackupDatabaseAsync()
        {
            try
            {
                // Get the database path - checking both possible paths
                string databasePath = "";
                string localAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kuyumcu.db3");
                string appDataPath = Path.Combine(FileSystem.AppDataDirectory, "kuyumcu.db");
                
                if (File.Exists(localAppDataPath))
                {
                    databasePath = localAppDataPath;
                }
                else if (File.Exists(appDataPath))
                {
                    databasePath = appDataPath;
                }
                else
                {
                    return "Veritabanı dosyası bulunamadı.";
                }

                // Authenticate the user if not already authenticated
                var driveService = await GetDriveServiceAsync();
                if (driveService == null)
                {
                    return "Google Drive bağlantısı kurulamadı. Lütfen tarayıcıdaki kimlik doğrulama işlemini tamamlayın.";
                }

                // Create backup file name with timestamp
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupFileName = $"{timestamp}_Kuyumcu_Yedek.db3";
                
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
                            return $"Veritabanı yedekleme başarılı. Dosya adı: {file.Name}";
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
                // Get the credential folder path in the app's local storage
                var credentialFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), CredentialFolderName);
                Directory.CreateDirectory(credentialFolderPath);

                // Create ClientSecrets object with Google OAuth credentials
                var clientSecrets = new ClientSecrets
                {
                    // NOTE: These are example values and must be replaced with actual values from the Google Cloud Console
                    ClientId = "345633897924-09a5r28ba6vk4210chg7pdkk85ehikje.apps.googleusercontent.com",
                    ClientSecret = "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"
                };

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
    }
}
