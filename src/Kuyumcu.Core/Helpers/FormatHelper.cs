namespace Kuyumcu.Helpers;

/// <summary>
/// Platformdan bağımsız biçimlendirme yardımcıları (arayüz ve görsel üretimi ortak kullanır).
/// </summary>
public static class FormatHelper
{
    public static string FormatPhoneNumber(this string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return phoneNumber;

        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (digits.Length != 10)
            return phoneNumber;

        return $"({digits.Substring(0, 3)}) {digits.Substring(3, 3)} {digits.Substring(6, 2)} {digits.Substring(8, 2)}";
    }

    public static string FormatPrice(decimal amount)
    {
        return amount % 1 == 0 ? $"{amount:N0}" : $"{amount:N2}";
    }
}
