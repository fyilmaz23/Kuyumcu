namespace Kuyumcu.Platform;

/// <summary>
/// Satış/bilgi iletişimi: kilitli modül ekranlarındaki "WhatsApp'tan bilgi al" düğmesi.
/// Numara boşsa düğme gösterilmez, sadece "yetkilinizle iletişime geçin" yazar.
/// </summary>
public static class SalesContact
{
    /// <summary>
    /// WhatsApp numarası: ülke koduyla, başında + ve 0 olmadan, sadece rakam (ör. 905321234567).
    /// YAYINDAN ÖNCE DOLDURULMALI.
    /// </summary>
    public const string WhatsAppNumber = "";

    public static bool HasWhatsApp => Digits(WhatsAppNumber).Length >= 10;

    /// <summary>Barkod modülü hakkında bilgi isteyen, cihaz kimliği önceden yazılmış WhatsApp mesajı bağlantısı.</summary>
    public static string BarcodeInquiryUrl(string deviceId) =>
        WhatsAppUrl(WhatsAppNumber, BarcodeInquiryMessage(deviceId));

    public static string BarcodeInquiryMessage(string deviceId) =>
        $"Merhaba, {ProductInfo.Name} barkod modülü hakkında bilgi almak istiyorum.\nCihaz kimliğim: {deviceId}";

    /// <summary>wa.me bağlantısı: WhatsApp kuruluysa uygulamada, değilse WhatsApp Web'de açılır.</summary>
    internal static string WhatsAppUrl(string number, string message) =>
        $"https://wa.me/{Digits(number)}?text={Uri.EscapeDataString(message)}";

    private static string Digits(string value) => new(value.Where(char.IsAsciiDigit).ToArray());
}
