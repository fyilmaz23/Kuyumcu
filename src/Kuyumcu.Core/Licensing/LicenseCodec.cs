using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Kuyumcu.Licensing;

/// <summary>Lisans anahtarının içindeki bilgiler (imzalı).</summary>
/// <param name="DeviceId">Lisansın bağlı olduğu cihaz kimliği (16 onaltılık karakter).</param>
/// <param name="IssuedOn">Veriliş tarihi.</param>
/// <param name="ExpiresOn">Son geçerlilik günü; null ise süresiz.</param>
public sealed record LicensePayload(string DeviceId, DateOnly IssuedOn, DateOnly? ExpiresOn);

public enum LicenseKeyError
{
    None,
    Empty,
    /// <summary>Eski (HMAC, "KUYM-...") biçim: artık kabul edilmiyor.</summary>
    OldFormat,
    BadFormat,
    BadSignature,
    WrongDevice,
    Expired,
}

public sealed record LicenseCheckResult(LicenseKeyError Error, LicensePayload? Payload)
{
    public bool IsValid => Error == LicenseKeyError.None;

    public string Message => Error switch
    {
        LicenseKeyError.None => "Geçerli lisans anahtarı.",
        LicenseKeyError.Empty => "Lisans anahtarı boş olamaz.",
        LicenseKeyError.OldFormat => "Bu anahtar eski biçimde. Lisans sistemi yenilendi; lütfen yetkilinizden yeni anahtar isteyin.",
        LicenseKeyError.BadFormat => "Lisans anahtarı eksik ya da hatalı kopyalanmış. Anahtarın tamamını yapıştırdığınızdan emin olun.",
        LicenseKeyError.BadSignature => "Lisans anahtarı geçersiz.",
        LicenseKeyError.WrongDevice => "Bu lisans anahtarı bu bilgisayar için üretilmemiş.",
        LicenseKeyError.Expired => $"Bu lisansın süresi {Payload?.ExpiresOn:dd.MM.yyyy} tarihinde doldu.",
        _ => "Lisans anahtarı geçersiz.",
    };
}

/// <summary>
/// İmzalı lisans anahtarı biçimi.
///
/// Anahtar, geliştiricideki ÖZEL anahtarla ECDSA P-256 ile imzalanır; uygulama sadece AÇIK anahtarı taşır.
/// Açık anahtarla imza doğrulanabilir ama yeni anahtar üretilemez — uygulamayı tersine mühendislikle açmak
/// anahtar üretmeye yetmez.
///
/// Biçim: "KD1-" + Crockford Base32( yük[14] + imza[64] ), 5'li gruplar halinde.
///   yük: sürüm(1) · cihaz(8) · veriliş günü(2) · bitiş günü(2, 0 = süresiz) · bayraklar(1)
///   günler 2024-01-01'den itibaren sayılır.
/// </summary>
public static class LicenseCodec
{
    public const string Prefix = "KD1";
    private const byte FormatVersion = 1;
    private const int PayloadLength = 14;
    private const int SignatureLength = 64;
    private const string OldPrefix = "KUYM";

    private static readonly DateOnly Epoch = new(2024, 1, 1);
    // İmza sadece bu ürünün lisans yükü için geçerli olsun (başka bir amaçla imzalanmış veriyle karışmasın)
    private static readonly byte[] Domain = Encoding.ASCII.GetBytes("KuyumcuDefteri.Lisans.v1:");

