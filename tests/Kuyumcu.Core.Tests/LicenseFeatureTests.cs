using System.Security.Cryptography;
using System.Text;
using Kuyumcu.Licensing;
using Kuyumcu.Models;
using Kuyumcu.Platform;

namespace Kuyumcu.Tests;

/// <summary>Ek ücretli modüller (barkod) lisans anahtarına imzayla gömülür.</summary>
public class LicenseFeatureTests
{
    private const string Device = "079FDBAA48F568BF";
    private static readonly DateOnly Today = new(2026, 11, 26);

    [Fact]
    public void Barkodlu_anahtar_modulu_tasir()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var license = LicenseCodec.Issue(new LicensePayload(Device, Today, null, LicenseFeatures.Barcode), key);

        var result = LicenseCodec.Verify(license, Device, Today, key);

        Assert.True(result.IsValid);
        Assert.Equal(LicenseFeatures.Barcode, result.Payload!.Features);
    }

    [Fact]
    public void Barkodsuz_anahtar_modul_acmaz()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var license = LicenseCodec.Issue(new LicensePayload(Device, Today, null), key);

        var result = LicenseCodec.Verify(license, Device, Today, key);

        Assert.True(result.IsValid);
        Assert.Equal(LicenseFeatures.None, result.Payload!.Features);
    }

    [Fact]
    public void Anahtardaki_modul_bayti_elle_degistirilirse_imza_gecmez()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var license = LicenseCodec.Issue(new LicensePayload(Device, Today, null), key);

        var bytes = Base32Decode(Compact(license)[LicenseCodec.Prefix.Length..]);
        bytes[13] = (byte)LicenseFeatures.Barcode; // modüller baytı
        var tampered = LicenseCodec.Prefix + "-" + Base32Encode(bytes);

        var result = LicenseCodec.Verify(tampered, Device, Today, key);

        Assert.Equal(LicenseKeyError.BadSignature, result.Error);
    }

    [Fact]
    public void Sureli_barkodlu_lisans_bitince_modul_de_kapanir()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var license = LicenseCodec.Issue(new LicensePayload(Device, Today, Today.AddDays(10), LicenseFeatures.Barcode), key);

        var result = LicenseCodec.Verify(license, Device, Today.AddDays(11), key);

        Assert.Equal(LicenseKeyError.Expired, result.Error);
    }

    [Theory]
    [InlineData(LicenseStatus.Licensed, LicenseFeatures.Barcode, true)]
    [InlineData(LicenseStatus.Licensed, LicenseFeatures.None, false)]
    [InlineData(LicenseStatus.Trial, LicenseFeatures.None, false)]
    [InlineData(LicenseStatus.Trial, LicenseFeatures.Barcode, false)]
    [InlineData(LicenseStatus.Expired, LicenseFeatures.Barcode, false)]
    public void Barkod_modulu_sadece_barkodlu_gecerli_lisansta_acik(LicenseStatus status, LicenseFeatures features, bool expected)
    {
        var state = new LicenseState { Status = status, Features = features };

        Assert.Equal(expected, state.HasBarcodeModule);
    }

    [Fact]
    public void WhatsApp_baglantisi_numarayi_ve_cihaz_kimligini_icerir()
    {
        var url = SalesContact.WhatsAppUrl("+90 (532) 123 45 67", SalesContact.BarcodeInquiryMessage(Device));

        Assert.StartsWith("https://wa.me/905321234567?text=", url);
        var text = Uri.UnescapeDataString(url[(url.IndexOf("text=", StringComparison.Ordinal) + 5)..]);
        Assert.Contains("barkod modülü hakkında bilgi almak istiyorum", text);
        Assert.EndsWith("Cihaz kimliğim: " + Device, text);
        Assert.DoesNotContain(' ', url); // boşluk ve satır sonu kodlanmış olmalı
    }

    private static string Compact(string key) =>
        new(key.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());

    // Test için Crockford Base32 (LicenseCodec'teki ile aynı alfabe)
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private static byte[] Base32Decode(string text)
    {
        var output = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in text)
        {
            buffer = (buffer << 5) | Alphabet.IndexOf(c);
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)(buffer >> (bits - 8)));
                bits -= 8;
            }
            buffer &= (1 << bits) - 1;
        }
        return output.ToArray();
    }

    private static string Base32Encode(byte[] data)
    {
        var sb = new StringBuilder();
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                sb.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
            buffer &= (1 << bits) - 1;
        }
        if (bits > 0)
            sb.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        return sb.ToString();
    }
}
