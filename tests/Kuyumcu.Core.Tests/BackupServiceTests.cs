using Kuyumcu.Models;
using Kuyumcu.Platform;
using Kuyumcu.Services;
using SQLite;

namespace Kuyumcu.Tests;

// SQLiteAsyncConnection.ResetPool() bağlantı havuzunu genel olarak sıfırlar: veritabanı testleri aynı anda koşmasın
[Collection("Veritabanı")]
public sealed class BackupServiceTests : IAsyncLifetime
{
    private readonly TestEnvironment _env = new();
    private DatabaseService _db = null!;
    private BackupService _backup = null!;

    public async Task InitializeAsync()
    {
        _db = new DatabaseService(_env);
        await _db.UpdateDatabaseSchemaAsync();
        _backup = new BackupService(_db, _env);
    }

    public async Task DisposeAsync()
    {
        _backup.Dispose();
        SQLiteAsyncConnection.ResetPool();
        await Task.Delay(50);
        _env.Dispose();
    }

    [Fact]
    public async Task Kurulumu_tamamlanmamis_bos_veritabani_yedeklenmez()
    {
        Assert.Null(await _backup.BackupNowAsync());
        Assert.Empty(_backup.GetBackupsList());
    }

    [Fact]
    public async Task Kurulum_tamamlaninca_yedek_alinir_ve_ozeti_okunur()
    {
        await _db.SaveSettingsAsync(new AppSettings { BusinessName = "Çarşı Kuyumculuk", IsSetupCompleted = true });
        await _db.SaveCustomerAsync(new Customer { Name = "Ahmet Yılmaz" });

        var path = await _backup.BackupNowAsync();

        Assert.NotNull(path);
        var summary = await _db.ReadBackupSummaryAsync(path!);
        Assert.Equal(new BackupSummary("Çarşı Kuyumculuk", 1), summary);
    }

    [Fact]
    public async Task Eski_yedek_temizliginde_en_yeni_on_yedek_yasina_bakilmadan_kalir()
    {
        await _db.SaveSettingsAsync(new AppSettings { BusinessName = "X", IsSetupCompleted = true });
        Directory.CreateDirectory(_env.BackupDirectory);

        // 12 adet 60 günlük yedek (uygulama uzun süre açılmamış ya da yeniden kurulmuş gibi)
        var old = DateTime.Now.AddDays(-60);
        for (var i = 0; i < 12; i++)
        {
            var file = Path.Combine(_env.BackupDirectory, $"kuyumcu_backup_{old.AddMinutes(i):yyyyMMdd_HHmmss}.db3");
            await File.WriteAllTextAsync(file, "eski");
            File.SetCreationTime(file, old);
        }

        await _backup.BackupNowAsync(); // yeni yedek + temizlik

        var remaining = _backup.GetBackupsList();
        Assert.Equal(BackupService.MinimumToKeep, remaining.Count);
    }

    [Fact]
    public async Task Gecersiz_dosyanin_ozeti_null()
    {
        var path = Path.Combine(_env.AppDataDirectory, "bozuk.db3");
        await File.WriteAllTextAsync(path, "bu bir veritabanı değil");
        Assert.Null(await _db.ReadBackupSummaryAsync(path));
    }

    private sealed class TestEnvironment : IAppEnvironment, IDisposable
    {
        public TestEnvironment()
        {
            AppDataDirectory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "kuyumcu-test-" + Guid.NewGuid().ToString("N"))).FullName;
        }

        public string AppDataDirectory { get; }
        public string BackupDirectory => Path.Combine(AppDataDirectory, "backups");
        public string CacheDirectory => AppDataDirectory;
        public string PicturesDirectory => AppDataDirectory;
        public bool IsReadOnlyClient => false;
        public bool IsMobile => false;
        public string GetDeviceFingerprint() => "test";

        public void Dispose()
        {
            try { Directory.Delete(AppDataDirectory, true); } catch { /* SQLite dosyası kilitli kalabilir */ }
        }
    }
}
