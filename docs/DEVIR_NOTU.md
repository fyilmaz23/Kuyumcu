# Devir Notu — Kuyumcu Defteri

Bu belge, projeyi başka bir oturumda / geliştiricide kaldığı yerden sürdürmek için gereken bağlamı içerir.
Son güncelleme: dal `claude/android-yeniden-2ef27f` = güncel `kuyumcudefteri` (barkodlu stok dahil, bölüm 7–8) + yeni Android uygulaması (bölüm 9).

## Amaç

Tek müşteriye ("Çarşı Kuyumculuk") yazılmış uygulamayı, birden çok kuyumcuya satılabilecek bir **ürüne**
dönüştürmek ve arayüzünü yenilemek.

- **Windows** (tam sürüm): WPF + BlazorWebView, **Microsoft Store**'dan MSIX olarak dağıtılacak (kayıt ücretsiz,
  Store imzalar, lisans uygulama içinde satılır → komisyon yok).
- **Android** (salt okunur): .NET MAUI + BlazorWebView, telefona özel arayüz, Drive'daki son yedeği kendisi indirir;
  **Google Play**'e AAB (hedef API 36). Paket `com.kuyumcudefteri.app` (kullanıcı kararı, 24.11.2026).
- **.NET 10** (LTS). Teknoloji kararları ve gerekçeleri aşağıda.

## Proje yapısı

```
src/Kuyumcu.Core      Modeller + servisler (SQLite, lisans, yedek, Drive, görsel üretimi). net10.0, platformdan bağımsız
src/Kuyumcu.UI        Ortak Blazor arayüzü (Razor Class Library): sayfalar, tema, bileşenler, app.css, app.js
src/Kuyumcu.Desktop   WPF kabuğu (net10.0-windows10.0.19041.0, self-contained, win-x64) + Packaging/AppxManifest.xml
src/Kuyumcu.Android   Android uygulaması (MAUI + BlazorWebView, net10.0-android), salt okunur; kendi telefon arayüzü
                      (Components/), Kuyumcu.UI'yi KULLANMAZ. Kök ad alanı Kuyumcu.Droid, derleme adı KuyumcuDefteri
src/Kuyumcu.Preview   Blazor Server tarayıcı önizlemesi (Mac/Linux'ta arayüz geliştirmek için, örnek verili) — dağıtılmaz
tests/Kuyumcu.Core.Tests  xUnit birim testleri (kod üretimi, TSPL, toplamlar, fiyat sitesi, stok servisi, yedek/geri yükleme)
build/                CI betikleri: write-secrets.ps1 (gizli dosyaları üretir), pack-msix.ps1 (makeappx ile MSIX)
tools/Kuyumcu.LicenseTool  Lisans anahtarı üretme aracı (geliştirici bilgisayarında; bkz. docs/YAYIN.md "Lisans anahtarları")
docs/YAYIN.md         Partner Center, Play Console (OAuth Android istemcisi, Veri güvenliği, kapalı test), GitHub Secrets
docs/GIZLILIK.md      Gizlilik politikası taslağı (yayınlanıp URL'si mağazalara girilecek)
docs/ETIKET_VE_OKUYUCU_TESTI.md  Etiket yazıcısı (TSC TE210) ve barkod okuyucu (Sunlux RH10) deneme adımları
```

- Platforma özgü her şey `src/Kuyumcu.Core/Platform/IPlatformServices.cs` arayüzlerinin arkasında:
  `IAppEnvironment`, `ISecureStore` (Windows: DPAPI), `IFilePickerService`, `IClipboardService`, `IShareService`,
  `IReceiptPrinter`, `ILabelPrinter` (etiket yazıcısı, ham komut), `ILegacyDataLocator`. Her kabuk projesi kendi
  uygulamasını kaydeder (Desktop, Preview). Android (`Kuyumcu.Android`) `AddKuyumcuCore` çağırmaz; sadece kullandığı
  servisleri kaydeder (görsel üretimi, fiyat kaynağı) — etiket/yazıcı/lisans servisleri yoktur.
