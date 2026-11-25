# Yayın Kurulumu (Microsoft Store + Google Play)

Bu belge, ürünü ilk kez mağazalara çıkarmak için **bir kere** yapılacak işleri anlatır.
Sonrasında her sürüm için sadece bir etiket göndermek yeterlidir:

```bash
git tag v2.0.0
git push origin v2.0.0
```

GitHub Actions (`.github/workflows/release.yml`) iki dosya üretir ve GitHub Release'e ekler:

| Dosya | Nereye yüklenir |
|---|---|
| `KuyumcuDefteri_X.Y.Z_x64.msix` | Partner Center → uygulama → yeni gönderim → Paketler |
| `com.kuyumcudefteri.app-Signed.aab` | Play Console → uygulama → Üretim (veya Test) → yeni sürüm |

---

## 1. Microsoft Store (Windows)

1. [Partner Center](https://partner.microsoft.com/dashboard)'da geliştirici hesabı açın (ücretsiz).
2. **Uygulamalar ve oyunlar → Yeni ürün → MSIX veya PWA uygulaması** → ürün adını ayırın (örn. "Kuyumcu Defteri").
3. Ürün sayfasında **Ürün yönetimi → Ürün kimliği** bölümündeki üç değeri GitHub'a **Variables** olarak girin
   (Settings → Secrets and variables → Actions → Variables):

   | Variable | Partner Center'daki alan | Örnek |
   |---|---|---|
   | `STORE_PACKAGE_NAME` | Package/Identity/Name | `12345FirmaAdi.KuyumcuDefteri` |
   | `STORE_PUBLISHER` | Package/Identity/Publisher | `CN=XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX` |
   | `STORE_PUBLISHER_DISPLAY_NAME` | Package/Properties/PublisherDisplayName | `Firma Adı` |

4. Gönderimde istenenler:
   - **Gizlilik politikası URL'si** (zorunlu: uygulama müşteri verisi tutuyor ve internete bağlanıyor).
   - Ekran görüntüleri (en az 1, önerilen 1920×1080), açıklama, kategori: *İş*.
   - Yaş derecelendirmesi anketi.
   - **Fiyat:** Ücretsiz. Lisans uygulama içinde satılır (oyun olmayan uygulamalarda Microsoft komisyon almaz).
   - `runFullTrust` yeteneği için açıklama: *"Masaüstü uygulamasıdır (WPF); yerel veritabanı, yazıcı ve dosya erişimi gerekir."*
5. MSIX'i Microsoft imzalar; sizin sertifika almanıza gerek yoktur.

## 2. Google Play (Android – salt okunur)

Android uygulaması (`src/Kuyumcu.Android`, .NET MAUI) **salt okunurdur**: bilgisayardaki uygulamanın Google Drive'a
yüklediği `KuyumcuDefteri_son_yedek.db3` dosyasını kendisi indirir ve gösterir. Açılışta ve uygulamaya her dönüşte
yeni yedek var mı bakar (en sık 2 dakikada bir), "Yenile" ile elle de bakılır. İnternet yoksa telefondaki son yedek
gösterilir; ekranda bilgisayarın o yedeği ne zaman aldığı yazar. Drive bağlanamazsa "Yedek dosyasını seç" ile .db3
elle açılabilir; "Örnek verilerle dene" kurulum olmadan uygulamayı gösterir (Play incelemesi için de kullanılır).

### 2.1 Uygulama ve imzalama (bir kere)

1. [Play Console](https://play.google.com/console)'da uygulama oluşturun. Uygulama adı **Kuyumcu Defteri**,
   paket adı **`com.kuyumcudefteri.app`** (`src/Kuyumcu.Android/Kuyumcu.Android.csproj` → `ApplicationId`).
   **İlk yüklemeden sonra değiştirilemez.** Varsayılan dil: Türkçe. Uygulama / Ücretsiz.
2. Yükleme anahtarı (upload keystore) oluşturun ve **güvenli bir yerde yedekleyin** (kaybolursa Play Console'dan
   anahtar sıfırlama istenebilir ama uğraştırır):

   ```powershell
   keytool -genkeypair -v -keystore upload.keystore -alias upload -keyalg RSA -keysize 4096 -validity 10000
   certutil -encode upload.keystore upload.keystore.b64   # baştaki/sondaki "-----BEGIN/END" satırlarını silin
   ```

   (`keytool`: `C:\Program Files (x86)\Android\openjdk\jdk-17.0.14\bin\keytool.exe`)
3. GitHub **Secrets**'a ekleyin:

   | Secret | Değer |
   |---|---|
   | `ANDROID_KEYSTORE_BASE64` | `upload.keystore.b64` içeriği (tek satır) |
   | `ANDROID_KEYSTORE_PASSWORD` | keystore şifresi |
   | `ANDROID_KEY_ALIAS` | `upload` |
   | `ANDROID_KEY_PASSWORD` | anahtar şifresi |

   Android derlemesi `GOOGLE_DESKTOP_*` bilgilerini **kullanmaz** (bilerek: masaüstü istemci sırrı APK'ya girmesin).
4. **Play Uygulama İmzalama** açık kalsın (varsayılan). İlk AAB yüklendikten sonra
   *Test ve yayınla → Uygulama bütünlüğü → Uygulama imzalama* sayfasındaki **"Uygulama imzalama anahtarı sertifikası"
   SHA-1** değerini not edin (2.2'de gerekecek).
5. İlk AAB: `git tag v2.0.0 && git push origin v2.0.0` → GitHub Release'teki `com.kuyumcudefteri.app-Signed.aab`
   dosyasını **Dahili test** kanalına yükleyin. Sürüm kodu (`ApplicationVersion`) CI'da `100 + çalıştırma numarası`dır,
   her yayında artar.

### 2.2 Google Drive erişimi: Android OAuth istemcisi (bir kere)

Telefon, masaüstünün yüklediği dosyayı `drive.file` kapsamıyla görür. Bu kapsam **yalnızca aynı Google Cloud
projesinin istemcilerinin oluşturduğu dosyaları** gösterir; bu yüzden Android istemcisi masaüstü istemcisinin
(bkz. 3. bölüm) **AYNI projesinde** açılmalıdır. Uygulamada istemci kimliği/sırrı yoktur: Google, isteği
paket adı + imza sertifikasının SHA-1'i ile eşler.

1. [Google Cloud Console](https://console.cloud.google.com/) → masaüstü istemcisinin olduğu proje →
   **API'ler ve Hizmetler → Kimlik bilgileri → Kimlik bilgisi oluştur → OAuth istemci kimliği → Android**.
2. Paket adı `com.kuyumcudefteri.app`, SHA-1 olarak aşağıdakilerin her biri için **ayrı birer** Android istemcisi açın:

   | Ne için | SHA-1 nereden |
   |---|---|
   | Play'den kurulan sürümler (kapalı test, üretim) | Play Console → Uygulama bütünlüğü → **Uygulama imzalama anahtarı** SHA-1 |
   | CI'ın imzaladığı AAB'yi doğrudan kurmak (isteğe bağlı) | `keytool -list -v -keystore upload.keystore -alias upload` |
   | Geliştirme (Visual Studio / `dotnet build`) | `keytool -list -v -keystore "%LOCALAPPDATA%\Xamarin\Mono for Android\debug.keystore" -alias androiddebugkey -storepass android -keypass android` |

3. **OAuth izin ekranı** "Üretim" durumunda olmalı (3. bölüm). "Test" durumundaysa sadece eklenen test kullanıcıları
   bağlanabilir.
4. Yanlış/eksik yapılandırmada telefonda *"Google girişi bu uygulama sürümü için yapılandırılmamış (OAuth istemcisi /
   SHA-1)"* hatası görünür (Google'ın `DEVELOPER_ERROR` kodu 10).
5. Bağlanılıyor ama *"Drive'da yedek bulunamadı"* deniyorsa: bilgisayarda Drive yedeği açık mı, ilk yedek alındı mı,
   telefonda aynı Google hesabı mı seçildi, Android istemcisi masaüstüyle aynı projede mi, kontrol edin.

### 2.3 Mağaza kaydı ve formlar

- **Gizlilik politikası URL'si:** `docs/GIZLILIK.md` taslağını doldurup herkese açık bir adreste yayınlayın.
- **Uygulama erişimi:** "Tüm işlevler özel erişim gerektirmez" yerine *"Bazı işlevler kısıtlı"* seçip şu talimatı
  yazın: *"Uygulama, Windows uygulamasının kullanıcının kendi Google Drive'ına aldığı yedeği okur. İnceleme için açılış
  ekranında 'Örnek verilerle dene' düğmesine dokunun; tüm ekranlar örnek verilerle çalışır. Giriş bilgisi gerekmez."*
- **Reklam içerir mi:** Hayır.
- **İçerik derecelendirmesi:** anket → kategori "Yardımcı Program, Üretkenlik, İletişim veya Diğer"; şiddet/kumar vb.
  hepsi hayır.
- **Hedef kitle:** 18 yaş ve üzeri. Çocuklara yönelik değil.
- **Kategori:** İş. **Etiketler:** muhasebe/defter.
- **Mağaza girişi:** kısa açıklama (80 karakter), tam açıklama, simge 512×512, öne çıkan görsel 1024×500, en az
  2 telefon ekran görüntüsü (örnek verilerle alınabilir: gerçek müşteri adı görünmesin).
- **Finansal özellikler beyanı** (sorulursa): uygulama ödeme, kredi, kripto vb. hizmet **sunmaz**; işletmenin kendi
  veresiye/emanet defterini görüntüler. "Bu özelliklerin hiçbiri" seçilir.

### 2.4 Veri güvenliği formu (önerilen yanıtlar)

Tanımlar Google'a göre: "toplama" = verinin cihazdan **geliştiriciye veya üçüncü tarafa** aktarılması.

| Soru | Yanıt | Gerekçe |
|---|---|---|
| Uygulama zorunlu veri türlerinden herhangi birini topluyor veya paylaşıyor mu? | **Hayır** (aşağıdaki nota bakın) | Müşteri verisi kullanıcının kendi Drive'ından telefona iner, telefonda kalır; geliştiricinin sunucusu yok, analitik/reklam SDK'sı yok. |
| Aktarılan veriler şifreleniyor mu? | Evet | Tüm bağlantılar HTTPS. |
| Kullanıcı veri silinmesini isteyebilir mi? | Evet | "Telefondan sil", "Bağlantıyı kes", uygulamayı kaldırmak; Drive'daki dosyalar kullanıcının kendi hesabında. |

Not / dikkat:
- Google Drive'a erişim, kullanıcının **kendi** hesabındaki dosyaya kendi cihazından erişimdir; Google'ın tanımında
  bu, geliştiricinin veri toplaması sayılmaz. Paylaş düğmeleri (özet/fiyat görseli) kullanıcının başlattığı
  paylaşımdır ve beyan gerektirmez.
- **Altın fiyatı:** fiyat sitesi üye kodu/şifresi bu sitede oturum açmak için o siteye gönderilir. Fiyat kaynağı
  şifreli bir siteyse, forma *"Kişisel bilgiler → Diğer bilgiler: toplanır, paylaşılmaz, isteğe bağlı, amaç: uygulama
  işlevselliği"* eklemek temkinli yoldur. Fiyat kaynağı şifresiz bir siteye geçerse bu satır gerekmez.
- Uygulamaya analitik, reklam veya hata raporlama SDK'sı eklenirse form **güncellenmelidir**.

### 2.5 Kapalı test ve üretime çıkış

Kişisel (bireysel) geliştirici hesabı Kasım 2023'ten sonra açıldıysa, **yeni her uygulama** için üretime çıkmadan
önce kapalı test zorunludur: **en az 12 test kullanıcısı, kesintisiz 14 gün boyunca katılımda** kalmalı.
(Daha önce başka bir uygulamada yapılmış olması bu uygulamayı muaf tutmaz.) Süre uzun olduğu için erken başlayın:

1. **Dahili test** (100 kişiye kadar, inceleme beklemeden): AAB'yi yükleyin, kendi hesabınızla kurup Drive bağlantısını
   ve örnek verileri deneyin. Uygulama imzalama SHA-1'i için Android OAuth istemcisini (2.2) bu aşamada açın.
2. **Kapalı test** kanalı oluşturun → test kullanıcılarını e-posta listesi ya da Google Grubu olarak ekleyin
   (12'den fazla, ör. 15-20 kişi; bırakanlar olabilir) → katılım bağlantısını paylaşın.
   Test kullanıcıları bağlantıdan "test kullanıcısı ol"a basıp Play'den kurmalı ve **14 gün boyunca kayıtlı kalmalı**.
   Kuyumcu olmayan test kullanıcıları "Örnek verilerle dene" ile kullanabilir.
3. 14 gün dolunca Play Console → **Kontrol paneli → Üretim erişimi için başvur**: test sürecine ve geri bildirimlere
   dair kısa anket doldurulur. Onay birkaç gün sürebilir.
4. Onaydan sonra aynı AAB'yi (ya da daha yenisini) **Üretim**'e yükleyin; aşamalı (ör. %20) yayın seçilebilir.

### 2.6 Yerelde derleme / deneme

```powershell
dotnet build src/Kuyumcu.Android -f net10.0-android                       # 'maui-android' iş yükü ve API 36 gerekir
dotnet build src/Kuyumcu.Android -f net10.0-android -t:Run                 # açık emülatöre/cihaza kurup çalıştırır
```

Bu bilgisayarda SDK yolu varsayılan değilse: `-p:AndroidSdkDirectory="C:\Program Files (x86)\Android\android-sdk"`.

## 3. Ortak gizli bilgiler

| Secret | Açıklama |
|---|---|
| `GOOGLE_DESKTOP_CLIENT_ID` | Ürünün Google OAuth istemcisi (aşağıya bakın). Boşsa müşteri kendi bilgilerini girmek zorunda kalır. |
| `GOOGLE_DESKTOP_CLIENT_SECRET` | Aynı istemcinin secret değeri. |

Lisans için GitHub'a gizli bilgi **girilmez** (aşağıya bakın).

### Lisans anahtarları

Lisanslar geliştiricinin **özel anahtarıyla imzalanır** (ECDSA P-256). Uygulamada sadece açık anahtar vardır
(`src/Kuyumcu.Core/Licensing/LicensePublicKey.cs`); uygulamayı açıp incelemek yeni lisans üretmeye yetmez.

- Özel anahtar: `%USERPROFILE%\.kuyumcu-lisans\ozel-anahtar.pem` (depoda, CI'da ve buluttaki açık klasörlerde **olmamalı**).
  **Mutlaka yedekleyin** (şifre yöneticisi / şifreli USB). Kaybolursa yeni lisans üretilemez; başkasının eline geçerse
  o kişi lisans üretebilir. İkisinde de çözüm: yeni anahtar çifti + uygulama güncellemesi + tüm müşterilere yeni lisans.
- Lisans üretmek (müşteri cihaz kimliğini Ayarlar > Lisans'tan iletir):

  ```powershell
  dotnet run --project tools/Kuyumcu.LicenseTool -- uret 079FDBAA48F568BF --not "Çarşı Kuyumculuk"            # süresiz
  dotnet run --project tools/Kuyumcu.LicenseTool -- uret 079FDBAA48F568BF --gun 365 --not "Çarşı Kuyumculuk"  # 1 yıl
  dotnet run --project tools/Kuyumcu.LicenseTool -- coz KD1-XXXXX-...                                          # anahtarı incele
  ```

  Üretilen her anahtar `%USERPROFILE%\.kuyumcu-lisans\lisans-kayitlari.csv` dosyasına da yazılır.
- Lisans cihaza (Windows kurulumunun MachineGuid'ine) bağlıdır. Windows yeniden kurulursa veya bilgisayar değişirse
  müşteri yeni cihaz kimliğini iletir, yeni anahtar üretilir.

### Google Drive OAuth istemcisi (bir kere)

Müşterilerin Google Cloud ile uğraşmaması için ürünün kendi OAuth istemcisi kullanılır:

1. [Google Cloud Console](https://console.cloud.google.com/) → yeni proje (örn. "Kuyumcu Defteri").
2. **API'ler ve Hizmetler → Kütüphane → Google Drive API → Etkinleştir.**
3. **OAuth izin ekranı:** Kullanıcı türü *Harici*, uygulama adı "Kuyumcu Defteri", kapsam:
   `.../auth/drive.file` (sadece uygulamanın oluşturduğu dosyalar; hassas olmayan kapsamdır, Google güvenlik
   denetimi gerektirmez). Ardından **Yayınla → Üretim** yapın (aksi halde sadece test kullanıcıları giriş yapabilir
   ve Google girişleri 7 günde bir düşer — otomatik yedek durur, müşteri "Yeniden bağlanın" uyarısı görür).
4. **Kimlik Bilgileri → OAuth istemci kimliği → Masaüstü uygulaması** → Client ID ve Secret'ı yukarıdaki
   GitHub Secrets'a girin. (Masaüstü uygulamalarında bu "secret" gerçek bir sır sayılmaz; Google'ın belgelediği
   kurulu uygulama akışıdır.)
5. Android uygulaması için aynı projede **Android** türü istemci(ler) açın: bkz. **2.2**. Ayrı bir proje açılırsa
   telefon masaüstünün yüklediği yedekleri göremez.

## 4. Mevcut (ilk) müşterinin yeni sürüme geçişi

Eski uygulama (`com.companyname.kuyumcu`) ile yeni Store uygulaması farklı paketlerdir, yan yana kurulabilir.

1. Müşteri Store'dan yeni uygulamayı kurar.
2. İlk açılışta **"Eski sürümün verileri bulundu → Eski Verileri Aktar"** düğmesi çıkar; tek tıkla müşteriler,
   işlemler ve ayarlar aktarılır. (Bulunamazsa: eski uygulamada Drive yedeği alıp yeni uygulamada
   Ayarlar → Yedeği İçe Aktar.)
3. Eski lisans anahtarı (`KUYM-...`) yeni sürümde geçmez; aktarımdan sonra **"Lisans yenilenmeli"** ekranı çıkar.
   Müşteri ekrandaki cihaz kimliğini iletir, `tools/Kuyumcu.LicenseTool` ile yeni anahtar üretilip verilir.
4. Altın fiyatı özelliği için: Ayarlar → **Altın Fiyatı Görseli** bölümüne ekeo üye kodu/şifresi girilir ve
   `musteri-varliklari/carsi_kuyumculuk_template.jpg` şablon olarak yüklenir.
5. Her şey doğrulandıktan sonra eski uygulama kaldırılabilir.
