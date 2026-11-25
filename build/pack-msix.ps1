# CI: dotnet publish çıktısından Microsoft Store'a yüklenecek MSIX paketini oluşturur.
# Windows SDK araçlarını (makepri, makeappx) kullanır; windows-latest çalıştırıcısında hazır gelir.
param(
    [Parameter(Mandatory)] [string] $PublishDir,
    [Parameter(Mandatory)] [string] $PackagingDir,
    [Parameter(Mandatory)] [string] $Version,      # 4 parçalı, Store için son parça 0 olmalı: 2.0.0.0
    [Parameter(Mandatory)] [string] $OutputPath,
    # Yerel deneme: paket içeriğini bu klasörde bırakır; Geliştirici Modu açıkken
    # Add-AppxPackage -Register <klasör>\AppxManifest.xml ile imzasız kurulup denenebilir.
    [string] $LayoutDir
)
$ErrorActionPreference = 'Stop'

foreach ($name in 'STORE_PACKAGE_NAME', 'STORE_PUBLISHER', 'STORE_PUBLISHER_DISPLAY_NAME') {
    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) {
        throw "$name değişkeni tanımlı değil. Partner Center > Ürün kimliği sayfasındaki değerleri GitHub Variables'a girin (bkz. docs/YAYIN.md)."
    }
}

# Windows SDK araçlarını bul (en yeni sürüm). Önce kurulu Windows SDK, yoksa NuGet önbelleğindeki
# Microsoft.Windows.SDK.BuildTools paketi (WPF/WebView2 derlemesi bunu zaten indirir).
function Find-SdkBin([string] $root) {
    if (-not (Test-Path $root)) { return $null }
    Get-ChildItem $root -Directory -Recurse -Filter x64 -ErrorAction SilentlyContinue |
        Where-Object { Test-Path (Join-Path $_.FullName 'makeappx.exe') } |
        Sort-Object { [version]($_.Parent.Name -replace '[^\d\.]', '') } -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}
$nugetRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
$sdkBin = Find-SdkBin 'C:\Program Files (x86)\Windows Kits\10\bin'
if (-not $sdkBin) { $sdkBin = Find-SdkBin (Join-Path $nugetRoot 'microsoft.windows.sdk.buildtools') }
if (-not $sdkBin) { throw "Windows SDK (makeappx.exe) bulunamadı." }
Write-Host "Windows SDK araçları: $sdkBin"
$makeappx = Join-Path $sdkBin 'makeappx.exe'
$makepri = Join-Path $sdkBin 'makepri.exe'

# Paket içeriği: yayın çıktısı + görseller + bildirim
$layout = if ($LayoutDir) { $LayoutDir } else { Join-Path ([IO.Path]::GetTempPath()) "msix-layout-$([guid]::NewGuid())" }
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
New-Item -ItemType Directory -Path $layout | Out-Null
Copy-Item -Path (Join-Path $PublishDir '*') -Destination $layout -Recurse
Copy-Item -Path (Join-Path $PackagingDir 'Images') -Destination $layout -Recurse

# -Encoding UTF8: Windows PowerShell 5.1 varsayılan olarak ANSI okur ve Türkçe harfler bozulur
$manifest = Get-Content (Join-Path $PackagingDir 'AppxManifest.xml') -Raw -Encoding UTF8
$manifest = $manifest.Replace('__PACKAGE_NAME__', $env:STORE_PACKAGE_NAME).
    Replace('__PUBLISHER__', $env:STORE_PUBLISHER).
    Replace('__PUBLISHER_DISPLAY_NAME__', $env:STORE_PUBLISHER_DISPLAY_NAME).
    Replace('__VERSION__', $Version)
Set-Content -Path (Join-Path $layout 'AppxManifest.xml') -Value $manifest -Encoding utf8

# Kaynak dizini (ölçekli görseller için)
$priConfig = Join-Path ([IO.Path]::GetTempPath()) 'priconfig.xml'
& $makepri createconfig /cf $priConfig /dq tr-TR /o
if ($LASTEXITCODE -ne 0) { throw "makepri createconfig başarısız." }
& $makepri new /pr $layout /cf $priConfig /mn (Join-Path $layout 'AppxManifest.xml') /of (Join-Path $layout 'resources.pri') /o
if ($LASTEXITCODE -ne 0) { throw "makepri new başarısız." }

New-Item -ItemType Directory -Path (Split-Path -Parent $OutputPath) -Force | Out-Null
& $makeappx pack /d $layout /p $OutputPath /o
if ($LASTEXITCODE -ne 0) { throw "makeappx pack başarısız." }

if (-not $LayoutDir) { Remove-Item $layout -Recurse -Force }
Write-Host "MSIX oluşturuldu: $OutputPath"
