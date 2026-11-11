# Kuyumcu Defteri

Kuyumcular için müşteri, emanet ve altın/döviz hesap defteri.

- **Windows** (tam sürüm): WPF + Blazor arayüz, Microsoft Store'dan dağıtılır.
- **Android** (salt okunur): bilgisayardan alınan yedeği açıp görüntüler, altın fiyatı görseli paylaşır. Google Play'den dağıtılır.

## Proje yapısı

```
src/
  Kuyumcu.Core      Modeller ve servisler (veritabanı, lisans, yedekleme, görsel üretimi) — platformdan bağımsız
  Kuyumcu.UI        Ortak Blazor arayüzü (sayfalar, tema, stiller)
  Kuyumcu.Desktop   Windows uygulaması (WPF + BlazorWebView) → MSIX
  Kuyumcu.Mobile    Android uygulaması (.NET MAUI + BlazorWebView) → AAB
build/              CI yardımcı betikleri (gizli dosya üretimi, MSIX paketleme)
branding/           Uygulama simgesi kaynakları
docs/YAYIN.md       Mağaza ve GitHub Secrets kurulumu
musteri-varliklari/ Müşteriye özel dosyalar (ürüne gömülmez)
```

Platforma özgü her şey `Kuyumcu.Core/Platform/IPlatformServices.cs` içindeki arayüzlerin arkasındadır;
Windows ve Android projeleri kendi uygulamalarını sağlar.

## Geliştirme

Gereksinim: .NET 10 SDK. Android için ayrıca `dotnet workload install maui-android`.

```bash
dotnet build src/Kuyumcu.Desktop      # Windows uygulaması (Linux/macOS'ta da derlenir, Windows'ta çalışır)
dotnet run --project src/Kuyumcu.Desktop
dotnet build src/Kuyumcu.Mobile -f net10.0-android
```

Yerel derlemelerde lisans ve Google anahtarları için geliştirici varsayılanları kullanılır
(`*.DevSecrets.cs`). Yayın derlemelerinde CI bunları GitHub Secrets'tan üretir.

## Yayın

`git tag vX.Y.Z && git push origin vX.Y.Z` → bkz. [docs/YAYIN.md](docs/YAYIN.md).
