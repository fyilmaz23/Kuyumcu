# CI: GitHub Secrets'tan derlemeye girecek gizli C# dosyalarını üretir.
# Bu dosyalar .gitignore'dadır; repoda asla bulunmaz.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Escape([string]$value) { return $value.Replace('\', '\\').Replace('"', '\"') }

$licenseKey = $env:LICENSE_SECRET_KEY
if ([string]::IsNullOrWhiteSpace($licenseKey)) {
    throw "LICENSE_SECRET_KEY secret tanımlı değil. Lisans anahtarları doğrulanamaz; yayın durduruldu."
}

$licensePath = Join-Path $root 'src/Kuyumcu.Core/Services/LicenseService.Secrets.cs'
@"
namespace Kuyumcu.Services
{
    public partial class LicenseService
    {
        private const string SECRET_KEY = "$(Escape $licenseKey)";
    }
}
"@ | Set-Content -Path $licensePath -Encoding utf8

$clientId = "$env:GOOGLE_DESKTOP_CLIENT_ID"
$clientSecret = "$env:GOOGLE_DESKTOP_CLIENT_SECRET"
if ([string]::IsNullOrWhiteSpace($clientId)) {
    Write-Warning "GOOGLE_DESKTOP_CLIENT_ID tanımlı değil: Google Drive yedeği için müşteriler kendi OAuth bilgilerini girmek zorunda kalır."
}

$productPath = Join-Path $root 'src/Kuyumcu.Core/Platform/ProductSecrets.Secrets.cs'
@"
namespace Kuyumcu.Platform;

public static partial class ProductSecrets
{
    public const string GoogleDesktopClientId = "$(Escape $clientId)";
    public const string GoogleDesktopClientSecret = "$(Escape $clientSecret)";
}
"@ | Set-Content -Path $productPath -Encoding utf8

Write-Host "Gizli yapılandırma dosyaları üretildi."
