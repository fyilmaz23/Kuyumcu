# Google Drive Yedekleme Ayarları

## Google API Kimlik Bilgilerinizi Ayarlama

Veritabanını Google Drive'a yedekleyebilmek için, Google Cloud Console'dan API kimlik bilgilerini almanız gerekiyor. Bu işlem aşağıdaki adımları izleyerek gerçekleştirilebilir:

1. [Google Cloud Console](https://console.cloud.google.com/)'a gidin ve oturum açın.
2. Yeni bir proje oluşturun veya mevcut bir projeyi seçin.
3. Sol menüden "API'ler ve Hizmetler" > "Kimlik Bilgileri" seçeneğine tıklayın.
4. "Kimlik Bilgileri Oluştur" düğmesine tıklayın ve "OAuth istemci kimliği" seçeneğini seçin.
5. Uygulama türü olarak "Masaüstü uygulaması" seçin.
6. İstemci adı girin (örneğin, "Kuyumcu Uygulaması") ve "Oluştur" düğmesine tıklayın.
7. Oluşturulduğunda, size bir İstemci Kimliği (Client ID) ve İstemci Sırrı (Client Secret) verilecektir.

## API Erişimini Etkinleştirme

1. Google Cloud Console'da sol menüden "API'ler ve Hizmetler" > "Kütüphane" seçeneğine tıklayın.
2. Arama kutusuna "Google Drive API" yazın ve sonuçlardan "Google Drive API"yi seçin.
3. "Etkinleştir" düğmesine tıklayın.

## Kuyumcu Uygulamasında Ayarları Yapılandırma

Elde ettiğiniz kimlik bilgilerini aşağıdaki dosyada uygun yerlere yerleştirin:

```
c:\Users\fatih\source\repos\Kuyumcu\Services\GoogleDriveService.cs
```

Aşağıdaki satırları bulun (yaklaşık 93-96. satırlar):

```csharp
var clientSecrets = new ClientSecrets
{
    // NOTE: These are example values and must be replaced with actual values from the Google Cloud Console
    ClientId = "YOUR_CLIENT_ID.apps.googleusercontent.com",
    ClientSecret = "YOUR_CLIENT_SECRET"
};
```

Bu satırları Google Cloud Console'dan aldığınız bilgilerle değiştirin:

```csharp
var clientSecrets = new ClientSecrets
{
    ClientId = "123456789-abc123def456.apps.googleusercontent.com", // Kendi Client ID'nizi yazın
    ClientSecret = "abc123def456" // Kendi Client Secret'ınızı yazın
};
```

## İlk Kullanım

İlk kez "Google Drive'a Yedekle" butonuna tıkladığınızda, tarayıcıda bir Google oturum açma sayfası açılacak ve uygulamaya Google Drive'ınıza erişim izni vermeniz istenecektir. Bu izni verdikten sonra, uygulama veritabanınızı Google Drive'a yükleyebilecek.

## Güvenlik Notu

İstemci Kimliği (Client ID) ve İstemci Sırrı (Client Secret) bilgilerinizi güvende tutun ve başkalarıyla paylaşmayın. Bu bilgiler Google hesabınıza sınırlı erişim sağlamak için kullanılır.
