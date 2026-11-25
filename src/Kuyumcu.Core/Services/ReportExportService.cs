using System.Text;
using Kuyumcu.Platform;

namespace Kuyumcu.Services;

/// <summary>CSV üretimi (Excel'de açılır: UTF-8 BOM, ";" ayraç, Türkçe sayı biçimi).</summary>
public static class Csv
{
    public const char Separator = ';';

    public static string Build(IEnumerable<string> header, IEnumerable<IEnumerable<string?>> rows)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(Separator, header.Select(Escape))).Append("\r\n");
        foreach (var row in rows)
            sb.Append(string.Join(Separator, row.Select(Escape))).Append("\r\n");
        return sb.ToString();
    }

    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        // Excel'in formül olarak yorumlamasını engelle
        if (value[0] is '=' or '+' or '-' or '@' && !decimal.TryParse(value, out _))
            value = "'" + value;
        return value.IndexOfAny(new[] { Separator, '"', '\r', '\n' }) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}

/// <summary>Raporları dosyaya yazar: Belgeler\Kuyumcu Defteri Raporları. Windows'ta dosya Gezgin'de gösterilir.</summary>
public class ReportExportService
{
    private readonly IAppEnvironment _environment;
    private readonly IShareService _share;

    public ReportExportService(IAppEnvironment environment, IShareService share)
    {
        _environment = environment;
        _share = share;
    }

    public string ReportDirectory
    {
        get
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return string.IsNullOrEmpty(documents)
                ? Path.Combine(_environment.CacheDirectory, "Raporlar")
                : Path.Combine(documents, $"{ProductInfo.Name} Raporları");
        }
    }

    /// <summary>CSV dosyasını kaydeder, gösterir ve yolunu döndürür.</summary>
    public async Task<string> SaveCsvAsync(string fileNamePrefix, string csv)
    {
        Directory.CreateDirectory(ReportDirectory);
        var path = Path.Combine(ReportDirectory, $"{fileNamePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        await File.WriteAllTextAsync(path, csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        try { await _share.ShareFileAsync(path, fileNamePrefix); } catch { /* gösterilemezse de dosya kaydedildi */ }
        return path;
    }
}
