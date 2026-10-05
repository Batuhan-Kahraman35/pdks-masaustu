using System.Text;
using PdksMasaustu.Core.Modeller;
using PdksMasaustu.Core.Oturum;

namespace PdksMasaustu.Tests;

public sealed class DosyaOturumKasasiTestleri : IDisposable
{
    private readonly string _klasor = Path.Combine(Path.GetTempPath(), "pdks-test-" + Guid.NewGuid().ToString("N"));
    private string Dosya => Path.Combine(_klasor, "oturum.bin");

    public void Dispose()
    {
        if (Directory.Exists(_klasor)) Directory.Delete(_klasor, recursive: true);
    }

    [Fact]
    public void Kaydedilen_oturum_yeni_ornekte_okunur_ve_dosyada_duz_metin_degildir()
    {
        var oturum = new OturumBilgisi("gizli-token", new Personel(3, "x@y.z", "Ali Veli", null));

        new DosyaOturumKasasi(Dosya, new TersCeviriciKoruyucu()).Kaydet(oturum);
        var okunan = new DosyaOturumKasasi(Dosya, new TersCeviriciKoruyucu()).Oku();

        Assert.Equal(oturum, okunan);
        Assert.DoesNotContain("gizli-token", Encoding.UTF8.GetString(File.ReadAllBytes(Dosya)));
    }

    [Fact]
    public void Bozuk_dosya_oturum_yok_sayilir()
    {
        Directory.CreateDirectory(_klasor);
        File.WriteAllBytes(Dosya, [1, 2, 3]);

        Assert.Null(new DosyaOturumKasasi(Dosya, new TersCeviriciKoruyucu()).Oku());
    }

    [Fact]
    public void Silinen_oturum_okunmaz()
    {
        var kasa = new DosyaOturumKasasi(Dosya, new TersCeviriciKoruyucu());
        kasa.Kaydet(new OturumBilgisi("t", new Personel(1, "a", "A", 1)));

        kasa.Sil();

        Assert.Null(kasa.Oku());
        Assert.False(File.Exists(Dosya));
    }
}
