# Etiket Yazıcısı ve Barkod Okuyucu Denemesi

Barkodlu vitrin stoğunu dükkân cihazlarıyla ilk kez denemek için adımlar.
Cihazlar: **TSC TE210** (203 dpi, TSPL, USB), **Sunlux RH10** (2D, USB), **72 × 10 mm kelebek kuyumcu etiketi**.

> Uygulama gerçek veritabanını kullanır (`%LOCALAPPDATA%\KuyumcuDefteri`). İlk açılışta stok tabloları eklenir;
> müşteri ve işlem verilerine dokunulmaz. Yine de önce **Ayarlar → Bilgisayardaki Otomatik Yedekler → Şimdi Yedekle**.

## 1. Uygulamayı açın

```powershell
git switch feature/barkod-stok
dotnet run --project src/Kuyumcu.Desktop
```

- Açık başka bir Kuyumcu Defteri penceresi varsa önce kapatın (aynı anda tek pencere açılır).
- Sol menüde **Vitrin Stoku, Okut / Satış, Ürün Girişi, Sayım, Kasa, Stok Raporları** görünmeli.
- Hata çıkarsa mesajı ve `dotnet run` penceresindeki çıktıyı not edin.

## 2. Yazıcıyı Windows'ta hazırlayın

1. TSC'nin Windows sürücüsünü kurun ve yazıcıyı USB ile bağlayın.
2. Yazıcının adını görün:
   ```powershell
   Get-Printer | Where-Object Name -like "*TSC*" | Select-Object Name, DriverName, PortName
   ```
3. Etiketi ve ribonu takın, **gap (boşluk) sensörünü kalibre edin**. Kalibrasyonu yazıcının kılavuzundaki
   yöntemle ya da TSC'nin Diagnostic Tool / Console programındaki "Calibrate" ile yapabilirsiniz.
   Kalibrasyon yapılmazsa yazıcı etiket boyunu yanlış ölçer ve baskı iki etikete taşar.

