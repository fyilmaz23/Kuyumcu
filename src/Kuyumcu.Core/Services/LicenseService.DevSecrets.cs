namespace Kuyumcu.Services
{
    // SADECE YEREL GELİŞTİRME İÇİN.
    // Yayın derlemelerinde CI, GitHub Secrets'tan LicenseService.Secrets.cs dosyasını üretir
    // ve bu dosya derlemeden çıkarılır (bkz. Kuyumcu.Core.csproj).
    public partial class LicenseService
    {
        private const string SECRET_KEY = "DEV_ONLY_NOT_FOR_RELEASE";
    }
}
