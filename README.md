# Kuyumcu Defteri

Kuyumcular için müşteri, emanet ve altın/döviz hesap defteri.

- **Windows** (tam sürüm): WPF + Blazor arayüz, Microsoft Store'dan dağıtılır.
- **Android** (salt okunur): bilgisayarın Google Drive'a aldığı son yedeği kendisi indirip gösterir, altın fiyatı görseli paylaşır. Google Play'den dağıtılır.

## Proje yapısı

```
src/
  Kuyumcu.Core      Modeller ve servisler (veritabanı, lisans, yedekleme, görsel üretimi) — platformdan bağımsız
  Kuyumcu.UI        Ortak Blazor arayüzü (sayfalar, tema, stiller)
  Kuyumcu.Desktop   Windows uygulaması (WPF + BlazorWebView) → MSIX
  Kuyumcu.Android   Android uygulaması (.NET MAUI + BlazorWebView, salt okunur, Drive'dan son yedeği indirir) → AAB
  Kuyumcu.Preview   Tarayıcı önizlemesi (Mac/Linux'ta arayüzü görmek için, örnek verilerle) — dağıtılmaz
build/              CI yardımcı betikleri (gizli dosya üretimi, MSIX paketleme)
tools/              Geliştirici araçları: Kuyumcu.LicenseTool (lisans anahtarı üretir; özel anahtar depoda değil)
branding/           Uygulama simgesi kaynakları (Windows PNG/ICO dosyaları build/simgeleri-uret.ps1 ile üretilir)
docs/YAYIN.md       Mağaza ve GitHub Secrets kurulumu; docs/GIZLILIK.md gizlilik politikası taslağı
musteri-varliklari/ Müşteriye özel dosyalar (ürüne gömülmez)
```

Platforma özgü her şey `Kuyumcu.Core/Platform/IPlatformServices.cs` içindeki arayüzlerin arkasındadır;
Windows ve Android projeleri kendi uygulamalarını sağlar.

## Geliştirme

Gereksinim: .NET 10 SDK. Android için ayrıca `dotnet workload install maui-android`.

```bash
dotnet build src/Kuyumcu.Desktop      # Windows uygulaması (Linux/macOS'ta da derlenir, Windows'ta çalışır)
dotnet run --project src/Kuyumcu.Desktop
dotnet build src/Kuyumcu.Android -f net10.0-android   # Android API 36 platformu da gerekir
```

### Mac'te (veya Linux'ta) arayüzü görmek

Windows uygulaması WPF olduğu için Mac'te çalışmaz; aynı arayüz tarayıcıda örnek verilerle açılabilir:

```bash
dotnet run --project src/Kuyumcu.Preview
# tarayıcıda: http://localhost:5080
```

Önizleme verileri `src/Kuyumcu.Preview/.preview-data` klasöründedir; silinirse örnek veriler yeniden oluşturulur.
Dosya seçme, pano, yazıcı ve paylaşım gibi Windows'a özgü özellikler önizlemede çalışmaz.

Yerel derlemelerde lisans ve Google anahtarları için geliştirici varsayılanları kullanılır
(`*.DevSecrets.cs`). Yayın derlemelerinde CI bunları GitHub Secrets'tan üretir.

## Yayın

`git tag vX.Y.Z && git push origin vX.Y.Z` → bkz. [docs/YAYIN.md](docs/YAYIN.md).