Uygulama yazıcıya hazır **TSPL komutları** gönderir (Windows'un RAW kuyruğu üzerinden). Etiket ölçüsünü sürücüde
ayarlamanız gerekmez; ölçüyü komut (`SIZE 72 mm,10 mm`, `GAP 3 mm`) belirler.

## 3. Ayarlar → Etiket Yazıcısı

1. **Bağlantı:** USB (Windows yazıcısı). **Yazıcı:** listeden TSC'yi seçin (görünmüyorsa *Listeyi yenile*).
2. Etiket: 72 × 10 mm, boşluk 3 mm, yön 1, koyuluk 10, hız 3 (varsayılanlar).
3. **Kaydet**.

## 4. Hizalama etiketi (ilk etiket)

**Hizalama etiketi** düğmesine basın. Etikete şunlar basılır:
- Etiketin kenarına bir **çerçeve**,
- Bir **mm cetveli**: her 1 mm'de kısa çentik, her 5 mm'de uzun çentik ve numara (5, 10 … 70), her 10 mm'de boydan boya çizgi.

Etikete bakın:

| Gördüğünüz | Ne yapmalı |
|---|---|
| Çerçeve etiketin kenarına oturuyor | Kaydırma gerekmez |
| Çerçeve sağa/sola kaymış | **X kaydırma**: içerik sağa kayıksa negatif, sola kayıksa pozitif değer (0,25 mm adımlarla) |
| Çerçeve yukarı/aşağı kaymış | **Y kaydırma** (+ aşağı, − yukarı) |
| Baskı iki etikete bölünüyor / her seferinde kayıyor | Gap sensörünü yeniden kalibre edin; **Boşluk (gap)** değerini kontrol edin |
| Baskı ters (baş aşağı) | **Baskı yönü**: 0 |
| Çok soluk / dağılmış | **Koyuluk** artırın / azaltın (resin ribonda 10–12) |
| Hiç basmıyor | Yazıcı adı doğru mu? Windows yazdırma kuyruğunda iş var mı? Hata mesajını not edin |

Sonra **kanatların** yerini okuyun: hangi mm aralığı 1. kanat, hangisi 2. kanat, kuyruk nerede başlıyor.
Etiketi ürüne sarıp kanatları kapattığınızda hangi yüzlerin dışarıda kaldığına da bakın.

Değerleri değiştirip tekrar **Hizalama etiketi** basın. Ekrandaki önizleme değişikliği anında gösterir.
Düğmeler kaydetmeden de ekrandaki değerlerle basar; uygun olunca **Kaydet**.

## 5. Kod ve yazının yeri

Varsayılan yerleşim TE210 + 72 × 10 mm kuyruklu etikette ölçüldü: yazıcı etiketi **kuyruk solda** besler; kuyruk
0–30 mm, 1. kanat 30–50 mm, 2. kanat 50–70 mm. Kod X=31,5, rakamlar X=38, gram/ayar X=51,5 mm. Farklı bir etikette
kanat başlangıçlarını cetvelden okuyup şöyle ayarlayın:

- **Kod X** = 1. kanadın başı + 1 mm
- **Rakamlar X** = Kod X + kod genişliği (ekranda "Kod ≈ 5,3 mm" yazar) + 1 mm
- **Yazı X** = 2. kanadın başı + 1 mm
- **Y** değerleri: 10 mm yükseklikte içerik ortada kalsın (varsayılan 2–2,2 mm).

## 6. Test etiketi

**Test etiketi** basın. Üzerinde test kodu (200000000004; hiçbir ürüne verilmez, okutulunca "test etiketi" der), "12,34 gr", "22K" ve Türkçe harf satırı
`ÇĞİŞÖÜ` vardır.

- TE210'da Türkçe harfler doğru çıkıyor; **Türkçe harfleri sadeleştir** varsayılan kapalı. Başka bir yazıcıda bozuk çıkarsa açın
  (etikete "KUNYE", "Carsi" gibi basılır).
- Kodu okuyucuyla okutun (7. adım). Okunmuyorsa **Kare boyu (nokta)** 4 yapın (kod ≈ 7 mm olur) ya da kod tipini
  QR'a çevirip deneyin.

## 7. Okuyucu (Sunlux RH10)

1. Okuyucuyu USB'ye takın, **Not Defteri**'ni açın ve test etiketini okutun.
   Beklenen: `200000000004` ve ardından yeni satır (Enter).
2. Rakamlar yerine başka karakterler geliyorsa okuyucunun klavye dili ayarını kontrol edin (kılavuzdaki ayar
   barkodları). Ürün kodları sadece rakam olduğu için ABD ya da Türkçe Q düzeni ikisi de genelde çalışır.
3. Sonda yeni satır yoksa okuyucuya "Enter/CR son eki" ayarını kılavuzdaki barkodla verin. Uygulama **sadece Enter'la
   biten** girişi işler.

## 8. Uygulamada uçtan uca deneme

1. **Ürün Girişi:** kategori *Bilezik*, ayar *22*, model boş. Bir ürünü etiketsiz tartın, gramı yazıp **Enter**.
   Ürün kaydedilir ve etiketi basılır. Etiketi takın. *Son etiketi tekrar bas* düğmesini de deneyin.
2. **Okut / Satış:** etiketi okutun → ürün, gram ve ayar gelmeli. Alana tıklamadan okutun; odak başka yerdeyken
   bile okutma alana gider.
3. **Satıldı olarak işaretle:** has fiyatı siteden gelir (değiştirilebilir). Kaydedin. Aynı etiketi tekrar okutun →
   "**zaten satılmış**" uyarısı ve uyarı sesi gelmeli. **İade al** ile vitrine geri alın.
4. Kayıtlı olmayan bir kod (örn. başka bir ürünün barkodu) okutun → "kayıtlı değil" ya da "mağaza ürün kodu değil" uyarısı.
5. **Sayım:** bir konum için sayım başlatın, birkaç ürünü okutun, birini iki kez okutun (uyarı), bitirin. Eksik
   listesi çıkmalı; hiçbir ürün kendiliğinden kayıp olmamalı.
6. Deneme ürünlerini ürün detayından **Sil** (neden: "Deneme") ile kaldırabilirsiniz; kodları bir daha verilmez.

## 9. İsteğe bağlı: Ethernet

Yazıcı ağa bağlıysa **Bağlantı: Ağ**, yazıcının IP adresi, port **9100**. Hizalama etiketiyle deneyin.
5 saniyede bağlanamazsa hata mesajı gösterilir.
