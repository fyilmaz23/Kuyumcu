using Kuyumcu.Services;

namespace Kuyumcu.Tests;

public class GoldPriceSourceTests
{
    // elazigaltinfiyatlari.com ana sayfasındaki tablonun yapısı (24.11.2026'da alınan sayfadan sadeleştirildi)
    private const string SampleHtml = """
        <html><body>
        <div class="hero" id="fiyatlar"><div class="sar"><div>
          <div class="baslik"><h1>Elazığ Altın Fiyatları</h1>
            <span class="canli"><i></i>Son güncelleme <b class="tab">16:47</b> · 24 Kasım 2026 Salı</span></div>
          <div class="tahta"><table>
            <caption>BUGÜN · 24 KASIM</caption>
            <thead><tr><th>Ürün</th><th>Alış</th><th>Satış</th></tr></thead>
            <tbody>
            <tr><td class="ad"><a href="/24-ayar-altin-fiyati/">24 Ayar</a><small>has altın, gram</small></td><td><span class="alis tab">6.090</span></td><td><span class="satis tab">6.160 ₺</span></td></tr>
            <tr><td class="ad"><a href="/22-ayar-altin-fiyati/">22 Ayar</a><small>bilezik, gram</small></td><td><span class="alis tab">5.540</span></td><td><span class="satis tab">6.090 ₺</span></td></tr>
            <tr><td class="ad"><a href="/14-ayar-altin-fiyati/">14 Ayar</a><small>gram</small></td><td><div class="not">Marka ve özel takılar hariç, işçilik dahil</div></td><td><span class="satis tab">5.140 ₺</span></td></tr>
            <tr><td class="ad"><a href="/besli-altin-fiyati/">Beşli</a><small>adet</small></td><td><span class="alis tab">203.500</span></td><td><span class="satis tab">211.000 ₺</span></td></tr>
            <tr><td class="ad"><a href="/tam-altin-fiyati/">Tam (Ata)</a><small>adet</small></td><td><span class="alis tab">40.700</span></td><td><span class="satis tab">42.200 ₺</span></td></tr>
            <tr><td class="ad"><a href="/yarim-altin-fiyati/">Yarım</a><small>adet</small></td><td><span class="alis tab">19.900</span></td><td><span class="satis tab">20.900 ₺</span></td></tr>
            <tr><td class="ad"><a href="/ceyrek-altin-fiyati/">Çeyrek</a><small>adet</small></td><td><span class="alis tab">9.950</span></td><td><span class="satis tab">10.450 ₺</span></td></tr>
            <tr><td class="ad"><a href="/gram-altin-fiyati/">Gram Altın</a><small>24 ayar 1 gr, adet</small></td><td><span class="alis tab">6.100</span></td><td><span class="satis tab">6.400 ₺</span></td></tr>
            </tbody></table></div>
        </div></div></div>
        <table><thead><tr><th>Tarih</th><th>24 AYAR</th></tr></thead><tbody><tr><td>23 Kasım</td><td>6.220</td></tr></tbody></table>
        </body></html>
        """;

    [Fact]
    public void Gunluk_tablo_okunur()
    {
        var prices = ElazigAltinFiyatlariSource.Parse(SampleHtml, new DateTime(2026, 11, 24, 17, 0, 0));

        Assert.Equal(new[] { "24 Ayar", "22 Ayar", "14 Ayar", "Beşli", "Tam (Ata)", "Yarım", "Çeyrek", "Gram Altın" },
            prices.Select(p => p.Type));

        var has = prices[0];
        Assert.Equal(6090m, has.BuyPrice);
        Assert.Equal(6160m, has.SellPrice);
        Assert.Equal(new DateTime(2026, 11, 24, 16, 47, 0), has.UpdateTime);

        var k14 = prices.Single(p => p.Type == "14 Ayar");
        Assert.Equal(5140m, k14.SellPrice);
        Assert.Equal(5140m, k14.BuyPrice); // alış verilmiyor → satış

        Assert.Equal(211_000m, prices.Single(p => p.Type == "Beşli").SellPrice);
    }

    [Theory]
    [InlineData("6.160 ₺", 6160.0)]
    [InlineData("203.500", 203500.0)]
    [InlineData("5.140,50", 5140.50)]
    [InlineData("", null)]
    [InlineData("—", null)]
    public void Fiyat_metni_okunur(string text, double? expected)
    {
        Assert.Equal(expected.HasValue ? (decimal)expected.Value : null, ElazigAltinFiyatlariSource.ParsePrice(text));
    }

    [Fact]
    public void Tablo_yoksa_bos_liste()
    {
        Assert.Empty(ElazigAltinFiyatlariSource.Parse("<html><body>bakım</body></html>", DateTime.Now));
    }
}
