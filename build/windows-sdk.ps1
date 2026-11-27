# pack-msix.ps1 ve bundle-msix.ps1 tarafından nokta ile yüklenir (. ./windows-sdk.ps1).
# Windows SDK araçlarını (makeappx, makepri) bulur: en yeni sürüm. Önce kurulu Windows SDK, yoksa NuGet önbelleğindeki
# Microsoft.Windows.SDK.BuildTools paketi (WPF/WebView2 derlemesi bunu zaten indirir).
# Araçların x64 sürümü kullanılır: derleme makinesi x64'tür; hedef paket mimarisi (x86/arm64) bundan bağımsızdır.
function Find-SdkBin([string] $root) {
    if (-not (Test-Path $root)) { return $null }
    Get-ChildItem $root -Directory -Recurse -Filter x64 -ErrorAction SilentlyContinue |
        Where-Object { Test-Path (Join-Path $_.FullName 'makeappx.exe') } |
        Sort-Object { [version]($_.Parent.Name -replace '[^\d\.]', '') } -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}

function Get-WindowsSdkBin {
    $nugetRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
    $sdkBin = Find-SdkBin 'C:\Program Files (x86)\Windows Kits\10\bin'
    if (-not $sdkBin) { $sdkBin = Find-SdkBin (Join-Path $nugetRoot 'microsoft.windows.sdk.buildtools') }
    if (-not $sdkBin) { throw "Windows SDK (makeappx.exe) bulunamadı." }
    Write-Host "Windows SDK araçları: $sdkBin"
    return $sdkBin
}
