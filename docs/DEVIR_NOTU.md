# Devir Notu — Kuyumcu Defteri

Bu belge, projeyi başka bir oturumda / geliştiricide kaldığı yerden sürdürmek için gereken bağlamı içerir.
Son güncelleme: dal `feature/barkod-stok` = güncel `kuyumcudefteri` (1ff0432) + barkodlu vitrin stoğu (bölüm 7–8).

## Amaç

Tek müşteriye ("Çarşı Kuyumculuk") yazılmış uygulamayı, birden çok kuyumcuya satılabilecek bir **ürüne**
dönüştürmek ve arayüzünü yenilemek.

- **Windows** (tam sürüm): WPF + BlazorWebView, **Microsoft Store**'dan MSIX olarak dağıtılacak (kayıt ücretsiz,
  Store imzalar, lisans uygulama içinde satılır → komisyon yok).
- **Android** (salt okunur): .NET MAUI + BlazorWebView, **Google Play**'e AAB (hedef API 36).
- **.NET 10** (LTS). Teknoloji kararları ve gerekçeleri aşağıda.

## Proje yapısı

```
src/Kuyumcu.Core      Modeller + servisler (SQLite, lisans, yedek, Drive, görsel üretimi). net10.0, platformdan bağımsız
src/Kuyumcu.UI        Ortak Blazor arayüzü (Razor Class Library): sayfalar, tema, bileşenler, app.css, app.js
src/Kuyumcu.Desktop   WPF kabuğu (net10.0-windows10.0.19041.0, self-contained, win-x64) + Packaging/AppxManifest.xml
src/Kuyumcu.Mobile    MAUI Android kabuğu (net10.0-android), salt okunur
src/Kuyumcu.Preview   Blazor Server tarayıcı önizlemesi (Mac/Linux'ta arayüz geliştirmek için, örnek verili) — dağıtılmaz
tests/Kuyumcu.Core.Tests  xUnit birim testleri (kod üretimi, TSPL, toplamlar, fiyat sitesi, stok servisi, yedek/geri yükleme)
build/                CI betikleri: write-secrets.ps1 (gizli dosyaları üretir), pack-msix.ps1 (makeappx ile MSIX)
tools/Kuyumcu.LicenseTool  Lisans anahtarı üretme aracı (geliştirici bilgisayarında; bkz. docs/YAYIN.md "Lisans anahtarları")
docs/YAYIN.md         Partner Center, Play Console, GitHub Secrets kurulumu
docs/ETIKET_VE_OKUYUCU_TESTI.md  Etiket yazıcısı (TSC TE210) ve barkod okuyucu (Sunlux RH10) deneme adımları
```

- Platforma özgü her şey `src/Kuyumcu.Core/Platform/IPlatformServices.cs` arayüzlerinin arkasında:
  `IAppEnvironment`, `ISecureStore` (Windows: DPAPI), `IFilePickerService`, `IClipboardService`, `IShareService`,
  `IReceiptPrinter`, `ILabelPrinter` (etiket yazıcısı, ham komut), `ILegacyDataLocator`. Her kabuk projesi kendi
  uygulamasını kaydeder (Desktop, Mobile, Preview — üçü de).
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
  etiketi) → `ILabelLanguage` (`TsplLanguage`; ZPL eklemek için yeni bir uygulama + `LabelPrinterLanguage` değeri).