    /// <summary>Lisans anahtarı üretir. Sadece geliştirici araçları çağırır (özel anahtar gerekir).</summary>
    public static string Issue(LicensePayload payload, ECDsa privateKey)
    {
        var data = EncodePayload(payload);
        var signature = privateKey.SignData(Signed(data), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        if (signature.Length != SignatureLength)
            throw new InvalidOperationException("Beklenmeyen imza uzunluğu: anahtar P-256 olmalı.");

        var body = Base32.Encode([.. data, .. signature]);
        return Prefix + "-" + string.Join("-", Chunk(body, 5));
    }

    /// <summary>Anahtarın imzasını, cihazını ve süresini doğrular.</summary>
    public static LicenseCheckResult Verify(string? key, string deviceId, DateOnly today, ECDsa publicKey)
    {
        var decoded = Decode(key, publicKey);
        if (!decoded.IsValid)
            return decoded;

        var payload = decoded.Payload!;
        if (!string.Equals(payload.DeviceId, NormalizeDeviceId(deviceId), StringComparison.Ordinal))
            return new(LicenseKeyError.WrongDevice, payload);

        if (payload.ExpiresOn is { } expires && today > expires)
            return new(LicenseKeyError.Expired, payload);

        return new(LicenseKeyError.None, payload);
    }

    /// <summary>Anahtarı çözer ve imzasını doğrular (cihaz ve süre kontrolü yapmaz).</summary>
    public static LicenseCheckResult Decode(string? key, ECDsa publicKey)
    {
        if (string.IsNullOrWhiteSpace(key))
            return new(LicenseKeyError.Empty, null);

        // Boşluk, satır sonu ve tireler yok sayılır (WhatsApp vb. anahtarı bölebilir)
        var compact = new string(key.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray()).ToUpperInvariant();

        if (compact.StartsWith(OldPrefix, StringComparison.Ordinal))
            return new(LicenseKeyError.OldFormat, null);
        if (!compact.StartsWith(Prefix, StringComparison.Ordinal))
            return new(LicenseKeyError.BadFormat, null);

        if (!Base32.TryDecode(compact[Prefix.Length..], out var bytes) || bytes.Length != PayloadLength + SignatureLength)
            return new(LicenseKeyError.BadFormat, null);

        var data = bytes.AsSpan(0, PayloadLength).ToArray();
        var signature = bytes.AsSpan(PayloadLength).ToArray();
        if (!publicKey.VerifyData(Signed(data), signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            return new(LicenseKeyError.BadSignature, null);

        var payload = DecodePayload(data);
        return payload == null ? new(LicenseKeyError.BadFormat, null) : new(LicenseKeyError.None, payload);
    }

    /// <summary>Cihaz kimliğini karşılaştırmaya uygun biçime getirir (büyük harf, boşluksuz).</summary>
    public static string NormalizeDeviceId(string deviceId) =>
        new string((deviceId ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray()).ToUpperInvariant();

    public static bool IsValidDeviceId(string deviceId)
    {
        var normalized = NormalizeDeviceId(deviceId);
        return normalized.Length == 16 && normalized.All(Uri.IsHexDigit);
    }

    private static byte[] EncodePayload(LicensePayload payload)
    {
        if (!IsValidDeviceId(payload.DeviceId))
            throw new ArgumentException("Cihaz kimliği 16 onaltılık karakter olmalı (ör. 079FDBAA48F568BF).", nameof(payload));
        if (payload.ExpiresOn is { } e && e < payload.IssuedOn)
            throw new ArgumentException("Bitiş tarihi veriliş tarihinden önce olamaz.", nameof(payload));

        var data = new byte[PayloadLength];
        data[0] = FormatVersion;
        Convert.FromHexString(NormalizeDeviceId(payload.DeviceId)).CopyTo(data, 1);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(9), ToDays(payload.IssuedOn));
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(11), payload.ExpiresOn is { } expires ? ToDays(expires) : (ushort)0);
        data[13] = 0; // bayraklar: ileride sürüm/özellik ayrımı için ayrıldı
        return data;
    }

    private static LicensePayload? DecodePayload(byte[] data)
    {
        if (data[0] != FormatVersion)
            return null;

        var deviceId = Convert.ToHexString(data, 1, 8);
        var issued = Epoch.AddDays(BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(9)));
        var expiresDays = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(11));
        return new LicensePayload(deviceId, issued, expiresDays == 0 ? null : Epoch.AddDays(expiresDays));
    }

    private static ushort ToDays(DateOnly date)
    {
        var days = date.DayNumber - Epoch.DayNumber;
        if (days is < 1 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(date), "Tarih 2024-01-02 ile 2203 arasında olmalı.");
        return (ushort)days;
    }

    private static byte[] Signed(byte[] data) => [.. Domain, .. data];

    private static IEnumerable<string> Chunk(string text, int size)
    {
        for (var i = 0; i < text.Length; i += size)
            yield return text.Substring(i, Math.Min(size, text.Length - i));
    }

    /// <summary>Crockford Base32: büyük/küçük harf duyarsız, karışan harfler (I, L, O, U) yok.</summary>
    private static class Base32
    {
        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        public static string Encode(byte[] data)
        {
            var sb = new StringBuilder((data.Length * 8 + 4) / 5);
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
            }
            if (bits > 0)
                sb.Append(Alphabet[(buffer << (5 - bits)) & 31]);
            return sb.ToString();
        }

        public static bool TryDecode(string text, out byte[] result)
        {
            var output = new List<byte>(text.Length * 5 / 8);
            int buffer = 0, bits = 0;
            foreach (var raw in text)
            {
                var c = raw switch { 'O' => '0', 'I' or 'L' => '1', _ => raw };
                var value = Alphabet.IndexOf(c);
                if (value < 0)
                {
                    result = [];
                    return false;
                }
                buffer = (buffer << 5) | value;
                bits += 5;
                if (bits >= 8)
                {
                    output.Add((byte)(buffer >> (bits - 8)));
                    bits -= 8;
                }
                buffer &= (1 << bits) - 1;
            }
            result = output.ToArray();
            return true;
        }
    }
}
