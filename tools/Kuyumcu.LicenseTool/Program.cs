using System.Globalization;
using Kuyumcu.Licensing;

// Kuyumcu Defteri lisans aracı (sadece geliştirici bilgisayarında).
//
//   dotnet run --project tools/Kuyumcu.LicenseTool -- anahtar-olustur
//   dotnet run --project tools/Kuyumcu.LicenseTool -- uret 079FDBAA48F568BF [--gun 365 | --bitis 31.12.2027] [--not "Çarşı Kuyumculuk"]
//   dotnet run --project tools/Kuyumcu.LicenseTool -- coz KD1-XXXXX-...
//   dotnet run --project tools/Kuyumcu.LicenseTool -- acik-anahtar

var tr = new CultureInfo("tr-TR");
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CurrentCulture = tr;
Console.OutputEncoding = System.Text.Encoding.UTF8;

try
{
    return args.FirstOrDefault()?.ToLowerInvariant() switch
    {
        "anahtar-olustur" => CreateKeyPair(),
        "uret" => Issue(args.Skip(1).ToArray()),
        "coz" => Inspect(string.Join(' ', args.Skip(1))),
        "acik-anahtar" => ShowPublicKey(),
        _ => Usage(),
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"HATA: {ex.Message}");
    return 1;
}

int Usage()
{
    Console.WriteLine("""
        Kuyumcu Defteri lisans aracı

          anahtar-olustur                 Bir kere: imza anahtar çiftini üretir.
          uret <cihaz-kimligi> [seçenek]  Lisans anahtarı üretir.
              --gun <N>                   N gün geçerli (varsayılan: süresiz)
              --bitis <GG.AA.YYYY>        Bu tarih dahil geçerli
              --not "<metin>"             Kayıt dosyasına yazılacak not (ör. iş yeri adı)
          coz <anahtar>                   Anahtarın içeriğini gösterir ve imzasını doğrular.
          acik-anahtar                    LicensePublicKey.cs'e yazılacak açık anahtarı gösterir.
        """);
    Console.WriteLine($"Özel anahtar: {LicenseSigningKey.DefaultPath}");
    Console.WriteLine($"(Farklı yol için {LicenseSigningKey.PathEnvironmentVariable} ortam değişkeni)");
    return 2;
}

int CreateKeyPair()
{
    var publicKey = LicenseSigningKey.Create();
    Console.WriteLine($"Özel anahtar oluşturuldu: {LicenseSigningKey.DefaultPath}");
    Console.WriteLine();
    Console.WriteLine("!! Bu dosyayı GÜVENLİ bir yere yedekleyin (şifre yöneticisi, şifreli USB). Kaybolursa yeni lisans üretemezsiniz;");
    Console.WriteLine("!! başkasının eline geçerse o kişi lisans üretebilir. Depoya / buluta açık şekilde koymayın.");
    Console.WriteLine();
    Console.WriteLine("Açık anahtar (src/Kuyumcu.Core/Licensing/LicensePublicKey.cs içindeki Base64 değeri):");
    Console.WriteLine(publicKey);
    return 0;
}

int ShowPublicKey()
{
    using var key = LicenseSigningKey.Load();
    var publicKey = LicenseSigningKey.ExportPublicKey(key);
    Console.WriteLine(publicKey);
    Console.WriteLine(publicKey == LicensePublicKey.Base64
        ? "(Uygulamadaki açık anahtarla aynı.)"
        : "UYARI: Uygulamadaki açık anahtar farklı! Bu özel anahtarla üretilen lisanslar uygulamada geçmez.");
    return 0;
}

int Issue(string[] options)
{
    var deviceId = options.FirstOrDefault(o => !o.StartsWith("--", StringComparison.Ordinal));
    if (deviceId == null || !LicenseCodec.IsValidDeviceId(deviceId))
        throw new ArgumentException("Geçerli bir cihaz kimliği verin (16 karakter, ör. 079FDBAA48F568BF). Müşteri bunu Ayarlar > Lisans'ta görür.");

    var today = DateOnly.FromDateTime(DateTime.Today);
    DateOnly? expires = null;
    if (Option("--gun") is { } days)
        expires = today.AddDays(int.Parse(days, tr) - 1);
    if (Option("--bitis") is { } until)
        expires = DateOnly.ParseExact(until, "dd.MM.yyyy", tr);
    var note = Option("--not") ?? string.Empty;

    using var key = LicenseSigningKey.Load();
    var payload = new LicensePayload(LicenseCodec.NormalizeDeviceId(deviceId), today, expires);
    var license = LicenseCodec.Issue(payload, key);

    // Kendi kendini doğrula: hem bu özel anahtarın açık karşılığıyla hem uygulamadaki açık anahtarla
    var check = LicenseCodec.Verify(license, payload.DeviceId, today, key);
    if (!check.IsValid)
        throw new InvalidOperationException("Üretilen anahtar doğrulanamadı: " + check.Message);
    var matchesApp = LicenseSigningKey.ExportPublicKey(key) == LicensePublicKey.Base64;

    var logPath = Path.Combine(Path.GetDirectoryName(LicenseSigningKey.DefaultPath)!, "lisans-kayitlari.csv");
    if (!File.Exists(logPath))
        File.WriteAllText(logPath, "tarih;cihaz;bitis;not;anahtar" + Environment.NewLine);
    File.AppendAllText(logPath, $"{DateTime.Now:dd.MM.yyyy HH:mm};{payload.DeviceId};{expires?.ToString("dd.MM.yyyy") ?? "süresiz"};{note.Replace(';', ',')};{license}{Environment.NewLine}");

    Console.WriteLine($"Cihaz   : {payload.DeviceId}");
    Console.WriteLine($"Geçerli : {(expires is { } e ? e.ToString("dd.MM.yyyy") + " dahil" : "süresiz")}");
    if (note.Length > 0) Console.WriteLine($"Not     : {note}");
    Console.WriteLine();
    Console.WriteLine(license);
    Console.WriteLine();
    Console.WriteLine($"Kayıt: {logPath}");
    if (!matchesApp)
        Console.WriteLine("UYARI: Uygulamadaki açık anahtar bu özel anahtarla eşleşmiyor; bu lisans uygulamada GEÇMEZ.");
    return 0;

    string? Option(string name)
    {
        var index = Array.FindIndex(options, o => string.Equals(o, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < options.Length ? options[index + 1] : null;
    }
}

int Inspect(string license)
{
    var result = LicenseCodec.Decode(license, LicensePublicKey.Create());
    Console.WriteLine(result.IsValid ? "İmza: GEÇERLİ (uygulamadaki açık anahtarla)" : $"İmza: GEÇERSİZ — {result.Message}");
    if (result.Payload is { } p)
    {
        Console.WriteLine($"Cihaz   : {p.DeviceId}");
        Console.WriteLine($"Veriliş : {p.IssuedOn:dd.MM.yyyy}");
        Console.WriteLine($"Geçerli : {(p.ExpiresOn is { } e ? e.ToString("dd.MM.yyyy") + " dahil" : "süresiz")}");
    }
    return result.IsValid ? 0 : 1;
}
