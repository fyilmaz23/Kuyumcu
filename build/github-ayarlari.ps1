# GitHub Actions Secrets/Variables kurulumu (release.yml icin). Bkz. docs/YAYIN.md
# Sifreler ekranda gorunmez, dosyaya yazilmaz; dogrudan GitHub'a gonderilir.
# Bos birakilan alanlar atlanir: betik istenildigi kadar tekrar calistirilabilir
# (or. Partner Center hesabi acildiktan sonra sadece STORE_* degerleri icin).
param(
    [string]$Repo = 'fyilmaz23/Kuyumcu',
    [string]$KeystorePath = "$env:USERPROFILE\.kuyumcu-imza\upload.keystore",
    [string]$Keytool = 'C:\Program Files (x86)\Android\openjdk\jdk-17.0.14\bin\keytool.exe'
)

$gh = (Get-Command gh -ErrorAction SilentlyContinue).Source
if (-not $gh -and (Test-Path 'C:\Program Files\GitHub CLI\gh.exe')) { $gh = 'C:\Program Files\GitHub CLI\gh.exe' }
if (-not $gh) { Write-Error 'GitHub CLI bulunamadi. Kurun: winget install --id GitHub.cli'; exit 1 }

& $gh auth status --hostname github.com *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Host 'GitHub hesabina giris yapilacak (tarayici acilir)...' -ForegroundColor Cyan
    & $gh auth login --hostname github.com --git-protocol https --web
    if ($LASTEXITCODE -ne 0) { Write-Error 'GitHub girisi yapilamadi.'; exit 1 }
}

function Read-Secret([string]$Prompt) {
    $s = Read-Host $Prompt -AsSecureString
    $b = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($s)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($b) } finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($b) }
}
function Set-RepoSecret([string]$Name, [string]$Value) {
    & $gh secret set $Name --repo $Repo --body $Value
    if ($LASTEXITCODE -ne 0) { throw "$Name ayarlanamadi" }
    Write-Host "  Secret ayarlandi: $Name" -ForegroundColor Green
}
function Set-RepoVariable([string]$Name, [string]$Value) {
    & $gh variable set $Name --repo $Repo --body $Value
    if ($LASTEXITCODE -ne 0) { throw "$Name ayarlanamadi" }
    Write-Host "  Variable ayarlandi: $Name" -ForegroundColor Green
}

# --- 1) Android imzalama ---------------------------------------------------------------
Write-Host "`n[1/3] Android yukleme anahtari: $KeystorePath" -ForegroundColor Cyan
if ((Test-Path $KeystorePath) -and (Read-Host 'Android secret''lari ayarlansin mi? (E/h)') -notmatch '^[hH]') {
    $alias = Read-Host 'Anahtar takma adi (Enter = upload)'
    if ([string]::IsNullOrWhiteSpace($alias)) { $alias = 'upload' }
    $storePass = Read-Secret 'Keystore sifresi'
    & $Keytool -list -keystore $KeystorePath -storepass $storePass -alias $alias *> $null
    if ($LASTEXITCODE -ne 0) { Write-Error 'Keystore sifresi veya takma ad yanlis; Android secret''lari ayarlanmadi.'; exit 1 }
    $keyPass = Read-Secret 'Anahtar sifresi (Enter = keystore sifresiyle ayni)'
    if ([string]::IsNullOrEmpty($keyPass)) { $keyPass = $storePass }

    Set-RepoSecret 'ANDROID_KEYSTORE_BASE64' ([Convert]::ToBase64String([IO.File]::ReadAllBytes($KeystorePath)))
    Set-RepoSecret 'ANDROID_KEYSTORE_PASSWORD' $storePass
    Set-RepoSecret 'ANDROID_KEY_ALIAS' $alias
    Set-RepoSecret 'ANDROID_KEY_PASSWORD' $keyPass
    $storePass = $null; $keyPass = $null
} else { Write-Host '  Atlandi.' }

# --- 2) Google Drive OAuth (masaustu istemcisi) ----------------------------------------
Write-Host "`n[2/3] Google Cloud > KuyumcuApp > Kimlik bilgileri > Masaustu OAuth istemcisi" -ForegroundColor Cyan
$clientId = Read-Host 'GOOGLE_DESKTOP_CLIENT_ID (Enter = atla)'
if (-not [string]::IsNullOrWhiteSpace($clientId)) {
    $clientSecret = Read-Secret 'GOOGLE_DESKTOP_CLIENT_SECRET'
    Set-RepoSecret 'GOOGLE_DESKTOP_CLIENT_ID' $clientId.Trim()
    Set-RepoSecret 'GOOGLE_DESKTOP_CLIENT_SECRET' $clientSecret.Trim()
    $clientSecret = $null
} else { Write-Host '  Atlandi.' }

# --- 3) Microsoft Store kimligi (gizli degil) ------------------------------------------
Write-Host "`n[3/3] Partner Center > urun > Urun yonetimi > Urun kimligi" -ForegroundColor Cyan
$pkg = Read-Host 'STORE_PACKAGE_NAME (Package/Identity/Name, Enter = atla)'
if (-not [string]::IsNullOrWhiteSpace($pkg)) {
    $pub = Read-Host 'STORE_PUBLISHER (CN=...)'
    $disp = Read-Host 'STORE_PUBLISHER_DISPLAY_NAME'
    Set-RepoVariable 'STORE_PACKAGE_NAME' $pkg.Trim()
    Set-RepoVariable 'STORE_PUBLISHER' $pub.Trim()
    Set-RepoVariable 'STORE_PUBLISHER_DISPLAY_NAME' $disp.Trim()
} else { Write-Host '  Atlandi (Partner Center sonrasi tekrar calistirin).' }

Write-Host "`nGitHub'daki durum:" -ForegroundColor Cyan
& $gh secret list --repo $Repo
& $gh variable list --repo $Repo
