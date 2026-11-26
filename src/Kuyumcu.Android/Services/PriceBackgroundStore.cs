using Kuyumcu.Services;

namespace Kuyumcu.Droid.Services;

/// <summary>Fiyat görselinin arka planı nereden geliyor?</summary>
public enum PriceBackgroundSource
{
    /// <summary>Bu telefonda seçilen resim.</summary>
    Phone,
    /// <summary>Bilgisayarda Ayarlar'dan yüklenen şablon (yedekle gelir).</summary>
    Desktop,
    /// <summary>Hiçbiri yok: sade varsayılan arka plan.</summary>
    Default,
}

/// <summary>
/// Altın fiyatı görselinin arka plan resmi. Kullanıcı telefonda bir resim seçer; her fiyat paylaşımında liste bu resmin
/// üzerine yazılır. Resim seçilirken yönü düzeltilip 1080 px genişliğe getirilir ve uygulamanın özel klasöründe tek
/// dosya olarak saklanır (yedek değişse de silinmez). Seçilmemişse bilgisayardaki şablon, o da yoksa sade arka plan.
/// </summary>
public sealed class PriceBackgroundStore
{
    private readonly string _path = Path.Combine(FileSystem.AppDataDirectory, "fiyat_arka_plan.jpg");
    private byte[]? _cached;

    public bool HasPhoneBackground => File.Exists(_path);

    private const string TextTopKey = "fiyat_yazi_konumu";
    public const int DefaultTextTopPercent = (int)(GoldPriceExportService.DefaultTextTop * 100);
    public const int MinTextTopPercent = 5;
    public const int MaxTextTopPercent = 80;

    /// <summary>
    /// Fiyat listesinin ilk satırı resmin üstünden yüzde kaç aşağıda başlasın (arka plan resmine göre ayarlanır).
    /// Bu telefonda saklanır; arka plan değişse de korunur.
    /// </summary>
    public int TextTopPercent
    {
        get => Math.Clamp(Preferences.Default.Get(TextTopKey, DefaultTextTopPercent), MinTextTopPercent, MaxTextTopPercent);
        set => Preferences.Default.Set(TextTopKey, Math.Clamp(value, MinTextTopPercent, MaxTextTopPercent));
    }

    /// <summary>Telefonda seçilen arka plan (hazırlanmış JPEG); yoksa null.</summary>
    public byte[]? GetPhoneBackground()
    {
        if (_cached != null)
            return _cached;
        return _cached = File.Exists(_path) ? File.ReadAllBytes(_path) : null;
    }

    /// <summary>Paylaşımda kullanılacak arka plan ve kaynağı: telefonda seçilen &gt; bilgisayardaki şablon &gt; varsayılan.</summary>
    public (byte[]? Image, PriceBackgroundSource Source) Resolve(byte[]? desktopTemplate)
    {
        if (GetPhoneBackground() is { Length: > 0 } phone)
            return (phone, PriceBackgroundSource.Phone);
        if (desktopTemplate is { Length: > 0 })
            return (desktopTemplate, PriceBackgroundSource.Desktop);
        return (null, PriceBackgroundSource.Default);
    }

    /// <summary>
    /// Galeriden/dosyalardan resim seçtirir ve arka plan olarak kaydeder.
    /// Seçilmezse false; resim okunamazsa <see cref="InvalidDataException"/>.
    /// </summary>
    public async Task<bool> PickAsync()
    {
        var picked = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Arka plan resmini seçin",
            FileTypes = FilePickerFileType.Images,
        });
        if (picked == null)
            return false;

        byte[] original;
        await using (var stream = await picked.OpenReadAsync())
        using (var memory = new MemoryStream())
        {
            await stream.CopyToAsync(memory);
            original = memory.ToArray();
        }

        var normalized = await Task.Run(() => GoldPriceExportService.NormalizeTemplate(original))
                         ?? throw new InvalidDataException("Seçilen dosya okunamadı. Lütfen JPG veya PNG bir resim seçin.");

        await File.WriteAllBytesAsync(_path, normalized);
        _cached = normalized;
        return true;
    }

    /// <summary>Telefonda seçilen arka planı kaldırır (bilgisayardaki şablona / varsayılana dönülür).</summary>
    public void Clear()
    {
        if (File.Exists(_path))
            File.Delete(_path);
        _cached = null;
    }
}
