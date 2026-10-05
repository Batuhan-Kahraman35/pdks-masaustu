using PdksMasaustu.Core.Modeller;
using PdksMasaustu.Core.Takip;

namespace PdksMasaustu.Tests;

public class GunOzetiTestleri
{
    private static readonly DateTimeOffset Gun = new(2026, 10, 3, 0, 0, 0, TimeSpan.FromHours(3));
    private static HareketKaydi K(int id, HareketTipi tip, int saat, int dakika = 0) => new(id, tip, Gun.AddHours(saat).AddMinutes(dakika), false, null);

    [Fact]
    public void Tamamlanmis_gunde_mola_calismadan_dusulur()
    {
        var ozet = GunOzeti.Hesapla(
        [
            K(1, HareketTipi.Giris, 9), K(2, HareketTipi.MolaGiris, 12), K(3, HareketTipi.MolaCikis, 13),
            K(4, HareketTipi.Cikis, 18),
        ], Gun.AddHours(20));

        Assert.Equal(TimeSpan.FromHours(8), ozet.Calisma);
        Assert.Equal(TimeSpan.FromHours(1), ozet.Mola);
        Assert.Equal(Gun.AddHours(9), ozet.IlkGiris);
    }

    [Fact]
    public void Acik_mola_su_ana_kadar_sayilir()
    {
        var ozet = GunOzeti.Hesapla([K(1, HareketTipi.Giris, 9), K(2, HareketTipi.MolaGiris, 10)], Gun.AddHours(10).AddMinutes(20));

        Assert.Equal(TimeSpan.FromHours(1), ozet.Calisma);
        Assert.Equal(TimeSpan.FromMinutes(20), ozet.Mola);
    }

    [Fact]
    public void Moladayken_cikis_molayi_kapatir()
    {
        var ozet = GunOzeti.Hesapla(
            [K(1, HareketTipi.Giris, 9), K(2, HareketTipi.MolaGiris, 17), K(3, HareketTipi.MolaCikis, 17, 30), K(4, HareketTipi.Cikis, 17, 30)],
            Gun.AddHours(19));

        Assert.Equal(TimeSpan.FromHours(8), ozet.Calisma);
        Assert.Equal(TimeSpan.FromMinutes(30), ozet.Mola);
    }

    [Fact]
    public void Kayit_yoksa_sifir()
    {
        var ozet = GunOzeti.Hesapla([], Gun.AddHours(12));

        Assert.Equal(TimeSpan.Zero, ozet.Calisma);
        Assert.Null(ozet.IlkGiris);
    }
}
