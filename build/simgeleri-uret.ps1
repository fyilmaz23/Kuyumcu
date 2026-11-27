<#
  branding/app-icon.svg ve branding/app-icon-foreground.svg'den Windows simgelerini (MSIX görselleri,
  app.ico), arayüz favicon'unu ve branding/app-icon-512.png'yi yeniden üretir.
  SVG'leri Edge (yoksa Chrome) başsız modda her boyutta ayrı ayrı çizdirir; ek araç gerekmez.
  Android simgesi build sırasında MAUI tarafından SVG'den üretildiği için burada üretilmez.

  Kullanım:  powershell -ExecutionPolicy Bypass -File build/simgeleri-uret.ps1
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$icon = Join-Path $root 'branding/app-icon.svg'
$foreground = Join-Path $root 'branding/app-icon-foreground.svg'
$images = Join-Path $root 'src/Kuyumcu.Desktop/Packaging/Images'
$background = '#1F1A14'

$browser = @(
    "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Google\Chrome\Application\chrome.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $browser) { throw 'Edge veya Chrome bulunamadı.' }

$work = Join-Path ([IO.Path]::GetTempPath()) ('kd-simge-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null

# svg: çizilecek dosya; width/height: çıktı boyutu; art: svg'nin çizileceği kare boyut (ortalanır); bg: zemin rengi (yoksa şeffaf)
function Render-Svg([string]$svg, [string]$out, [int]$width, [int]$height, [int]$art = 0, [string]$bg = 'transparent') {
    if ($art -eq 0) { $art = [Math]::Min($width, $height) }
    $src = ([Uri]$svg).AbsoluteUri
    $html = Join-Path $work 'sayfa.html'
    Set-Content -Path $html -Encoding UTF8 -Value @"
<!doctype html><html><body style="margin:0;width:${width}px;height:${height}px;background:$bg;display:flex;align-items:center;justify-content:center;overflow:hidden">
<img src="$src" style="width:${art}px;height:${art}px;flex-shrink:0"></body></html>
"@
    $shot = Join-Path $work 'shot.png'
    if (Test-Path $shot) { Remove-Item $shot }
    # Başsız Edge ara sıra takılıyor: her denemeye ayrı profil, 30 sn süre; takılırsa kapatıp yeniden dene
    for ($try = 1; $try -le 3 -and -not (Test-Path $shot); $try++) {
        $profileDir = Join-Path $work "profil-$try-$([guid]::NewGuid().ToString('N'))"
        $edgeArgs = @('--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run', '--disable-extensions',
            '--force-device-scale-factor=1', '--allow-file-access-from-files', '--default-background-color=00000000',
            "--user-data-dir=`"$profileDir`"", "--window-size=$width,$height", "--screenshot=`"$shot`"", ([Uri]$html).AbsoluteUri)
        $p = Start-Process -FilePath $browser -ArgumentList $edgeArgs -PassThru -WindowStyle Hidden `
            -RedirectStandardError (Join-Path $work 'edge-err.txt') -RedirectStandardOutput (Join-Path $work 'edge-out.txt')
        if (-not $p.WaitForExit(30000)) {
            & taskkill.exe /PID $p.Id /T /F 2>&1 | Out-Null
            Start-Sleep -Milliseconds 500
            if (Test-Path $shot) { Remove-Item $shot }   # yarım kalmış olabilir
        }
    }
    if (-not (Test-Path $shot)) { throw "Çizilemedi: $out" }
    Move-Item -Force $shot $out
    Write-Host "  $out ($width x $height)"
}

try {
    Write-Host 'MSIX görselleri'
    Render-Svg $icon (Join-Path $images 'Square44x44Logo.png') 44 44
    Render-Svg $icon (Join-Path $images 'Square44x44Logo.targetsize-256_altform-unplated.png') 256 256
    Render-Svg $icon (Join-Path $images 'Square150x150Logo.png') 150 150
    Render-Svg $icon (Join-Path $images 'StoreLogo.png') 50 50
    Render-Svg $foreground (Join-Path $images 'Wide310x150Logo.png') 310 150 170 $background
    Render-Svg $foreground (Join-Path $images 'SplashScreen.png') 620 300 260 $background

    Write-Host 'Diğer'
    Render-Svg $icon (Join-Path $root 'src/Kuyumcu.UI/wwwroot/favicon.png') 64 64
    Render-Svg $icon (Join-Path $root 'branding/app-icon-512.png') 512 512

    Write-Host 'app.ico'
    $sizes = 16, 24, 32, 48, 64, 128, 256
    $pngs = foreach ($s in $sizes) {
        $p = Join-Path $work "ico-$s.png"
        Render-Svg $icon $p $s $s
        , [IO.File]::ReadAllBytes($p)
    }
    # ICO: 6 baytlık başlık + her boyut için 16 baytlık dizin girdisi + PNG verileri (Vista ve sonrası PNG girdileri okur)
    $ms = New-Object IO.MemoryStream
    $w = New-Object IO.BinaryWriter($ms)
    $w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $s = $sizes[$i]; $len = $pngs[$i].Length
        $w.Write([byte]($s % 256)); $w.Write([byte]($s % 256)); $w.Write([byte]0); $w.Write([byte]0)
        $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$len); $w.Write([uint32]$offset)
        $offset += $len
    }
    foreach ($png in $pngs) { $w.Write($png) }
    $w.Flush()
    $ico = Join-Path $root 'src/Kuyumcu.Desktop/Packaging/app.ico'
    [IO.File]::WriteAllBytes($ico, $ms.ToArray())
    Write-Host "  $ico ($($sizes -join ', '))"
}
finally {
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
