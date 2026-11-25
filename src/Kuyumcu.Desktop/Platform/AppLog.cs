using System.Text;

namespace Kuyumcu.Desktop.Platform;

/// <summary>
/// Basit hata günlüğü: %LOCALAPPDATA%\KuyumcuDefteri\logs\uygulama.log
/// Müşteride "açılmıyor / boş ekran" gibi sorunlarda destek için. 1 MB'ı geçince eski dosya .1 olarak saklanır.
/// Kişisel veri (müşteri adı, tutar) YAZILMAZ; sadece açılış bilgisi ve hata ayrıntıları.
/// </summary>
public static class AppLog
{
    private const long MaxBytes = 1024 * 1024;
    private static readonly object Sync = new();
    private static string? _path;

    public static string? FilePath => _path;

    public static void Initialize(string appDataDirectory)
    {
        try
        {
            var dir = Path.Combine(appDataDirectory, "logs");
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "uygulama.log");
            if (File.Exists(_path) && new FileInfo(_path).Length > MaxBytes)
                File.Move(_path, _path + ".1", overwrite: true);
        }
        catch
        {
            _path = null; // günlük yazılamıyorsa uygulama yine çalışsın
        }
    }

    public static void Info(string message) => Write("BİLGİ", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("HATA", ex == null ? message : $"{message}{Environment.NewLine}{ex}");

    private static void Write(string level, string message)
    {
        if (_path == null)
            return;
        try
        {
            lock (Sync)
                File.AppendAllText(_path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}", Encoding.UTF8);
        }
        catch
        {
            // yoksay
        }
    }
}
