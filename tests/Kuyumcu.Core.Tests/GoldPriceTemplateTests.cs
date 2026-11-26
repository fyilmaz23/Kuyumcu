using Kuyumcu.Services;
using SkiaSharp;

namespace Kuyumcu.Tests;

/// <summary>Fiyat görseli arka planının hazırlanması (telefonda seçilen resim / masaüstü şablonu).</summary>
public class GoldPriceTemplateTests
{
    private static byte[] Jpeg(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
            canvas.Clear(SKColors.Goldenrod);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }

    [Fact]
    public void Sablon_1080_genislikte_ise_boyutu_degismez()
    {
        // Mevcut şablon (carsi_kuyumculuk_template.jpg) 1080x1920: çıktısı eskisiyle aynı kalmalı
        using var bitmap = GoldPriceExportService.LoadTemplate(Jpeg(1080, 1920));
        Assert.NotNull(bitmap);
        Assert.Equal(1080, bitmap!.Width);
        Assert.Equal(1920, bitmap.Height);
    }

    [Fact]
    public void Buyuk_fotograf_oran_korunarak_1080_genislige_iner()
    {
        using var bitmap = GoldPriceExportService.LoadTemplate(Jpeg(3000, 4000));
        Assert.NotNull(bitmap);
        Assert.Equal(GoldPriceExportService.TemplateWidth, bitmap!.Width);
        Assert.Equal(1440, bitmap.Height);
    }

    [Fact]
    public void Kucuk_resim_1080_genislige_buyur()
    {
        using var bitmap = GoldPriceExportService.LoadTemplate(Jpeg(540, 960));
        Assert.Equal(1080, bitmap!.Width);
        Assert.Equal(1920, bitmap.Height);
    }

    [Fact]
    public void Resim_olmayan_veri_null_doner()
    {
        Assert.Null(GoldPriceExportService.LoadTemplate(new byte[] { 1, 2, 3, 4 }));
        Assert.Null(GoldPriceExportService.NormalizeTemplate(System.Text.Encoding.UTF8.GetBytes("resim değil")));
    }

    [Fact]
    public void Normalize_saklanabilir_jpeg_uretir()
    {
        var normalized = GoldPriceExportService.NormalizeTemplate(Jpeg(2000, 2000));
        Assert.NotNull(normalized);
        using var check = SKBitmap.Decode(normalized);
        Assert.Equal(1080, check.Width);
        Assert.Equal(1080, check.Height);
    }

    [Fact]
    public void Varsayilan_yazi_konumu_eskisiyle_ayni()
    {
        // Çarşı şablonu (1080x1920) ve sade arka plan (1080x1350): ilk satır %25'te kalmalı
        Assert.Equal(1920 * 0.25f, GoldPriceExportService.FirstRowY(1920, 8, 67 * 1.66f, GoldPriceExportService.DefaultTextTop));
        Assert.Equal(1350 * 0.25f, GoldPriceExportService.FirstRowY(1350, 8, 67 * 1.66f, GoldPriceExportService.DefaultTextTop));
    }

    [Fact]
    public void Yazi_asagi_alinabilir_ama_resimden_tasmaz()
    {
        const float row = 67 * 1.66f;
        Assert.Equal(1920 * 0.4f, GoldPriceExportService.FirstRowY(1920, 8, row, 0.4f));

        // %90 istense de son satır alt kenarın %3 yukarısında kalır
        var y = GoldPriceExportService.FirstRowY(1920, 8, row, 0.9f);
        Assert.Equal(1920 * 0.97f, y + 7 * row, 2);
    }
}
