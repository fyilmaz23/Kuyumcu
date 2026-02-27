using SQLite;

namespace Kuyumcu.Models
{
    public class AppSettings
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>
        /// İş yeri adı (örn: "Kuyumcu Mehmet", "Çarşı Kuyumculuk")
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
    }
}
