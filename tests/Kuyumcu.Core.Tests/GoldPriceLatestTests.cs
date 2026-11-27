using Kuyumcu.Models;
using Kuyumcu.Platform;
using Kuyumcu.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kuyumcu.Tests;

/// <summary>Okutma ekranı fiyatı her seferinde çeker; internet yoksa son alınan fiyat "eski" işaretiyle gelir.</summary>
public class GoldPriceLatestTests : IDisposable
{
    private readonly FolderEnvironment _env = new();
    private readonly FakeSource _source = new();

    private GoldPriceExportService NewService() =>
        new(NullLogger<GoldPriceExportService>.Instance, _env, new NoShare(), _source);

    [Fact]
    public async Task Her_istekte_yeniden_cekilir()
    {
        var service = NewService();
        _source.Next = Prices(6000);
        var first = await service.GetLatestPricesAsync();
        _source.Next = Prices(6100);
        var second = await service.GetLatestPricesAsync();

        Assert.Equal(2, _source.Calls);
        Assert.False(second!.IsStale);
        Assert.Equal(6100, second.Prices[0].SellPrice);
        Assert.Equal(6000, first!.Prices[0].SellPrice);
    }

    [Fact]
    public async Task Internet_yoksa_son_fiyat_eski_isaretiyle_doner()
    {
        var service = NewService();
        _source.Next = Prices(6000);
        await service.GetLatestPricesAsync();

        _source.Next = null; // bağlantı yok
        var stale = await service.GetLatestPricesAsync();

        Assert.NotNull(stale);
        Assert.True(stale.IsStale);
        Assert.Equal(6000, stale.Prices[0].SellPrice);
        Assert.Contains("güncel olmayabilir", stale.Warning(DateTime.Now));
    }

    [Fact]
    public async Task Kaynak_hata_firlatsa_da_son_fiyat_doner()
    {
        var service = NewService();
        _source.Next = Prices(6000);
        await service.GetLatestPricesAsync();

        _source.Throw = true;
        var stale = await service.GetLatestPricesAsync();

        Assert.True(stale!.IsStale);
    }

    [Fact]
    public async Task Son_fiyat_uygulama_yeniden_acilinca_diskten_okunur()
    {
        _source.Next = Prices(6000);
        await NewService().GetLatestPricesAsync();

        _source.Next = null;
        var reopened = NewService();
        var stale = await reopened.GetLatestPricesAsync();

        Assert.True(stale!.IsStale);
        Assert.Equal("24 Ayar", stale.Prices[0].Type);
        Assert.Equal(6000, stale.Prices[0].SellPrice);
    }

    [Fact]
    public async Task Hic_fiyat_alinmamissa_null()
    {
        _source.Next = null;
        Assert.Null(await NewService().GetLatestPricesAsync());
    }

    [Fact]
    public async Task Az_once_alinamadiysa_zorlamadan_istekte_yeniden_beklenmez()
    {
        var service = NewService();
        _source.Next = Prices(6000);
        await service.GetLatestPricesAsync();
        _source.Next = null;
        await service.GetLatestPricesAsync(); // okutma: alınamadı
        var calls = _source.Calls;

        var forDialog = await service.GetLatestPricesAsync(forceRefresh: false); // hemen ardından satış penceresi

        Assert.Equal(calls, _source.Calls);
        Assert.True(forDialog!.IsStale);
    }

    [Fact]
    public void Zaman_metni()
    {
        var now = new DateTime(2026, 11, 27, 15, 0, 0);
        Assert.Equal("bugün 14:32", GoldPriceSnapshot.When(new DateTime(2026, 11, 27, 14, 32, 0), now));
        Assert.Equal("dün 09:10", GoldPriceSnapshot.When(new DateTime(2026, 11, 26, 9, 10, 0), now));
        Assert.Equal("24.11.2026 16:47", GoldPriceSnapshot.When(new DateTime(2026, 11, 24, 16, 47, 0), now));
        Assert.Null(new GoldPriceSnapshot([], now, false).Warning(now));
    }

    private static List<GoldPriceDto> Prices(decimal sell24) =>
        [new GoldPriceDto("24 Ayar", sell24 - 70, sell24), new GoldPriceDto("22 Ayar", sell24 - 550, sell24 - 70)];

    public void Dispose() => _env.Dispose();

    private sealed class FakeSource : IGoldPriceSource
    {
        public List<GoldPriceDto>? Next { get; set; }
        public bool Throw { get; set; }
        public int Calls { get; private set; }
        public string DisplayName => "test";

        public Task<List<GoldPriceDto>?> FetchAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Throw)
                throw new HttpRequestException("bağlantı yok");
            return Task.FromResult(Next);
        }
    }

    private sealed class NoShare : IShareService
    {
        public Task<bool> ShareFileAsync(string filePath, string title) => Task.FromResult(false);
    }

    private sealed class FolderEnvironment : IAppEnvironment, IDisposable
    {
        public string AppDataDirectory { get; } =
            Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "kuyumcu-fiyat-" + Guid.NewGuid().ToString("N"))).FullName;
        public string BackupDirectory => AppDataDirectory;
        public string CacheDirectory => AppDataDirectory;
        public string PicturesDirectory => AppDataDirectory;
        public bool IsMobile => false;
        public string GetDeviceFingerprint() => "test";

        public void Dispose()
        {
            try { Directory.Delete(AppDataDirectory, true); } catch { }
        }
    }
}
