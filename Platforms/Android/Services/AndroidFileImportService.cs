using Android.Content;
using Android.Provider;
using Kuyumcu.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using System;
using System.Threading.Tasks;

namespace Kuyumcu.Platforms.Android.Services
{
    public class AndroidFileImportService : IFileImportService
    {
        public async Task<string> PickDatabaseFileAsync()
        {
            try
            {
                // Ekstra MIME tipleri ile dosya seçimini başlat
                var customFileType = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.Android, new[] { "application/octet-stream", "application/x-sqlite3" } }
                    });

                var options = new PickOptions
                {
                    PickerTitle = "Veritabanı Dosyası Seç (.db3)",
                    FileTypes = customFileType,
                };

                var result = await FilePicker.PickAsync(options);
                if (result == null)
                    return null; // Kullanıcı işlemi iptal etti

                // Sadece .db3 uzantılı dosyaları kabul et
                if (!result.FileName.EndsWith(".db3", StringComparison.OrdinalIgnoreCase))
                {
                    await Application.Current.MainPage.DisplayAlert("Hata", "Lütfen sadece .db3 uzantılı dosya seçin.", "Tamam");
                    return null;
                }

                return result.FullPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dosya seçme hatası: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert("Hata", $"Dosya seçilirken bir hata oluştu: {ex.Message}", "Tamam");
                return null;
            }
        }

        public async Task<string> CopyFileToAppFolderAsync(string sourceFilePath, string destinationFileName)
        {
            try
            {
                // Hedef dosya yolunu oluştur
                string destFileName = Path.GetFileName(destinationFileName);
                string destFilePath = Path.Combine(FileSystem.CacheDirectory, destFileName);

                // Dosyayı kopyala
                byte[] fileData = await File.ReadAllBytesAsync(sourceFilePath);
                await File.WriteAllBytesAsync(destFilePath, fileData);

                return destFilePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dosya kopyalama hatası: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert("Hata", $"Dosya kopyalanırken bir hata oluştu: {ex.Message}", "Tamam");
                return null;
            }
        }
    }
}
