# Devir Notu — Kuyumcu Defteri

Bu belge, projeyi başka bir oturumda / geliştiricide kaldığı yerden sürdürmek için gereken bağlamı içerir.
Son güncelleme: dal `kuyumcudefteri`.

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
src/Kuyumcu.Desktop   WPF kabuğu (net10.0-windows, self-contained, win-x64) + Packaging/AppxManifest.xml
src/Kuyumcu.Mobile    MAUI Android kabuğu (net10.0-android), salt okunur
src/Kuyumcu.Preview   Blazor Server tarayıcı önizlemesi (Mac/Linux'ta arayüz geliştirmek için, örnek verili) — dağıtılmaz
build/                CI betikleri: write-secrets.ps1 (gizli dosyaları üretir), pack-msix.ps1 (makeappx ile MSIX)
docs/YAYIN.md         Partner Center, Play Console, GitHub Secrets kurulumu
```

- Platforma özgü her şey `src/Kuyumcu.Core/Platform/IPlatformServices.cs` arayüzlerinin arkasında:
  `IAppEnvironment`, `ISecureStore` (Windows: DPAPI), `IFilePickerService`, `IClipboardService`, `IShareService`,
  `IReceiptPrinter`, `ILegacyDataLocator`. Her kabuk projesi kendi uygulamasını kaydeder.
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
- `LICENSE_SECRET_KEY` değişirse eski lisans anahtarları geçersiz olur.

## Doğrulanmadı / dikkat
- **WPF uygulaması Windows'ta hiç çalıştırılmadı** (geliştirme Linux bulut ortamında yapıldı; derleniyor).
  İlk iş: Windows'ta `dotnet run --project src/Kuyumcu.Desktop` ve eski veri aktarma testi.
- `build/pack-msix.ps1` ve Android AAB üretimi CI'da henüz denenmedi (ilk etikette görülecek).
- `LicenseKeyGenerator.razor` eski yapıya göre yazıldı; derleme hatası verebilir, uyarlanmalı.
- Yeni işlemde varsayılan cins artık **son kullanılan cins** (eskiden TL). Kullanıcı istemezse geri alınacak.
- Tutar girişinde Türkçe biçim: "12.5" → 125 okunur (nokta binlik). Eski sürümde de böyleydi.

## Sıradaki işler
1. Windows'ta çalıştırıp test et; çıkan hataları düzelt. `LicenseKeyGenerator.razor`'ı yeni yapıya uyarla.
2. Ayarlar, Kurulum ve Lisans ekranlarının düzenini yeni tasarıma getir.
3. Android veri görüntüleme ekranını (`ImportData.razor`) yeni tasarıma getir.
4. Android'in Google Drive'dan son yedeği kendisi indirmesi (Android OAuth istemcisi gerekir).
5. Mağaza hazırlığı: ürün adı ve Android paket adı kesinleşmeli (Play'e ilk yüklemeden sonra değişmez),
   gizlilik politikası sayfası, GitHub Secrets/Variables (bkz. `docs/YAYIN.md`).
6. Eski `ccr-325b955a-m9osww` uzak dalı silinecek (bulut oturumu silemedi).

## Geliştirme komutları

```powershell
dotnet run --project src/Kuyumcu.Desktop     # Windows uygulaması
dotnet run --project src/Kuyumcu.Preview     # tarayıcı önizlemesi → http://localhost:5080
dotnet build src/Kuyumcu.Mobile -f net10.0-android   # 'dotnet workload install maui-android' gerekir
```

Dil: kullanıcı ile Türkçe konuşulur; kod yorumları ve arayüz metinleri Türkçe.
