using Kuyumcu.Platform;

namespace Kuyumcu.Desktop.Platform;

/// <summary>
/// Ürünün eski MAUI sürümünün (paket adı "com.companyname.kuyumcu") veritabanını bulur.
/// Eski sürüm MSIX olarak kurulduğu için veritabanı paketin sanal klasöründe durur:
/// %LOCALAPPDATA%\Packages\com.companyname.kuyumcu_*\LocalCache\Local\Kuyumcu.db3
/// </summary>
public sealed class WindowsLegacyDataLocator : ILegacyDataLocator
{
    private const string LegacyPackagePrefix = "com.companyname.kuyumcu_";
    private const string LegacyDatabaseName = "Kuyumcu.db3";

    private readonly IAppEnvironment _environment;

    public WindowsLegacyDataLocator(IAppEnvironment environment)
    {
        _environment = environment;
    }

    private string MarkerPath => Path.Combine(_environment.AppDataDirectory, "legacy-imported.flag");

    public void MarkImported() => File.WriteAllText(MarkerPath, DateTime.Now.ToString("O"));

    public string? FindLegacyDatabase()
    {
        if (File.Exists(MarkerPath))
            return null;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new List<string>();

        var packagesDir = Path.Combine(localAppData, "Packages");
        if (Directory.Exists(packagesDir))
        {
            foreach (var packageDir in Directory.EnumerateDirectories(packagesDir, LegacyPackagePrefix + "*"))
                candidates.Add(Path.Combine(packageDir, "LocalCache", "Local", LegacyDatabaseName));
        }

        // Paketlenmeden çalıştırılmış eski sürümler doğrudan %LOCALAPPDATA% altına yazıyordu
        candidates.Add(Path.Combine(localAppData, LegacyDatabaseName));

        return candidates
            .Where(File.Exists)
            .Where(path => !string.Equals(Path.GetFullPath(path), Path.GetFullPath(_environment.DatabasePath), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }
}
