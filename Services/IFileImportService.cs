using System;
using System.Threading.Tasks;

namespace Kuyumcu.Services
{
    public interface IFileImportService
    {
        /// <summary>
        /// Kullanıcıdan bir .db3 dosyası seçmesini ister ve seçilen dosyanın yolunu döndürür.
        /// </summary>
        /// <returns>Seçilen dosyanın tam yolu veya işlem iptal edilirse null</returns>
        Task<string> PickDatabaseFileAsync();
        
        /// <summary>
        /// Seçilen dosyayı uygulama klasörüne kopyalar
        /// </summary>
        /// <param name="sourceFilePath">Kaynak dosya yolu</param>
        /// <param name="destinationFileName">Hedef dosya adı</param>
        /// <returns>Kopyalanan dosyanın tam yolu</returns>
        Task<string> CopyFileToAppFolderAsync(string sourceFilePath, string destinationFileName);
    }
}