- Ürün adı/klasör adı tek yerde: `src/Kuyumcu.Core/Platform/ProductInfo.cs`.
- Paket sürümleri merkezi: `Directory.Packages.props`.
- Gizli dosyalar (gitignore'da, CI üretir): `src/Kuyumcu.Core/Services/LicenseService.Secrets.cs`,
  `src/Kuyumcu.Core/Platform/ProductSecrets.Secrets.cs`. Yoksa `*.DevSecrets.cs` varsayılanları derlenir.
- `LicenseKeyGenerator.razor` geliştiricinin bilgisayarında, gitignore'da (`src/Kuyumcu.UI/Components/Pages/`).

## Yapılanlar

### 1. Geçiş ve ürünleştirme
- MAUI tek proje → Core / UI / Desktop (WPF) / Mobile (Android) yapısı, .NET 10.
- İlk müşterinin ekeo fiyat sitesi şifresi koddan çıkarıldı → Ayarlar (AppSettings) ve yedekle telefona gider.
- Çarşı Kuyumculuk şablonu ürüne gömülmüyor → Ayarlar'dan yüklenir (`musteri-varliklari/` altında duruyor).
- Android paket adı/etiketi nötr (`com.kuyumcudefteri.app`), gereksiz medya izinleri kaldırıldı, allowBackup=false.
- Google Drive: ürünün kendi OAuth istemcisi (CI secret, `drive.file` kapsamı); müşteri girişi "Gelişmiş" altında.
- Günlük yerel yedek düzeltildi (eskiden yanlış dosyayı yedekliyordu): `VACUUM INTO`, 30 gün,
  Windows'ta `Belgeler\Kuyumcu Defteri Yedekleri`.
- Yedek geri yükleme artık başka kurulumun lisansını taşımıyor; öncesinde otomatik yerel yedek alınıyor.
- Eski sürümden (`com.companyname.kuyumcu` MSIX paketi) veriyi bulup tek tıkla aktarma (Kurulum + Ayarlar).
- SQLite yerel kütüphanesi güvenlik açığı olmayan sürüme yükseltildi; kültür tr-TR'ye sabit.
- CI: `ci.yml` (her push'ta derleme), `release.yml` (`vX.Y.Z` etiketiyle MSIX + imzalı AAB).

### 2. Yeni arayüz (ilk tur)
- Tema: antrasit + altın, açık/koyu mod (`src/Kuyumcu.UI/Theme/KuyumcuTheme.cs`, `wwwroot/css/app.css`).
- Sol menü (daraltılabilir), üst bar: müşteri arama (Ctrl+K), tema, "Yeni Müşteri" (her sayfadan pencere).
- Ortak bileşenler (`Components/Shared`): PageHeader, StatCard, BalanceAmount, TypeBadge, Avatar, EmptyState, BrandLogo.
- Yenilenen sayfalar: Ana Sayfa, Müşteriler (bakiye sütunu), Müşteri Detayı (F2 Giden / F3 Gelen, bakiye
  kutucukları, "Kalan" sütunu düzeltildi, yazıcı menüsü), İşlem penceresi, İşlemler.
- Düzeltme: emanetler artık para birimi kartlarında listelenmiyor (sadece Emanetler bölümünde).

## Korunması gereken iş kuralları
- **Giden** = `TransactionType.CustomerDebt` (müşteriye verildi, müşteri borçlu) → kırmızı.
  **Gelen** = `StoreDebt` (müşteriden alındı) → yeşil. Net bakiye > 0 ise Giden.
- **Emanet** sadece Gelen işlemlerde olur, bakiyeye girmez, ayrı bölümde gösterilir.
- **Gizli işlemler:** müşteri detayında müşterinin **adına tıklamak** gizli işlemleri gösterir/gizler (bilerek gizli kısayol).
- İşlem kaydetmeden önce **onay penceresi**; yeni işlemde **aynı gün mükerrer** kontrolü.
- Lisans: kurulumu tamamlanmış ama LicenseInfo'su olmayan veritabanı "güncelleme yapan eski kullanıcı" sayılır
  → deneme süresi bitmiş başlar (kasıtlı).
- Lisans anahtarları imzalıdır (bölüm 5). `LicensePublicKey.Base64` değişirse **tüm** verilmiş lisanslar geçersiz olur.
  Eski biçim (`KUYM-...`, HMAC) anahtarlar ve DB'deki `IsLicensed` alanı **kabul edilmez** (kullanıcı kararı, 24.11.2026).
- Lisansa isteğe bağlı bitiş tarihi konabilir (kullanıcı kararı); bitişe 15 gün kala uyarı gösterilir.

### 3. İlk Windows çalıştırması (22.11.2026)
- Uygulama Windows'ta açılıyor; eski sürümden aktarma çalıştı (889 müşteri / 14.864 işlem, eski DB'ye dokunulmadı).
- Düzeltilen hatalar:
  - **Açılışta çökme** "Could not load Microsoft.Windows.SDK.NET": WebView2 SDK'sı .NET 5+ WPF için Windows SDK
    projeksiyonu istiyor → Desktop hedefi `net10.0-windows10.0.19041.0` (Min OS 10.0.17763, MSIX ile aynı).
  - **"Yükleniyor..."da takılma:** Desktop `index.html`'de `autostart="false"` vardı; WPF Blazor'u kendisi başlatmaz.
    (Android'de MAUI başlattığı için orada kalıyor.)
  - **Deneme süresinin 2. açılışta bitmesi:** SecureStore'daki UTC tarihler `DateTime.TryParse` ile yerel saate (+3)
    çevriliyor, "saat geri alınmış" sanılıyordu → `LicenseService.TryParseUtc`. Lisanslı kullanıcıları etkilemiyordu.
  - **Yazı tipi:** `FontStack.Split(',')` tırnaklı/boşluklu adlar üretiyordu, Windows'ta Times'a düşüyordu → dizi.
  - Aktarma/geri yükleme doğrulaması kaynak DB'yi artık salt okunur açıyor.
  - Desktop'taki gereksiz `ProtectedData` / `System.Drawing.Common` paket referansları kaldırıldı (NU1510).
- `LicenseKeyGenerator.razor` yeni yapıya uyarlandı (yerelde, gitignore'da). Bağlantısı Ayarlar > Lisans ve
  "Deneme Süresi Doldu" ekranında, **sadece dosya derlemede varsa** görünür; süre dolmuşken de açılabilir.
- Bu geliştirme bilgisayarındaki eski kurulum lisans sisteminden önceki sürüm (LicenseInfo tablosu yok) →
  aktarımdan sonra kasıtlı kural gereği "Deneme Süresi Doldu" gelir.

### 4. Üst menü (24.11.2026, kullanıcı isteği)
- Sol menü kaldırıldı (`NavMenu.razor` silindi). `MainLayout`: antrasit tek satır üst bar (52 px) — marka/iş yeri adı,
  ikonlu sayfa düğmeleri (üzerine gelince başlık + kısayol), müşteri arama, deneme süresi rozeti, Drive yedek,
  tema, Ayarlar, "Yeni Müşteri". Kısayollar: Alt+1 Ana Sayfa, Alt+2 Müşteriler, Alt+3 İşlemler, Alt+4–7 stok sayfaları (bölüm 8),
  Alt+8 Ayarlar. Alt+rakam sadece üst sıradaki rakam tuşlarıyla çalışır (Numpad değil: bazı okuyucular Alt+Numpad ile yazar).
- `.kd-page` genişlik sınırı kaldırıldı (tam ekran). Dar pencerede önce iş yeri adı, sonra düğme yazısı gizlenir.

### 5. İmzalı lisans + Ayarlar/Kurulum/Lisans ekranları (24.11.2026)
- `Kuyumcu.Core/Licensing`: `LicenseCodec` (biçim: `KD1-` + Crockford Base32, 14 bayt yük + 64 bayt ECDSA P-256 imza;
  yük = sürüm, cihaz kimliği, veriliş günü, isteğe bağlı bitiş günü), `LicensePublicKey` (depoda), `LicenseSigningKey`
  (özel anahtar `%USERPROFILE%\.kuyumcu-lisans\ozel-anahtar.pem`, depo dışında; kullanıcının bilgisayarında üretildi).
- `tools/Kuyumcu.LicenseTool`: `anahtar-olustur`, `uret <cihaz> [--gun N|--bitis GG.AA.YYYY] [--not]`, `coz`, `acik-anahtar`.
  Her üretim `lisans-kayitlari.csv`'ye yazılır.
- `LicenseService`: durum her sorguda imza + cihaz + süre doğrulanarak hesaplanır (`GetLicenseStateAsync` → `LicenseState`,
  kilit nedeni: deneme bitti / lisans süresi doldu / anahtar geçersiz). Cihaz kimliği her seferinde MachineGuid'den
  türetilir (güvenli depodaki değer sadece yedek). Süreli lisansta "bugün" = max(şimdi, son kontrol) → saat geri alınarak
  uzatılamaz. Etkinleştirme bu bilgisayarın kimliğine göre doğrular (aktarılmış DB'deki eski kimlik sorunu çözüldü).
- Yeni kurulumda, aynı Windows kullanıcısında önceki deneme başlangıcı varsa o kullanılır; `DpapiSecureStore` artık
  `HKCU\Software\KuyumcuDefteri` altına da yazar (veri klasörü silinince deneme sıfırlanmaz; MSIX kaldırmada silinir).
- CI'dan `LICENSE_SECRET_KEY` ve `LicenseService.DevSecrets.cs` kaldırıldı.
- Ekranlar: ortak `Shared/LicensePanel.razor`; Ayarlar iki sütun (sol: İş Yeri, Altın Fiyatı, Drive — Kaydet başlıkta;
  sağ: Lisans, Yerel Yedekler, Yedeği İçe Aktar); Kurulum (eski veri varsa "Önerilen" seçenek); "Lisans gerekli" ekranı.
  Üst menüde süreli lisans bitmek üzereyken "Lisans: N gün" rozeti; etkinleştirince `AppState.LicenseChanged` ile yenilenir.
- Geliştirici sayfası `LicenseKeyGenerator.razor` (gitignore'da) özel anahtar dosyasını okuyarak üretir.
- Test için deneme süresini sıfırlamak: uygulamayı kapat, `%LOCALAPPDATA%\KuyumcuDefteri` klasörünü VE
  `HKCU\Software\KuyumcuDefteri` anahtarını kaldır.

### 6. Otomatik Google Drive yedeği (24.11.2026)
- `CloudBackupService` (Core): 15 dk'da bir bakar; son yedekten/kontrolden 2 saat geçtiyse VACUUM INTO ile anlık kopya
  alır, MD5'i son yüklenenle aynıysa yüklemez (VACUUM INTO çıktısı aynı veride birebir aynı — denendi). Hata olursa
  15 dk sonra tekrar dener; oturum geçersizse (invalid_grant/401) durur ve "Yeniden bağlanın" der. Masaüstü kapanırken
  `FlushOnExitAsync` (en fazla 20 sn). Durum: `%LOCALAPPDATA%\KuyumcuDefteri\cloud-backup.json`.
- `GoogleDriveService`: giriş SADECE "Google Hesabını Bağla" ile (tarayıcı); otomatik yedek kayıtlı oturumu sessizce
  kullanır, asla tarayıcı açmaz. Drive'da "Kuyumcu Defteri Yedekleri" klasörü: `KuyumcuDefteri_yyyy-MM-dd_HHmm.db3`
  (Drive MD5'i ile doğrulanır) + her seferinde güncellenen `KuyumcuDefteri_son_yedek.db3` (telefon için, sabit dosya
  kimliği). Dosyalar `appProperties.kuyumcuKind` (snapshot/latest) ile ayırt edilir.
- Saklama: en yeni 10 + son 14 günün her günü + son 12 ayın her ayı; diğerleri Drive çöpüne (30 gün geri alınabilir).
  Eski sürümün Drive kökündeki `*_Kuyumcu_Yedek.db3` dosyalarına dokunulmaz.
- Arayüz: üst menüde `CloudBackupIndicator` (yeşil güncel / altın uyarı / kırmızı hata / soluk kapalı; tıklayınca hemen
  yedekler ya da Ayarlar'a götürür). Ayarlar > Google Drive kartı: hesap, son yedek, Şimdi Yedekle, Drive'da Aç,
  Bağlantıyı Kes. Eski onaylı elle yedek düğmesi kaldırıldı.

### 7. Altın fiyatı kaynağı (25.11.2026)
- ekeo (fiyat.ekeo.org.tr) girişi kaldırıldı. Fiyatlar **https://elazigaltinfiyatlari.com/** sayfasından şifresiz alınır
  (`Services/GoldPriceSource.cs`: `IGoldPriceSource` + `ElazigAltinFiyatlariSource`; "BUGÜN" tablosu, `span.alis/satis`).
  14 Ayar'ın sadece satış fiyatı var (alış = satış). Başka şehir için yeni bir `IGoldPriceSource` yazılır.
- `GoldPriceExportService` fiyatları 5 dk önbellekte tutar; `GetHasGoldPriceAsync()` = "24 Ayar" satış (has fiyatı).
- `AppSettings.GoldPriceSiteCode/Password` kolonları eski veritabanlarıyla uyum için duruyor, kullanılmıyor.

### 8. Barkodlu vitrin stoğu (dal `feature/barkod-stok`, 24–25.11.2026)
İlk kullanıcı dükkânın cihazları: **TSC TE210** (203 dpi, TSPL, USB), **Sunlux RH10** (2D, USB klavye gibi, sonuna Enter),
**72 × 10 mm kelebek PP kuyumcu etiketi** (kanatlar basılır, kuyruk yapışkansız). Yazıcı/etiket/kategori ayarları
koda gömülü değil, Ayarlar'dan değişir.

**Core**
- `Models/Stock.cs`: `StockItem` (etiketli ürün), `StockMovement` (her hareket), `StockCategory`, `KaratDefinition`
  (ayar → milyem), `StockLocation`, `BulkProduct` (kasa: çeyrek, yarım, tam, gram altın…), `StockCountSession` +
  `StockCountLine` (sayım), `StockCodeCounter`. `Models/LabelPrinterSettings.cs` (tek satır, cihaza özel).
- Tablolar `DatabaseService.CreateTablesAsync` ile oluşur (InitializeAsync ve UpdateDatabaseSchemaAsync ortak);
  boş tablolara `Stock/StockDefaults.cs` varsayılanları eklenir. `DatabaseService.GetConnectionAsync()` (internal)
  stok servislerine aynı bağlantıyı verir.
- `Services/StockService.cs` (ürün, hareket, kasa, tanımlar, etiket ayarı), `StockCountService.cs` (sayım),
  `LabelPrintService.cs` (yerleşim → dil → USB/TCP), `ReportExportService.cs` (CSV), `DatabaseImportService
  .GetImportedStockAsync()` (Android).
- `Stock/`: `ProductCode` (kod üretimi/denetimi), `WeightFormat` (gram/milyem okuma-yazma), `StockCalculator`
  (toplam, has, fiyat önerisi, sayım karşılaştırması). Hepsi saf; birim testli.
- `Labels/`: dilden bağımsız `LabelDocument` (nokta cinsinden öğeler) ← `LabelLayout` (ürün / test / hizalama
  etiketi) → `ILabelLanguage` (`TsplLanguage`, `ZplLanguage` — bölüm 15; yeni dil = yeni uygulama + `LabelPrinterLanguage` değeri).
- Platform: `ILabelPrinter`. Desktop `WindowsLabelPrinter` (winspool.drv P/Invoke, "RAW"; GDI yok), (eski Mobile)
  `UnsupportedLabelPrinter`, Preview `FileLabelPrinter` (`.preview-data/labels/*.tspl`). Ağ (TCP 9100) Core'da.

**UI** (`Components/Pages/Stok`; üst menüde Vitrin Stoku, Okut / Satış, Ürün Girişi, Sayım; Kasa ve Stok Raporları
Vitrin Stoku sayfasının üst düğmelerinde)
- `/stock` liste + ayar bazında adet/gram/has, `/stock/entry` seri giriş, `/stock/item/{id}` detay,
  `/stock/scan` okut/satış/iade, `/stock/count` sayım, `/stock/bulk` kasa, `/stock/reports` raporlar.
- Ayarlar: `LabelPrinterSettingsSection` (yazıcı, ölçüler, kaydırma, kod tipi, konumlar, önizleme, test ve hizalama
  etiketi) ve `StockDefinitionsSection` (kategori, ayar/milyem, konum, kasa ürünleri).
- Okutma: `Shared/ScanInput.razor` + `app.js` `kuyumcu.attachScanInput`. Sadece Enter'la gelen giriş işlenir; değer
  JS'de okunup alan hemen temizlenir (hızlı okutmada karakter/okutma kaybolmaz), geçersizse geri yazılır. Sayfa
  açılınca ve pencere kapanınca alan seçilir. Okutma ekranlarında odak bir giriş alanında değilken gelen rakamlar
  okutma alanına yönlendirilir (pencere açıkken ya da başka alana yazılırken karışmaz). Global kısayollar (Ctrl+K,
  F2/F3) rakam ve Enter'a dokunmaz.
- Android: eski `ImportData` sayfası ve `Shared/ImportedStockView.razor` bölüm 12'de kaldırıldı. **Yeni Android
  uygulamasında (bölüm 9) vitrin stoğu yok**; eklenirse `Core/Services/BackupSnapshot` üzerinden okunmalı.

### Barkod/stok iş kuralları (korunmalı)
- **Ürün kodu:** 12 hane, sadece rakam = "2" + 10 hane sıra + 1 GS1 mod-10 kontrol hanesi. Kodda gram/fiyat yok.
  Benzersiz (unique index) ve **asla yeniden kullanılmaz**: sonraki = max(sayaç, tablodaki en büyük kod) + 1;
  silinen ürün satırı (IsDeleted) kodu tutar. **Geri yüklemede sayaç geri sarılmaz** (büyük olan korunur) — eski
  yedek dönse bile basılmış etiketlerin kodu başka ürüne verilmez.
- **Ağırlık tam sayı mg** (`WeightMg`), milyem **binde** (916 = 0,916). Gram girişi virgül ya da nokta, en çok 3
  ondalık, > 0. Etikete **brüt** gram 2 ondalık basılır; has hesabı **net** (brüt − taş) üzerinden.
- **Etikette fiyat yok.** DataMatrix (ayardan QR), kod rakamla (6+6), gram + ayar + isteğe bağlı kısa kategori;
  dükkân adı isteğe bağlı (varsayılan kapalı). Yazılar Windows-1254 ile gider; TE210 Türkçe harfleri düzgün basıyor; sadeleştirme varsayılan kapalı.
- **Her stok değişikliği** ürünle aynı transaction'da `StockMovement`'a yazılır (giriş, satış, iade, düzeltme +
  değişen alanlar, silme + neden, kayıp/bulundu + sayım no, kasa giriş/çıkış/düzeltme). Silme = `IsDeleted`.
- **Sayım:** bitirmek ürünü kayıp yapmaz; eksikleri seçip "kayıp yap" ayrı, onaylı adımdır. Aynı kod ikinci kez sayılmaz.
- **Satış:** fiyat isteğe bağlı; öneri = net gram × (ayar milyemi + kategori işçilik milyemi) × has fiyatı (canlı,
  elle değiştirilebilir), 10 TL'ye yuvarlanır. **Veresiye**: defterde müşteriye **Giden** işlem (TL ya da 22 ayar gram;
  22 ayar karşılığı = net × (milyem + işçilik) / 22 ayar milyemi); defterin **mükerrer kontrolü ve onay penceresi**
  aynen uygulanır. İadede bu işlem isteğe bağlı silinir.
- **Kasa satışı veresiye** (bölüm 16): Kasa → Çıkış'ta "Müşteri hesabına yaz" → defterde **Giden** işlem; borç cinsi
  **her satışta seçilir** (ürünün kendi cinsi / TL / 22 ayar gram). Aynı mükerrer kontrolü ve onay penceresi.
  Kasa hareketinde müşteri ve işlem kimliği saklanır. **İade:** kasa geçmişinde veresiye satışın yanında "İade al";
  mal kasaya geri girer (`BulkReturn`), defterdeki işlem isteğe bağlı silinir (varsayılan işaretli, etiketli ürün
  iadesiyle aynı). Sadece müşteriye bağlı çıkış iade alınır; aynı satış iki kez iade alınamaz.
- **Kim sattı:** dükkânda tek kullanıcı (kullanıcı kararı, 26.11.2026); `StockMovement.UserName` boş kalır.
- **Cihaza özel:** `LabelPrinterSettings` geri yüklemede bu bilgisayarınki korunur.
- **Android:** salt okunur ayrı uygulama (bölüm 9); stokta etiket basma ve stok değiştirme yok.
- **Lisans:** modül normal lisansın parçası; ayrı özellik bayrağı yok (imzalı lisans, bölüm 5;
  stok modülü `LicenseService`'e dokunmaz).

### 9. Android uygulaması sıfırdan (24.11.2026)
Kullanıcı kararları: .NET MAUI (Core paylaşılsın), aynı depo, paket `com.kuyumcudefteri.app`, Drive'a Google ile
otomatik erişim (+ dosya seçici yedek yol).
- Eski `src/Kuyumcu.Mobile` → `src/Kuyumcu.Android` (git mv; MudBlazor ve Kuyumcu.UI bağımlılığı yok, telefona özel
  Razor bileşenleri + `wwwroot/css/app.css`). Antrasit üst/alt çubuk + altın vurgu, açık/koyu tema telefonu izler.
  Ekranlar: Özet (`/`: müşteri sayıları, para birimi başına toplam Giden/Gelen, son işlemler), Müşteriler
  (`/musteriler`: Türkçe harf/şapka duyarsız arama, telefonla arama, Borçlu/Alacaklı filtresi, sanal liste),
  Müşteri detayı (`/musteriler/{id}`: bakiye kutucukları, emanetler ayrı, cins başına hareketler yeniden eskiye +
  "Kalan", isme dokununca gizli işlemler, özet görselini paylaş), Fiyatlar (`/fiyatlar`: elazigaltinfiyatlari.com
  fiyatları şifresiz; "Fiyat listesini paylaş" listeyi **arka plan resmine** yazar — bkz. aşağıdaki not),
  Yedek (`/yedek`: Drive bağlantısı, telefondaki yedeğin bilgileri, dosyadan aç, telefondan sil, gizlilik notu).
  İlk açılışta `Welcome`: Google ile bağlan / Yedek dosyasını seç / Örnek verilerle dene.
- Core'a eklenenler (masaüstü kullanmıyor, davranışı değişmedi):
  - `CustomerLedger`: müşteri detayındaki bakiye/kalan/emanet/gizli kurallarının platformdan bağımsız kopyası
    (masaüstü `CustomerDetail.razor` ile AYNI kural; ileride masaüstü de bunu kullanırsa kural tek yerde kalır).
    `NetBalances`: liste/özet için (emanet hariç, **gizli dahil** — masaüstü `GetCurrencyBalanceSummaryAsync` gibi).
  - `BackupSnapshot`: .db3'ü salt okunur açıp belleğe alır; bu uygulamanın yedeği değilse `InvalidDataException`.
  - `DriveLatestBackupClient`: düz REST (Google API kütüphanesi değil), `appProperties.kuyumcuKind='latest'` ile, yoksa
    ada göre arar; en yeni `modifiedTime`'lıyı alır. JSON kaynak üretici ile (kırpmaya dayanıklı).
  - `DemoBackup`: örnek verili .db3 üretir ("Örnek verilerle dene", Play incelemesi için).
  - `ProductInfo.DriveKind*` sabitleri (masaüstü `GoogleDriveService` de bunları kullanıyor, değerler aynı).
- Google girişi: `Platform/GoogleDriveAuthorizer.cs` — Google Identity Services **Authorization API**
  (`Xamarin.GooglePlayServices.Auth` 122.0.0). Uygulamada istemci kimliği yok; Cloud projesindeki Android istemcisi
  paket adı + SHA-1 ile eşlenir (bkz. YAYIN.md 2.2). İzin ekranı `StartIntentSenderForResult` + `MainActivity.OnActivityResult`.
  Bu paket daha yeni AndroidX çektiği için 5 adet `-ktx`/lifecycle paketi `Directory.Packages.props`'ta hizalandı
  (yoksa "Type ... is defined multiple times" dex hatası).
- Eşitleme: `Services/BackupSyncService.cs` — açılışta ve uygulamaya dönüşte (en sık 2 dk) sessiz kontrol, MD5 aynıysa
  indirmez; yeni dosya önce doğrulanır, sonra eskisinin yerine konur (bozuk indirme eldeki yedeği silmez).
  `Services/BackupStore.cs` — `AppDataDirectory/yedek/son_yedek.db3` + `son_yedek.json` (kaynak, bilgisayardaki yedek
  zamanı = Drive `modifiedTime`, indirilme, son kontrol, MD5). İnternet yoksa son indirilen gösterilir, ekranda
  "Bilgisayardaki yedek: 12 dk önce" + tarih yazar.
- Android: `allowBackup=false` + `data_extraction_rules.xml` (Android 12+ cihaz aktarımı da kapalı), izinler sadece
  INTERNET + ACCESS_NETWORK_STATE; `tel:` için DIAL sorgusu.
- CI: `ci.yml`'e Android derleme işi; `release.yml` yeni projeye göre, Android işinde `write-secrets.ps1` **bilerek yok**.
- `MainPage.xaml`: `SafeAreaEdges="Container"` — olmadan içerik durum/gezinme çubuklarının altına giriyordu (kenardan kenara).
- **Denendi (24.11.2026, Pixel 7 API 34 emülatörü, Debug APK):** karşılama ekranı, "Örnek verilerle dene", Özet,
  Müşteriler (liste/filtre), Müşteri detayı (kutucuklar, hareketler + Kalan, emanetler), özet görselini paylaşma
  (paylaşım menüsü açıldı), Android geri tuşu, Yedek sayfası, "Dosyadan aç" (SAF ile .db3 seçildi, yedek zamanı
  dosya adından okundu), koyu tema, Google izin akışının başlaması (hesap seçici açıldı) ve iptal mesajı.
  Core okuyucu gerçek veritabanının kopyasıyla (872 müşteri / 14.309 işlem) bilgisayarda denendi: 0,4 sn, bakiye ve
  Kalan hesaplarında uyuşmazlık yok.
- **25.11.2026:** `kuyumcudefteri` (barkodlu stok + şifresiz fiyat kaynağı) bu dala birleştirildi; Android Fiyatlar
  ekranı `IGoldPriceSource`a uyarlandı. **Release APK** (R8 + kırpma) emülatörde denendi: örnek veriler, canlı fiyatlar
  (elazigaltinfiyatlari.com) ve fiyat görseli paylaşma çalışıyor. Release AAB derleniyor (0 uyarı). 72 birim testi geçiyor.
- **Fiyat görseli arka planı (25.11.2026, kullanıcı isteği):** Fiyatlar ekranında "Resim seç / Değiştir / Kaldır".
  Seçilen resim `Services/PriceBackgroundStore.cs` ile `AppDataDirectory/fiyat_arka_plan.jpg` olarak saklanır (yedek
  değişse de kalır). Öncelik: telefonda seçilen > bilgisayardaki şablon (`AppSettings.GoldPriceTemplateImage`) > sade
  varsayılan. Her paylaşımda güncel fiyatlar bu resmin üzerine yeniden yazılır; ekranda "Paylaşılacak görsel" önizlemesi.
  Core `GoldPriceExportService.LoadTemplate/NormalizeTemplate`: telefon fotoğrafının EXIF yönü uygulanır, genişlik
  1080 px yapılır (yazı boyutu ve konumlar 1080 px şablona göre; 1080 px Çarşı şablonunun çıktısı değişmez). Birim
  testli (`GoldPriceTemplateTests`). Emülatörde Çarşı şablonu seçilip paylaşıldı, kapatıp açınca seçim korundu.
- **Fiyat paylaşımı (26.11.2026, kullanıcı isteği):** "Fiyat listesini paylaş" Özet sayfasının en üstünde de var
  (`Services/PriceShareService.cs`, Fiyatlar sayfasıyla ortak). Fiyatlar ekranında **Yazı konumu** (Yukarı/Aşağı, %3
  adım, %5–80, `Preferences` "fiyat_yazi_konumu"): ilk satırın üstten oranı, Core `CreateGoldPriceImageAsync(textTop)`.
  `FirstRowY` listeyi resmin dışına taşırmaz; varsayılan %25 olduğundan masaüstü çıktısı değişmedi (testli).
- **Play kapalı test 102 (2.1.0), 26.11.2026:** yukarıdaki iki özellik + Google girişi hata mesajı. Etiketsiz
  `release.yml` workflow_dispatch (version=2.1.0, çalıştırma #2 → sürüm kodu 102; GitHub Release/Store yok). 47 MB AAB'yi
  kullanıcı elle yükledi; gizlilik URL değişikliğiyle birlikte **Google incelemesinde**.
- **Drive bağlantısı (25.11.2026):** Cloud projesi **KuyumcuApp** (`kuyumcuapp-454423`, masaüstü istemcileriyle aynı;
  izin ekranı zaten "Üretim"de). Android istemcisi "Kuyumcu Defteri Android (gelistirme)": paket
  `com.kuyumcudefteri.app` + bu bilgisayarın debug SHA-1'i `1F:56:18:8F:5B:43:0A:7F:ED:DE:8E:0D:B0:43:86:C6:1C:D5:BF:03`.
  Play imzalama SHA-1'i için ayrı istemci **henüz yok** (ilk AAB yüklenince açılacak, YAYIN.md 2.2).
  Emülatörde masaüstünün yedek aldığı hesapla bağlanıldı: `KuyumcuDefteri_son_yedek.db3` indi (872 müşteri / 14.309
  işlem), "Şimdi yenile" → "Yedek güncel" (MD5 aynı, tekrar indirmedi).
  Bu sırada düzeltilenler (`GoogleDriveAuthorizer`, `BackupSyncService`):
  - "Bağlan" düğmesi `AuthorizationRequest.Prompt.SelectAccount` ile **her zaman hesap seçici** açar (yanlış hesap
    seçilince düzeltilemiyordu: Play Hizmetleri hesabı hatırlıyordu).
  - "Bağlantıyı kes" artık REST revoke yerine `AuthorizationClient.RevokeAccess` + `ClearToken` kullanır.
  - Drive 401 dönerse önbellekteki belirteç `ClearToken` ile silinip sessizce bir kez daha denenir; olmazsa
    "Yeniden bağlanın" (Yedek sayfasında kırmızı durum + "Bağlan" düğmesi; önceden yanlışlıkla "Bağlı" görünüyordu).
- **Google Play dahili test (25.11.2026):** Play Console "Fatih Yılmaz Apps" (kişisel hesap) → **Kuyumcu Defteri**
  (`com.kuyumcudefteri.app`, Türkçe, ücretsiz). Dahili test kanalında sürüm **100 (2.0.0)** yayında; test listesi
  "Kuyumcu Defteri - dahili test" (fyilmaz2124, adamakillianlat). Katılım bağlantısı:
  https://play.google.com/apps/internaltest/4701420845769712668
  - Yükleme anahtarı: `%USERPROFILE%\.kuyumcu-imza\upload.keystore` (alias `upload`, şifre aynı klasördeki
    `BENIOKU-sifre.txt`'de; **yedeklenmeli**). Yükleme SHA-1 `0C:88:9A:88:D0:2B:BE:5A:3F:1D:DD:F8:45:18:66:A8:6C:22:F8:9B`.
  - Play uygulama imzalama (klasik) SHA-1 `2E:3A:A6:C7:1F:DA:B7:FB:AF:2E:9F:2C:C6:88:AC:9D:30:C0:AA:48` → Cloud'da
    "Kuyumcu Defteri Android (Google Play)" istemcisi açıldı (Play'den kurulan sürüm Drive'a bağlanabilsin).
  - **26.11.2026:** Test kullanıcıları hesap seçtikten sonra bağlanamıyordu. Play'de imzalama anahtarı "Kuantum uyumlu
    (Beta)" anahtara yükseltilmiş; 3 SHA-1 var: klasik `2E:3A…AA:48`, kuantum sonrası
    `CD:2E:B2:5F:9B:5E:9A:66:1B:1E:B1:91:D8:62:CA:F2:84:BD:98:0E`, önceki anahtar
    `98:70:15:84:0D:E2:C9:FF:E3:07:04:A0:0F:09:39:5C:18:DC:5D:A5`. Son ikisi için Cloud'da "(Google Play - kuantum
    sonrasi)" ve "(Google Play - onceki anahtar)" istemcileri açıldı. Ayrıca `GoogleDriveAuthorizer.HandleActivityResult`
    `RESULT_CANCELED` gelen hatayı (DEVELOPER_ERROR 10) "iptal" sayıp yutuyordu; artık intent'teki durum okunuyor ve
    hata mesajı gösteriliyor (bu düzeltme bir sonraki AAB'de çıkacak).
  - AAB bu bilgisayarda derlendi (sürüm kodu 100). CI `100 + çalıştırma no` kullanır, yani sonraki CI sürümleri büyük
    olur. GitHub Secrets (`ANDROID_*`) henüz girilmedi.
  - 47 MB AAB, Chrome eklentisinin 10 MB dosya sınırını aştığı için Play'e kullanıcı elle yükledi.
- **Google Play kapalı test (25.11.2026):** Kapalı test kanalı Türkiye için, mevcut test listesiyle, sürüm
  **101 (2.0.1)** (Drive hesap seçimi düzeltmesi + fiyat görselinde 24 Ayar alış) ile başlatıldı. Mağaza girişi ve
  tüm beyanlar (gizlilik politikası URL'si, veri güvenliği, içerik derecelendirmesi, hedef kitle, uygulama erişimi)
  dahil 14 değişiklik **Google incelemesinde**; onay birkaç gün sürebilir. 14 günlük süre test kullanıcıları
  katıldıktan sonra işler (12+ kişi gerekli).
- **Denenmedi:** Play'den kurulumla gerçek cihazda Drive, yan çekilmiş (EXIF yönlü) gerçek telefon fotoğrafıyla arka plan.

### 10. Fiş metinleri ayarlarda (25.11.2026)
- Müşteri fişindeki sabit "KUYUMCU MEHMET" ve "@kuyumcumehmetelazig" kaldırıldı. `AppSettings.ReceiptTitle` /
  `ReceiptFooter` (yeni kolonlar, eski satırlarda NULL) → Ayarlar > İş Yeri'nde "Fiş başlığı" ve "Fiş alt yazısı".
- Kural `Core/Models/ReceiptTexts.cs`: başlık boşsa iş yeri adı; alt yazı boşsa basılmaz; alt yazıda boş satırlar
  atılır, en fazla 3 satır. Birim testli (`ReceiptTextsTests`).
- `IReceiptPrinter.PrintCustomerReceipt(customer, groups, texts)`: metinleri çağıran (`CustomerDetail`) ayarlardan verir.
  `WindowsReceiptPrinter` başlığı ve alt yazıyı ölçüp ortalar; uzun başlık alt satıra kayar.
  Bakiye satırları ("Borcum"/"Alacak"/"Hesap Sıfır") ve bilgilendirme notu **değişmedi**.
- Fiş çizimi, yazıcıya göndermeden görüntüye alınarak denendi (kısa/uzun başlık, 3 satırlık ve boş alt yazı, Türkçe
  harfler). Ayarlar alanları önizlemede kaydedilip geri yüklendi. **Gerçek termal yazıcıda basılmadı.**

### 11. Microsoft Store (MSIX) hazırlığı (25.11.2026)
- **Yerelde MSIX denendi:** `build/pack-msix.ps1` artık Windows SDK kurulu değilse araçları NuGet önbelleğindeki
  `microsoft.windows.sdk.buildtools`'tan bulur; `-LayoutDir` ile paket içeriği bırakılır ve Geliştirici Modu açıkken
  `Add-AppxPackage -Register <LayoutDir>\AppxManifest.xml` ile imzasız kurulup denenebilir (bkz. YAYIN.md bölüm 1).
  Düzeltilen: Windows PowerShell 5.1 bildirimi ANSI okuyup Türkçe harfleri bozuyordu (`-Encoding UTF8`, betik BOM'lu).
  Deneme kimliğiyle (`KuyumcuDefteri.YerelDeneme`) paket 99,5 MB; kuruldu, açıldı, ana sayfa ve menü çalıştı.
  İlk açılışta pencere bir süre boş kaldı, sonraki açılışlarda tekrarlanmadı.
- **Hata günlüğü:** `Desktop/Platform/AppLog.cs` → `%LOCALAPPDATA%\KuyumcuDefteri\logs\uygulama.log` (1 MB'ta döner).
  Açılış (sürüm, paketli mi), WebView2 sürümü, sayfa yüklendi/yüklenemedi, WebView2 süreç çökmesi, sayfadaki JS
  hataları ve yakalanmayan .NET hataları. Müşteri verisi yazılmaz.
- **MSIX sanallaştırması (önemli):** Store'dan kurulan uygulamanın AppData'ya ve HKCU'ya yaptığı **yeni** yazımlar pakete
  özel yere gider ve **uygulama kaldırılınca silinir** (Belgeler yönlendirilmez). Yani yeni müşteride kaldırma →
  yerel veritabanı, Drive oturumu ve deneme kaydı silinir. Kapatma ayarı (`unvirtualizedResources`) Windows 10'da
  sadece Microsoft iş ortağı oyunlarına; yenisi Windows 11'e özel ve Store onayı belirsiz → kullanılmadı.
  Kullanıcı kararıyla alınan önlemler:
  - `DpapiSecureStore` (deneme başlangıcı, son kontrol, cihaz kimliği) üçüncü kopyayı **Belgeler\Kuyumcu Defteri
    Yedekleri\.kurulum.dat** (gizli, DPAPI şifreli) dosyasına da yazar; okuma sırası dosya → kayıt defteri → Belgeler.
    Böylece kaldır/yeniden kur ile deneme süresi sıfırlanmaz.
  - Kurulum ekranı: eski sürüm verisi yoksa Belgeler'deki yerel yedeklerden içinde müşteri olan en yenisini bulur →
    **"Önceki yedeğiniz bulundu"** (iş yeri adı, müşteri sayısı, tarih) → tek tıkla geri yükleme. Drive tanımlıysa
    **"Drive'dan Geri Yükle"**: Google'a bağlanır, `kuyumcuKind=latest` dosyasını indirir, özeti onaylatıp geri yükler
    (`GoogleDriveService.DownloadLatestBackupAsync`). Drive'dan geri yükleme gerçek hesapla **denenmedi**.
  - Kurulumu tamamlanmamış (boş) veritabanı artık ne yerelde ne Drive'da yedeklenir: yeni kurulumun boş yedeği
    "en yeni yedek" olmasın, Drive'daki "son yedek" (telefonun okuduğu) boşla ezilmesin.
  - Yerel yedek temizliği en yeni 10 yedeği yaşına bakmadan saklar (önceden: uzun süre sonra açılan/yeniden kurulan
    uygulama ilk yedekte 30 günden eski bütün yedekleri siliyordu).
  - Birim testleri: `BackupServiceTests` (boş DB yedeklenmez, özet okunur, en az 10 yedek kalır). Veritabanı testleri
    `[Collection("Veritabanı")]` ile sırayla koşar (`SQLiteAsyncConnection.ResetPool()` paralel testleri bozuyordu).
- **Önizleme:** `dotnet run --project src/Kuyumcu.Preview -- --bos true --urls http://localhost:5081` örnek veri
  yüklemeden `.preview-data-bos` klasöründe yeni kurulum gibi açılır (kurulum ekranını denemek için; launch.json
  "preview-bos"). Bu yolla "Önceki yedeğiniz bulundu → Yedeği Geri Yükle" denendi: 12 müşteri ve bakiyeler geldi.
- **Not (bu geliştirme bilgisayarı):** Claude masaüstü uygulamasından başlatılan süreçler Claude'un paket
  sanallaştırmasına girer; geliştirme sırasında çalıştırılan uygulamanın veri klasörü aslında
  `%LOCALAPPDATA%\Packages\Claude_…\LocalCache\Local\KuyumcuDefteri`. Gerçek `%LOCALAPPDATA%\KuyumcuDefteri` başka veri içerir.

### 12. Eski mobil kodunun temizliği (25.11.2026)
- `Kuyumcu.UI` artık sadece masaüstü ve önizlemede çalışıyor (yeni Android uygulaması UI kütüphanesini kullanmıyor).
  Kaldırılanlar: `Pages/ImportData.razor` (`/import-data`), `Shared/ImportedStockView.razor`,
  `Core/Services/DatabaseImportService.cs` (DI'a kayıtlı bile değildi), `IAppEnvironment.IsReadOnlyClient` ve UI'daki
  bütün dalları (Routes, MainLayout'taki mobil üst bar, stok sayfalarındaki "salt okunur" koşulları). Masaüstünde
  davranış değişmedi: koşullar zaten hep "yazılabilir" yönündeydi. `IsMobile` duruyor (Android'de görsel üretimi kullanıyor).
- Önizlemede bütün menü sayfaları hatasız açıldı; stok düğmeleri (Ürün Girişi, Yeni sayım, Kasa giriş/çıkış) yerinde.

### 13. Kod karıştırma (Obfuscar, 25.11.2026)
- `build/obfuscate.ps1`: yayın çıktısındaki `Kuyumcu.Core.dll`'yi Obfuscar 2.2.50 (yerel dotnet aracı,
  `dotnet-tools.json`) ile karıştırır. CI'da `dotnet publish`'ten sonra, MSIX'ten önce çalışır (`release.yml`).
  Eşleme dosyası CI'da artifact olarak yüklenmez (depo herkese açık).
- Ayarlar ihtiyatlı: public API ve **özellik/olay adları korunur** (SQLite tabloları, `cloud-backup.json`, Google API
  modelleri ada göre çalışır); private/internal tip, metot ve alan adları değişir; metinler gizlenir (`HideStrings`).
- Lisans sınıfları (`Licensing/*`: `LicenseCodec`, `LicensePublicKey`, `LicenseSigningKey`, `LicensePayload`,
  `LicenseCheckResult`, `LicenseKeyError`) **internal** yapıldı → karıştırmada adları da değişiyor. Erişim:
  `InternalsVisibleTo` ile lisans aracı (`kuyumcu-lisans`), testler ve `Kuyumcu.UI` (yerel anahtar üretme sayfası için).
- Yerel anahtar üretme sayfası (`LicenseKeyGenerator.razor`, gitignore'da) **Release derlemesine girmez**
  (`Kuyumcu.UI.csproj`'da koşullu `Content/RazorComponent Remove`; `/generate-license` Release DLL'de yok).
- Doğrulama (karıştırılmış Release, paketsiz): `CheckStoredKey`, `EffectiveToday`, `LicenseCodec`, `LicensePublicKey` adları
  ve açık anahtar metni DLL'de bulunamıyor. Bütün menü sayfaları hatasız açıldı; kayıtlı lisans doğrulandı
  ("Lisanslı"), geçersiz anahtar doğru mesajla reddedildi, Drive durum dosyası okundu, "Şimdi Yedekle" Drive'a yükledi.
  Hata günlüğünde hata yok. Lisans aracı ve 86 test çalışıyor.
- **Sınır:** Obfuscar kontrol akışını karıştırmaz ve `LicenseService`'in public metotları (UI çağırdığı için) adını korur;
  .NET kodu dnSpy gibi araçlarla hâlâ açılıp yamanabilir. Amaç caydırmak. Daha güçlüsü (çevrimiçi aktivasyon, birden çok
  yerde dağıtık kontrol) istenirse ayrıca konuşulmalı.

### 14. Android: vitrin stoğu görünümü (25.11.2026)
- Yedekteki stok tabloları telefonda **salt okunur** gösterilir. Çekirdek: `Stock/StockSnapshot.cs` (silinmemiş
  ürünler, kasa ürünleri, kategori/konum/ayar adları, ürün hareketleri; vitrin toplamı masaüstüyle aynı
  `StockCalculator` ile). `BackupSnapshot.Stock` olarak yüklenir; stok tablosu olmayan eski yedekte boştur.
- Ekranlar: `Pages/StockList.razor` (`/stok`): ayar bazında adet/gram/has tablosu (satıra dokununca o ayar süzülür),
  arama (kod, kategori, model, tedarikçi, konum), Vitrinde / Satılan / Kayıp (varsa) / Kasa sekmeleri.
  **Sadece rakamla arama (≥3 hane) ürün kodu aramasıdır ve durumu ne olursa olsun bütün ürünlerde yapılır** (etiketteki
  koda bakıp "satıldı mı?" sorusu için). `Pages/StockItemDetail.razor` (`/stok/{id}`): bilgiler + hareket geçmişi;
  satışta müşteri adı müşteri sayfasına bağlanır.
- Alt menüye "Stok" sekmesi ve Özet'e "Vitrin: N ürün · gram · has" kartı **sadece gösterilecek stok varsa** çıkar
  (`StockSnapshot.HasAny`: en az bir etiketli ürün ya da miktarı 0 olmayan kasa ürünü; kurulumda eklenen varsayılan
  kasa ürünleri sayılmaz).
- Hareket ve durum adları ortak: `Stock/StockText.cs` (masaüstü `StockItemDetail` da bunu kullanıyor).
- "Örnek verilerle dene" (`DemoBackup`) artık 36 etiketli ürün (6'sı satılmış) ve kasa miktarları içerir.
- Testler: `BackupSnapshotStockTests` (masaüstü toplamlarıyla aynı sonuç, silinen ürün görünmez, kullanılmamış stok
  gösterilmez, stok tablosuz eski yedek okunur, örnek veriler). 90 test geçiyor.
- Emülatörde örnek verilerle denendi: Özet kartı, 5 sekmeli alt menü, ayar süzme, kod ve metin araması, ürün detayı,
  Satılan ve Kasa sekmeleri. **Gerçek dükkân yedeğiyle denenmedi.**

### 15. ZPL (Zebra) dili ve çizgi barkod (26.11.2026)
- `Labels/ZplLanguage.cs`: aynı `LabelDocument`'ı ZPL II'ye çevirir. Ayarlar > Etiket Yazıcısı > Komut dili: "ZPL (Zebra)".
  - Başlık: `~SD` koyuluk (TSPL 0–15 → ZPL 0–30, ×2), `^CI28` (UTF-8, BOM'suz), `^PW`/`^LL` (nokta), `^LH0,0`, `^MNY`
    (boşluklu etiket; ayarlardaki gap ZPL'de kullanılmaz, yazıcı sensörü algılar), `^PR` hız, `^PON`/`^POI`
    (DIRECTION 1 = normal, 0 = ters), sonda `^PQ` kopya.
  - Yazı: ölçeklenebilir font 0, TSPL font hücresi boyunda (`^A0N,{yükseklik},{genişlik}`). Bütün alanlar `^FH_` ile;
    `^`, `~`, `_` karakterleri `_5E/_7E/_5F` olarak gider (yazı komutu bozamaz).
  - DataMatrix `^BXN,{modül},200`, QR `^BQN,2,{modül}` + `LA,` (TSPL'deki L/A gibi), çerçeve/çizgi `^GB`.
  - Önizlemedeki sahte yazıcı ZPL işini `.zpl` uzantısıyla yazar.
  - Ayarlarda yazıcı adı ilk kez seçilirken "Zebra"/"ZDesigner" geçiyorsa dil kendiliğinden ZPL olur.
- **Çizgi barkod (Code 128)**: Kod tipi'ne üçüncü seçenek (`LabelCodeType.Code128`, `LabelCode128` öğesi). Sadece 1D
  (lazer) okuyucu kullanan dükkânlar için; 2D okuyucuda DataMatrix daha küçük ve sağlam. Kod içeriği aynı 12 hane
  (ürün kodu kuralları değişmedi); okuyucu aynı rakamları gönderir. 12 rakam C alt kümesiyle 101 modül.
  - TSPL: `BARCODE x,y,"128",yükseklik,0,0,ince,ince,"kod"` (alt küme otomatik; insan okunur satır kapalı).
    ZPL: `^BY{ince}^BCN,{yükseklik},N,N,N` + `>;` (C alt kümesi). Rakamlar her iki dilde ayrı yazı olarak basılır.
  - Yeni kolonlar `BarcodeNarrowDots` (en ince çizgi, 1–4 nokta, varsayılan 1) ve `BarcodeHeightMm` (varsayılan 4,5).
    Eski satırlarda 0 gelir → `EffectiveBarcode*` varsayılanı kullanır.
  - `LabelPrinterSettings.ApplyCodeLayoutDefaults(tip)`: 2D ↔ barkod geçişinde 1. kanattaki kod/rakam konumları o tipin
    varsayılanına gelir (barkod: X 33,7 / Y 1 mm, rakamlar altında tek satır X 34 / Y 6,2 mm font 1). DataMatrix ↔ QR
    geçişinde yerleşim değişmez. "Varsayılan yerleşim" seçili kod tipini korur.
  - 72 × 10 mm etikette 1 noktalık barkod ≈ 12,6 mm (sessiz bölgelerle kanada ancak sığar); 2 nokta ≈ 25 mm, sığmaz.
    Ayarlarda bu uyarı gösteriliyor.
- Testler: `ZplAndBarcodeTests` (ZPL ürün/QR/hizalama çıktısı, kaçış, UTF-8, barkod modül sayısı ve kanada sığma,
  TSPL/ZPL barkod komutu, eski satır varsayılanları). 103 test geçiyor.
- Önizlemede denendi: kod tipi barkoda geçince konumlar ve önizleme güncellendi, ZPL seçilip test etiketi `.zpl`
  dosyasına yazıldı. **Gerçek Zebra yazıcıda ve TSC'de barkodla basılmadı; 1D okuyucuyla okunması denenmedi.**

### 16. Kasa satışını veresiyeye bağlama (26.11.2026)
Kullanıcı kararı: borç cinsi her satışta seçilsin. "Kim sattı" alanı yapılmayacak (tek kullanıcı).
- `Stock/BulkSale.cs` (Core, saf, birim testli):
  - `OwnCurrency`: ürünün defterdeki kendi cinsi. Adetliler **ada göre** (kelime olarak "çeyrek" → Çeyrek Altın,
    "yarım" → Yarım, "tam" → Tam); gramla tutulanlar ayarına göre (24/22/14 → o ayarın gramı). Ata Lira ve
    tanınmayan adların kendi cinsi yok → TL ya da 22 ayar seçilir.
  - `CreditCurrencies`: seçenekler, kendi cinsi (varsa) önce, sonra TL ve 22 Ayar (gr).
  - `SuggestCredit`: kendi cinsi = satılan adet/gram; 22 ayar = ağırlık × milyem / 22 ayar milyemi (işçiliksiz);
    TL = `SuggestPrice`: fiyat listesinde ürünün satırı varsa (Çeyrek, Yarım, "Tam (Ata)" — Tam ve Ata Lira için,
    "Gram Altın" — 24 ayar gram için) miktar × satış, yoksa has × 24 ayar satış; 10 TL'ye yuvarlanır. Hepsi
    değiştirilebilir öneri; deftere 2 ondalıkla yazılır.
  - `Description`: defter açıklaması "Kasa satışı: 2 adet Çeyrek[ - not]".
- `StockService.AddBulkMovementAsync(..., customerId, transactionId, price)`: sadece Çıkış'ta; hareketle aynı satırda saklanır.
- `UI/Services/LedgerConfirm.cs`: deftere stok ekranından yazarken **mükerrer kontrolü + onay** tek yerde; etiketli ürün
  satış penceresi de artık bunu kullanıyor (davranış aynı; müşteri detayındaki işlem penceresine dokunulmadı).
- `BulkMovementDialog`: Çıkış seçiliyken "Müşteri hesabına yaz (veresiye)" anahtarı; müşteri, borç cinsi, borç (öneri).
  Sıra: miktar kasada var mı → mükerrer/onay → defter işlemi → kasa hareketi (başarısızsa defter işlemi silinir).
  Hareket açıklaması "Veresiye satış: {müşteri}[ - not]"; TL'de fiyat da harekete yazılır.
- Testler: `BulkSaleTests` (7) + `StockServiceTests` kasa veresiye testi. 111 test geçiyor.
- Önizlemede denendi: 2 Çeyrek → TL (canlı fiyattan 20.840 öneri) → onay → kasa 24 → 22, hareket geçmişinde müşteri adı,
  müşterinin defterinde 20.840 TL Giden. **Kendi cinsiyle (adet) ve 22 ayar seçenekleri önizlemede kaydedilmedi** (birim testli).
- **İade (aynı gün eklendi):** `StockMovementType.BulkReturn = 13` ("İade"), `StockMovement.RelatedMovementId` (yeni
  kolon; iade → satış hareketi). `StockService.ReturnBulkSaleAsync(satışHareketiId, not)`: sadece müşteriye bağlı
  `BulkOut`, daha önce iade alınmamışsa; satılan miktar geri girer, müşteri/işlem/fiyat iade hareketine kopyalanır.
  Defter işlemini silmek UI'da (`BulkStock.razor`, onay penceresindeki kutu). Geçmişte iade edilen satışın yanında
  "iade alındı" yazar. Testler: 2 yeni servis testi (113 test). Önizlemede: 2 Çeyrek iade → kasa 22 → 24, defterdeki
  20.840 TL işlem silindi.

## Kullanıcıya sunulan öneriler (karar bekleyen sıra)
1. ~~Lisans~~ (yapıldı, bölüm 5), ~~Obfuscar ile kod karıştırma~~ (bölüm 13). Kalan (istenirse): çevrimiçi aktivasyon,
   dağıtık lisans kontrolü.
2. ~~Otomatik Drive yedeği~~ (yapıldı, bölüm 6). Kalan: ürünün OAuth istemcisi oluşturulup izin ekranı "Üretim"e
   alınmalı ("Test"te yenileme jetonları 7 günde düşer → yedek durur ve "Yeniden bağlanın" çıkar).
3. Android: ~~Drive'dan son yedeği otomatik okuma~~ (yapıldı, bölüm 9). Play kaydı ve dahili test yapıldı; kalan: kapalı test (12 kişi / 14 gün; aynı Play hesabı — kullanıcı
   daha önce başka uygulamada geçti, yeni uygulama için yine gerekir). Adımlar: `docs/YAYIN.md` bölüm 2.

## Doğrulanmadı / dikkat
- `build/pack-msix.ps1` yerelde denendi (bölüm 11); CI'da ve gerçek Store kimliğiyle henüz denenmedi (ilk etikette görülecek).
- Store sürümünde uygulama kaldırılırsa yerel veritabanı silinir (bölüm 11); veri Belgeler'deki yedeklerden ve Drive'dan
  kurulum ekranında geri yüklenir.
- Hedef çerçeve değişti (`net10.0-windows10.0.19041.0`): CI'da Linux üzerinde Desktop derlemesi geçti (26.11.2026).
- **CI birim testleri 25.11.2026'dan beri Linux'ta düşüyordu** (derlemeler geçiyordu). Nedenleri (26.11 düzeltildi):
  test projesinde SkiaSharp'ın Linux yerel kütüphanesi yoktu (`SkiaSharp.NativeAssets.Linux.NoDependencies` eklendi;
  fiyat görseli testleri) ve yedek temizliği dosya oluşturma zamanına bakıyordu (artık dosya adındaki tarih).
  CI günlükleri oturum açmadan okunamadığı için neden çıkarımla bulundu; düzeltmeden sonra CI #17 (c6ddb5c) yeşil.
- Özel lisans anahtarının yedeği kullanıcıda olmalı (kaybolursa yeni lisans üretilemez).
- Süreli lisansta bilgisayar saati bir kez ileri (ör. 2030) ayarlanırsa "son kontrol" o tarihte kalır ve lisans bitmiş
  sayılabilir; müşteriye yeni anahtar vermek gerekir (bilinçli ödünleşim: saat geri alınarak uzatmayı engeller).
- Yerel yedek `Environment.SpecialFolder.MyDocuments` altına gider; Belgeler OneDrive'a yönlendirilmişse
  `OneDrive\Belgeler\Kuyumcu Defteri Yedekleri` olur. Otomatik yedek **günde bir** alınır (26.11.2026): açılıştan 1 dk
  sonra ve açık kaldıkça 24 saatte bir denenir, o güne ait yedek (otomatik ya da elle) varsa atlanır
  (`BackupService.BackupIfDueAsync`). "Şimdi Yedekle" ve geri yükleme öncesi güvenlik yedeği her zaman alınır.
  Ödünleşim: sabah yedek alındıktan sonra gün içinde girilen kayıtlar yerel yedeğe ertesi gün girer (eskiden gün içinde
  yeniden açılışta da yedek alınıyordu); Drive yedeği bundan etkilenmez (2 saatte bir ve kapanışta, değişiklik varsa yüklenir). 30 gün saklama.
- Yeni işlemde varsayılan cins artık **son kullanılan cins** (eskiden TL). Kullanıcı istemezse geri alınacak.
- Tutar girişinde Türkçe biçim: "12.5" → 125 okunur (nokta binlik). Eski sürümde de böyleydi.
  (Gram girişinde ise nokta ondalıktır: "12.5" → 12,5 gr; gramda binlik ayıracı kullanılmaz.)

**Barkod/stok — doğrulanmadı**
- **İlk baskılar yapıldı (25.11.2026, TE210, USB, winspool RAW):** yazıcı etiketi **kuyruk solda** besliyor
  (X = 0 kuyruğun ucu). Cetvelle ölçülen: kuyruk 0–30 mm, kanatlar 30–70 mm (her biri 20 mm, katlama 50 mm).
  Varsayılan yerleşim buna göre: kod X 31,5 / rakamlar X 38 / gram-ayar X 51,5 mm. Türkçe harfler (CODEPAGE 1254)
  doğru çıkıyor. Bu yerleşimle basılan test etiketi kanatlara doğru oturdu (kullanıcı onayı). TCP 9100 gönderimi
  henüz denenmedi.
- DataMatrix 3 nokta/kare (≈ 5,3 mm) RH10 ile okunabilirliği denenmedi; okunmazsa kare boyu 4 yapılır.
- Sunlux RH10 gerçek cihazla denenmedi. Önizlemede tuş basışlarıyla (rakam + Enter) okutma, odak kaçırma yönlendirmesi,
  çift okutma ve hatalı kod uyarıları denendi.
- Eski Android `ImportData` → Vitrin Stoku sekmesi denenmedi; eski `Kuyumcu.Mobile` projesi bölüm 9 ile kaldırıldı.
  Yeni uygulamadaki stok görünümü bölüm 14'te.
- **ZPL (bölüm 15) gerçek yazıcıda denenmedi.** Bakılacaklar: font 0'ın Türkçe harfleri (bozuksa "sadeleştir"),
  QR'ın `^FO`'ya göre birkaç nokta aşağıda başlaması (Zebra'da bilinen davranış; Y kaydırmayla düzeltilir),
  `~SD` koyuluğunun yazıcı ayarını değiştirmesi (yazıcı kapanınca eski değere döner), `^PO` yönünün doğru olup olmadığı.
- Code 128 1 noktalık çizgiyle (203 dpi ≈ 0,125 mm) termal baskıda okunabilirlik sınırda; gerçek okuyucuyla denenmeli.
  TSC'nin `"128"` otomatik alt küme seçiminin rakamlarda C kullandığı varsayıldı (genişlik hesabı buna göre).
- Canlı fiyat sitesi HTML'i değişirse `ElazigAltinFiyatlariSource.Parse` boş döner (test örneği 24.11.2026 sayfasından).

## Sıradaki işler
1. ~~Windows'ta çalıştırıp test et~~ (yapıldı, bkz. bölüm 3). Kullanıcının elle testi sürüyor.
2. ~~Ayarlar, Kurulum ve Lisans ekranları~~ (bölüm 5), ~~otomatik Drive yedeği~~ (bölüm 6).
   ~~Android'in Drive'daki son yedeği okuması~~ (bölüm 9). Sıradaki: Play kapalı testi.
3. ~~Android veri görüntüleme ekranı~~ ve ~~Drive'dan otomatik indirme~~ (bölüm 9), ~~eski ImportData /
   IsReadOnlyClient temizliği~~ (bölüm 12), ~~Android vitrin stoğu görünümü~~ (bölüm 14).
4. ~~Google Cloud: Android OAuth istemcileri (debug + Play imzalama)~~, ~~Play dahili test~~ (bölüm 9).
   ~~Kapalı test hazırlığı ve incelemeye gönderim~~. Sıradaki: inceleme onayı → 12+ test kullanıcısını katılım
   bağlantısıyla davet et → 14 gün → üretim başvurusu. Telefonda Play'den kurup Drive'ı dene.
5. Mağaza hazırlığı: ~~gizlilik politikası~~ (GitHub'daki `docs/GIZLILIK.md`), ~~Play kaydı~~. Kalan: GitHub
   Secrets/Variables (`ANDROID_*`, `GOOGLE_DESKTOP_*`, `STORE_*`), Microsoft Store (Partner Center) kaydı ve ilk MSIX
   (bkz. `docs/YAYIN.md` bölüm 1 ve 3).
6. ~~Eski `ccr-325b955a-m9osww` uzak dalı~~ (26.11.2026 silindi; içeriği `kuyumcudefteri`'deydi).
   Lisans: çevrimiçi aktivasyon / dağıtık kontrol şimdilik yapılmayacak (kullanıcı kararı, 26.11.2026).
7. Barkod: TE210 ve RH10 ile `docs/ETIKET_VE_OKUYUCU_TESTI.md` adımları; çıkan kanat ölçüleri varsayılan yerleşime
   işlenebilir. ~~ZPL dili~~ ve ~~çizgi barkod~~ (bölüm 15; gerçek Zebra ile denenmeli), ~~kasa satışını veresiyeye
   bağlama~~ ve iadesi (bölüm 16). "Kim sattı" alanı yapılmayacak (tek kullanıcı).
8. ~~Fiş yazıcısındaki dükkâna özel metinler~~ (bölüm 10). İlk müşteri geçişte bunları Ayarlar'a girmeli (YAYIN.md 4).

## Geliştirme komutları

```powershell
dotnet run --project src/Kuyumcu.Desktop     # Windows uygulaması
dotnet run --project src/Kuyumcu.Preview     # tarayıcı önizlemesi → http://localhost:5080
dotnet build src/Kuyumcu.Android -f net10.0-android          # 'maui-android' iş yükü + Android API 36 gerekir
dotnet build src/Kuyumcu.Android -f net10.0-android -t:Run   # açık emülatöre/cihaza kurup çalıştırır
# Bu bilgisayarda: -p:AndroidSdkDirectory="C:\Program Files (x86)\Android\android-sdk", emülatör AVD: pixel_7_-_api_34
dotnet test tests/Kuyumcu.Core.Tests          # birim testleri (CI'da da çalışır)
```

Önizlemede etiket yazıcısı yerine `src/Kuyumcu.Preview/.preview-data/labels/*.tspl` dosyaları oluşur; örnek stok
verisi ilk açılışta eklenir (`.preview-data` silinirse yeniden oluşur).

Dil: kullanıcı ile Türkçe konuşulur; kod yorumları ve arayüz metinleri Türkçe.
