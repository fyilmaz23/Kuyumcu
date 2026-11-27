# CI: mimari başına üretilmiş MSIX paketlerini (x64, x86, arm64) Microsoft Store'a yüklenecek tek bir
# .msixbundle dosyasında birleştirir. Store her bilgisayara kendi mimarisine uygun paketi kurar; yalnızca x64
# gönderilirse 32-bit Windows 10 ve Windows 10 ARM bilgisayarlarda "Windows sürümü en düşük gereksinimleri
# karşılamıyor" hatası alınır.
param(
    # İçinde yalnızca bu sürümün .msix dosyaları bulunan klasör
    [Parameter(Mandatory)] [string] $PackagesDir,
    [Parameter(Mandatory)] [string] $Version,      # 4 parçalı, paketlerle aynı: 2.0.0.0
    [Parameter(Mandatory)] [string] $OutputPath
)
$ErrorActionPreference = 'Stop'

$packages = @(Get-ChildItem $PackagesDir -Filter *.msix -File)
if ($packages.Count -eq 0) { throw "$PackagesDir içinde .msix bulunamadı." }
Write-Host "Pakete girecekler: $($packages.Name -join ', ')"

. (Join-Path $PSScriptRoot 'windows-sdk.ps1')
$makeappx = Join-Path (Get-WindowsSdkBin) 'makeappx.exe'

New-Item -ItemType Directory -Path (Split-Path -Parent $OutputPath) -Force | Out-Null
& $makeappx bundle /d $PackagesDir /bv $Version /p $OutputPath /o
if ($LASTEXITCODE -ne 0) { throw "makeappx bundle başarısız." }
Write-Host "MSIX paket demeti oluşturuldu: $OutputPath"
