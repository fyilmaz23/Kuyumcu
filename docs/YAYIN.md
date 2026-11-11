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

1. [Play Console](https://play.google.com/console)'da uygulama oluşturun. Paket adı: **`com.kuyumcudefteri.app`**
   (`src/Kuyumcu.Mobile/Kuyumcu.Mobile.csproj` → `ApplicationId`). **İlk yüklemeden sonra değiştirilemez**;
   farklı bir ad isteniyorsa ilk yüklemeden önce değiştirin.
2. Yükleme anahtarı (upload keystore) oluşturun ve **güvenli bir yerde yedekleyin**:

   ```bash
   keytool -genkeypair -v -keystore upload.keystore -alias upload -keyalg RSA -keysize 4096 -validity 10000
   base64 -w0 upload.keystore > upload.keystore.b64   # Windows: certutil -encode upload.keystore upload.b64
   ```

3. GitHub **Secrets**'a ekleyin:

   | Secret | Değer |
   |---|---|
   | `ANDROID_KEYSTORE_BASE64` | `upload.keystore.b64` içeriği |
   | `ANDROID_KEYSTORE_PASSWORD` | keystore şifresi |
   | `ANDROID_KEY_ALIAS` | `upload` |
   | `ANDROID_KEY_PASSWORD` | anahtar şifresi |

4. Play Console'da **Play Uygulama İmzalama**'yı açık bırakın (varsayılan).
5. Doldurulması gerekenler: **Veri güvenliği formu** (veri cihazda kalır, üçüncü tarafla paylaşılmaz),
   gizlilik politikası URL'si, içerik derecelendirmesi, hedef kitle (18+ / işletmeler).
6. Hesabınız bireysel ve Kasım 2023'ten sonra açıldıysa: üretime çıkmadan önce **en az 12 test kullanıcısıyla
   14 gün kapalı test** zorunludur.

## 3. Ortak gizli bilgiler

| Secret | Açıklama |
|---|---|
| `LICENSE_SECRET_KEY` | Lisans anahtarlarını imzalayan gizli anahtar. **Değiştirilirse eski lisans anahtarları geçersiz olur.** |
| `GOOGLE_DESKTOP_CLIENT_ID` | Ürünün Google OAuth istemcisi (aşağıya bakın). Boşsa müşteri kendi bilgilerini girmek zorunda kalır. |
| `GOOGLE_DESKTOP_CLIENT_SECRET` | Aynı istemcinin secret değeri. |

### Google Drive OAuth istemcisi (bir kere)

Müşterilerin Google Cloud ile uğraşmaması için ürünün kendi OAuth istemcisi kullanılır:

1. [Google Cloud Console](https://console.cloud.google.com/) → yeni proje (örn. "Kuyumcu Defteri").
2. **API'ler ve Hizmetler → Kütüphane → Google Drive API → Etkinleştir.**
3. **OAuth izin ekranı:** Kullanıcı türü *Harici*, uygulama adı "Kuyumcu Defteri", kapsam:
   `.../auth/drive.file` (sadece uygulamanın oluşturduğu dosyalar; hassas olmayan kapsamdır, Google güvenlik
   denetimi gerektirmez). Ardından **Yayınla → Üretim** yapın (aksi halde sadece test kullanıcıları giriş yapabilir).
4. **Kimlik Bilgileri → OAuth istemci kimliği → Masaüstü uygulaması** → Client ID ve Secret'ı yukarıdaki
   GitHub Secrets'a girin. (Masaüstü uygulamalarında bu "secret" gerçek bir sır sayılmaz; Google'ın belgelediği
   kurulu uygulama akışıdır.)

## 4. Mevcut (ilk) müşterinin yeni sürüme geçişi

Eski uygulama (`com.companyname.kuyumcu`) ile yeni Store uygulaması farklı paketlerdir, yan yana kurulabilir.

1. Müşteri Store'dan yeni uygulamayı kurar.
2. İlk açılışta **"Eski sürümün verileri bulundu → Eski Verileri Aktar"** düğmesi çıkar; tek tıkla müşteriler,
   işlemler, ayarlar ve lisans aktarılır. (Bulunamazsa: eski uygulamada Drive yedeği alıp yeni uygulamada
   Ayarlar → Yedeği İçe Aktar.)
3. Altın fiyatı özelliği için: Ayarlar → **Altın Fiyatı Görseli** bölümüne ekeo üye kodu/şifresi girilir ve
   `musteri-varliklari/carsi_kuyumculuk_template.jpg` şablon olarak yüklenir.
4. Her şey doğrulandıktan sonra eski uygulama kaldırılabilir.
