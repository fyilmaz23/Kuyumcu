using System.Globalization;
using Kuyumcu.Helpers;
using Kuyumcu.Models;

namespace Kuyumcu.Droid.Services;

/// <summary>Ekrandaki tutar ve tarih biçimleri (masaüstüyle aynı: 1.250 ₺, 12,5 gr).</summary>
public static class Fmt
{
    private static readonly CultureInfo Tr = new("tr-TR");

    public static string Amount(decimal value, CurrencyType currency) =>
        $"{FormatHelper.FormatPrice(value)} {currency.GetSymbol()}";

    public static string DateTime(System.DateTime value) => value.ToString("dd.MM.yyyy HH:mm", Tr);

    public static string Date(System.DateTime value) => value.ToString("dd.MM.yyyy", Tr);

    /// <summary>"24 Kasım 2026, 17:42"</summary>
    public static string LongDateTime(DateTimeOffset value) => value.ToLocalTime().ToString("d MMMM yyyy, HH:mm", Tr);

    /// <summary>"az önce", "12 dk önce", "dün 14:30" ...</summary>
    public static string Ago(DateTimeOffset? value) => TimeAgo.Format(value?.UtcDateTime);

    public static string Size(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0} KB",
        _ => $"{bytes / (1024.0 * 1024.0):0.0} MB",
    };
}
