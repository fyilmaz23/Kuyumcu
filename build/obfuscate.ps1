# Yayın çıktısındaki Kuyumcu.Core.dll'yi Obfuscar ile karıştırır (lisans kodunu okumayı/yamamayı zorlaştırmak için).
# Kullanım (dotnet publish'ten sonra, MSIX paketlemeden önce):
#   ./build/obfuscate.ps1 -PublishDir artifacts/desktop-publish -MappingPath artifacts/obfuscar-mapping.txt
#
# Ayarlar bilerek ihtiyatlı:
# - Public API korunur (Kuyumcu.UI ve masaüstü Core'u public adlarla kullanır).
# - Özellik ve olay adları DEĞİŞMEZ: SQLite tabloları, JSON durum dosyaları ve Google API modelleri ada göre çalışır.
# - Değişen: private/internal tip, metot ve alan adları (ör. lisans doğrulamanın iç adımları); metinler gizlenir
#   (alan öneki, açık anahtar, hata metinleri "strings" aramasıyla bulunamaz).
# Eşleme dosyası (MappingPath) hata ayıklama için saklanmalı ama pakete/depoya KONMAMALI.
param(
    [Parameter(Mandatory)] [string] $PublishDir,
    [Parameter(Mandatory)] [string] $MappingPath
)
$ErrorActionPreference = 'Stop'

$publish = (Resolve-Path $PublishDir).Path
$target = Join-Path $publish 'Kuyumcu.Core.dll'
if (-not (Test-Path $target)) { throw "Kuyumcu.Core.dll bulunamadı: $target" }

$work = Join-Path ([IO.Path]::GetTempPath()) "obfuscar-$([guid]::NewGuid())"
$outDir = Join-Path $work 'out'
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

$config = @"
<?xml version="1.0" encoding="utf-8"?>
<Obfuscator>
  <Var name="InPath" value="$publish" />
  <Var name="OutPath" value="$outDir" />
  <Var name="KeepPublicApi" value="true" />
  <Var name="HidePrivateApi" value="true" />
  <Var name="RenameProperties" value="false" />
  <Var name="RenameEvents" value="false" />
  <Var name="RenameFields" value="true" />
  <Var name="HideStrings" value="true" />
  <Var name="ReuseNames" value="true" />
  <Var name="UseUnicodeNames" value="false" />
  <Var name="SuppressIldasm" value="true" />
  <Var name="RegenerateDebugInfo" value="false" />
  <Var name="LogFile" value="$(Join-Path $outDir 'Mapping.txt')" />
  <AssemblySearchPath path="$publish" />
  <Module file="`$(InPath)\Kuyumcu.Core.dll" />
</Obfuscator>
"@
$configPath = Join-Path $work 'obfuscar.xml'
Set-Content -Path $configPath -Value $config -Encoding utf8

Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    & dotnet tool restore | Out-Null
    & dotnet obfuscar.console $configPath
    if ($LASTEXITCODE -ne 0) { throw "Obfuscar başarısız (çıkış kodu $LASTEXITCODE)." }
}
finally {
    Pop-Location
}

$obfuscated = Join-Path $outDir 'Kuyumcu.Core.dll'
if (-not (Test-Path $obfuscated)) { throw "Karıştırılmış Kuyumcu.Core.dll üretilmedi." }
Copy-Item $obfuscated $target -Force
# Hata ayıklama sembolü karıştırılmış dosyayla uyuşmaz; yayında olmamalı
Remove-Item (Join-Path $publish 'Kuyumcu.Core.pdb') -ErrorAction SilentlyContinue

New-Item -ItemType Directory -Path (Split-Path -Parent ([IO.Path]::GetFullPath($MappingPath))) -Force | Out-Null
Copy-Item (Join-Path $outDir 'Mapping.txt') $MappingPath -Force
Remove-Item $work -Recurse -Force
Write-Host "Kuyumcu.Core.dll karıştırıldı. Eşleme: $MappingPath"
