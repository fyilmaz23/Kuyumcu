# CI: dotnet publish çıktısından Microsoft Store'a yüklenecek MSIX paketini oluşturur.
# Windows SDK araçlarını (makepri, makeappx) kullanır; windows-latest çalıştırıcısında hazır gelir.
param(
    [Parameter(Mandatory)] [string] $PublishDir,
    [Parameter(Mandatory)] [string] $PackagingDir,
    [Parameter(Mandatory)] [string] $Version,      # 4 parçalı, Store için son parça 0 olmalı: 2.0.0.0
    [Parameter(Mandatory)] [string] $OutputPath,
    # Yayın çıktısının mimarisi (dotnet publish -r win-<mimari>). Paket bildirimine yazılır.
    [ValidateSet('x64', 'x86', 'arm64')] [string] $Architecture = 'x64',
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

. (Join-Path $PSScriptRoot 'windows-sdk.ps1')
$sdkBin = Get-WindowsSdkBin
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
    Replace('__VERSION__', $Version).
    Replace('__ARCH__', $Architecture)
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
Write-Host "MSIX oluşturuldu ($Architecture): $OutputPath"
