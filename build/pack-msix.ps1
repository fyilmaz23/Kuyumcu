# CI: dotnet publish çıktısından Microsoft Store'a yüklenecek MSIX paketini oluşturur.
# Windows SDK araçlarını (makepri, makeappx) kullanır; windows-latest çalıştırıcısında hazır gelir.
param(
    [Parameter(Mandatory)] [string] $PublishDir,
    [Parameter(Mandatory)] [string] $PackagingDir,
    [Parameter(Mandatory)] [string] $Version,      # 4 parçalı, Store için son parça 0 olmalı: 2.0.0.0
    [Parameter(Mandatory)] [string] $OutputPath
)
$ErrorActionPreference = 'Stop'

foreach ($name in 'STORE_PACKAGE_NAME', 'STORE_PUBLISHER', 'STORE_PUBLISHER_DISPLAY_NAME') {
    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) {
        throw "$name değişkeni tanımlı değil. Partner Center > Ürün kimliği sayfasındaki değerleri GitHub Variables'a girin (bkz. docs/YAYIN.md)."
    }
}

# Windows SDK araçlarını bul (en yeni sürüm)
$kits = 'C:\Program Files (x86)\Windows Kits\10\bin'
$sdkBin = Get-ChildItem $kits -Directory | Where-Object { $_.Name -match '^10\.' } |
    Sort-Object { [version]$_.Name } -Descending |
    ForEach-Object { Join-Path $_.FullName 'x64' } |
    Where-Object { Test-Path (Join-Path $_ 'makeappx.exe') } | Select-Object -First 1
if (-not $sdkBin) { throw "Windows SDK (makeappx.exe) bulunamadı." }
$makeappx = Join-Path $sdkBin 'makeappx.exe'
$makepri = Join-Path $sdkBin 'makepri.exe'

# Paket içeriği: yayın çıktısı + görseller + bildirim
$layout = Join-Path ([IO.Path]::GetTempPath()) "msix-layout-$([guid]::NewGuid())"
New-Item -ItemType Directory -Path $layout | Out-Null
Copy-Item -Path (Join-Path $PublishDir '*') -Destination $layout -Recurse
Copy-Item -Path (Join-Path $PackagingDir 'Images') -Destination $layout -Recurse

$manifest = Get-Content (Join-Path $PackagingDir 'AppxManifest.xml') -Raw
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

Remove-Item $layout -Recurse -Force
Write-Host "MSIX oluşturuldu: $OutputPath"