- Platform: `ILabelPrinter`. Desktop `WindowsLabelPrinter` (winspool.drv P/Invoke, "RAW"; GDI yok), Mobile
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
- Android: `ImportData` → "Vitrin Stoku" sekmesi (`Shared/ImportedStockView.razor`), salt okunur.

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
- **Cihaza özel:** `LabelPrinterSettings` geri yüklemede bu bilgisayarınki korunur.
- **Android:** stok sadece görüntülenir; etiket basma ve stok değiştirme yok (`IsReadOnlyClient`).
- **Lisans:** modül normal lisansın parçası; ayrı özellik bayrağı yok (imzalı lisans, bölüm 5;
  stok modülü `LicenseService`'e dokunmaz).


## Kullanıcıya sunulan öneriler (karar bekleyen sıra)
1. ~~Lisans~~ (yapıldı, bölüm 5). Kalan: Obfuscar ile kod karıştırma (yamalamayı zorlaştırır), istenirse çevrimiçi aktivasyon.
2. ~~Otomatik Drive yedeği~~ (yapıldı, bölüm 6). Kalan: ürünün OAuth istemcisi oluşturulup izin ekranı "Üretim"e
   alınmalı ("Test"te yenileme jetonları 7 günde düşer → yedek durur ve "Yeniden bağlanın" çıkar).
3. Android: aynı Play hesabı kullanılacak (kullanıcı daha önce 12 kişi/14 gün testten geçti; yeni uygulama için
   büyük olasılıkla tekrar gerekecek — kapalı testi erken başlat). Drive'dan son yedeği otomatik okuma.

## Doğrulanmadı / dikkat
- `build/pack-msix.ps1` ve Android AAB üretimi CI'da henüz denenmedi (ilk etikette görülecek).
- Hedef çerçeve değişti (`net10.0-windows10.0.19041.0`): CI'da Linux üzerinde Desktop derlemesi ilk push'ta kontrol edilmeli.
- Özel lisans anahtarının yedeği kullanıcıda olmalı (kaybolursa yeni lisans üretilemez).
- Süreli lisansta bilgisayar saati bir kez ileri (ör. 2030) ayarlanırsa "son kontrol" o tarihte kalır ve lisans bitmiş
  sayılabilir; müşteriye yeni anahtar vermek gerekir (bilinçli ödünleşim: saat geri alınarak uzatmayı engeller).
- Yerel yedek `Environment.SpecialFolder.MyDocuments` altına gider; Belgeler OneDrive'a yönlendirilmişse
  `OneDrive\Belgeler\Kuyumcu Defteri Yedekleri` olur. Her açılıştan 1 dk sonra yedek alınır (günde birkaç açılışta
  birkaç dosya oluşur; 30 gün saklama). İstenirse "o gün yedek varsa atla" yapılabilir.
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
- Android `ImportData` → Vitrin Stoku sekmesi derlenip denenmedi (bu oturumda maui-android iş yükü yoktu; Core ve UI
  derleniyor). Mobile projesinde sadece `UnsupportedLabelPrinter` + kayıt satırı eklendi.
- Canlı fiyat sitesi HTML'i değişirse `ElazigAltinFiyatlariSource.Parse` boş döner (test örneği 24.11.2026 sayfasından).

## Sıradaki işler
1. ~~Windows'ta çalıştırıp test et~~ (yapıldı, bkz. bölüm 3). Kullanıcının elle testi sürüyor.
2. ~~Ayarlar, Kurulum ve Lisans ekranları~~ (bölüm 5), ~~otomatik Drive yedeği~~ (bölüm 6).
   Sıradaki: Android'in Drive'daki `KuyumcuDefteri_son_yedek.db3`'ü okuması + Play kapalı testi.
3. Android veri görüntüleme ekranını (`ImportData.razor`) yeni tasarıma getir.
4. Android'in Google Drive'dan son yedeği kendisi indirmesi (Android OAuth istemcisi gerekir).
5. Mağaza hazırlığı: ürün adı ve Android paket adı kesinleşmeli (Play'e ilk yüklemeden sonra değişmez),
   gizlilik politikası sayfası, GitHub Secrets/Variables (bkz. `docs/YAYIN.md`).
6. Eski `ccr-325b955a-m9osww` uzak dalı silinecek (bulut oturumu silemedi).
7. Barkod: TE210 ve RH10 ile `docs/ETIKET_VE_OKUYUCU_TESTI.md` adımları; çıkan kanat ölçüleri varsayılan yerleşime
   işlenebilir. Sonra ZPL dili (başka dükkânlar için), kasa ürünlerinin satışta veresiyeye bağlanması, kullanıcı (kim
   sattı) alanının doldurulması düşünülebilir. Fiş yazıcısındaki dükkâna özel metinler ("KUYUMCU MEHMET",
   Instagram adı) hâlâ `WindowsReceiptPrinter`'da gömülü; ayarlara taşınmalı.

## Geliştirme komutları

```powershell
dotnet run --project src/Kuyumcu.Desktop     # Windows uygulaması
dotnet run --project src/Kuyumcu.Preview     # tarayıcı önizlemesi → http://localhost:5080
dotnet build src/Kuyumcu.Mobile -f net10.0-android   # 'dotnet workload install maui-android' gerekir
dotnet test tests/Kuyumcu.Core.Tests          # birim testleri (CI'da da çalışır)
```

Önizlemede etiket yazıcısı yerine `src/Kuyumcu.Preview/.preview-data/labels/*.tspl` dosyaları oluşur; örnek stok
verisi ilk açılışta eklenir (`.preview-data` silinirse yeniden oluşur).

Dil: kullanıcı ile Türkçe konuşulur; kod yorumları ve arayüz metinleri Türkçe.
