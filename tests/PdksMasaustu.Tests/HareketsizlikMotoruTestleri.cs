using PdksMasaustu.Core.Hareketsizlik;
using PdksMasaustu.Core.Modeller;

namespace PdksMasaustu.Tests;

public class HareketsizlikMotoruTestleri
{
    private static readonly DateTimeOffset Saat10 = new(2026, 10, 3, 10, 0, 0, TimeSpan.FromHours(3));
    private static readonly TimeSpan Hatirlatma = TimeSpan.FromMinutes(30);

    private static HareketsizlikMotoru YeniMotor() => new(new PdksAyarlari(15, 120), Hatirlatma);

    private static AnlikDurum Iceride(DateTimeOffset girisZamani) => new(PersonelDurumu.Iceride, girisZamani, false);

    [Fact]
    public void Iceride_esik_altinda_karar_yok()
    {
        var karar = YeniMotor().Degerlendir(Saat10.AddMinutes(14), Saat10, Iceride(Saat10.AddHours(-1)));

        Assert.Equal(KararTipi.Yok, karar.Tip);
    }

    [Fact]
    public void Iceride_mola_esigi_asilinca_son_hareket_saatiyle_mola()
    {
        var karar = YeniMotor().Degerlendir(Saat10.AddMinutes(15), Saat10, Iceride(Saat10.AddHours(-1)));

        Assert.Equal(KararTipi.OtomatikMola, karar.Tip);
        Assert.Equal(Saat10, karar.Zaman); // tespit anı (10:15) değil, son hareket (10:00)
    }

    [Fact]
    public void Iceride_uykudan_donuste_cikis_esigi_asildiysa_dogrudan_cikis()
    {
        var karar = YeniMotor().Degerlendir(Saat10.AddMinutes(125), Saat10, Iceride(Saat10.AddHours(-1)));

        Assert.Equal(KararTipi.OtomatikCikis, karar.Tip);
        Assert.Equal(Saat10, karar.Zaman);
    }

    [Fact]
    public void Molada_cikis_esigi_asilinca_cikis()
    {
        var durum = new AnlikDurum(PersonelDurumu.Molada, Saat10, true);

        var karar = YeniMotor().Degerlendir(Saat10.AddMinutes(120), Saat10, durum);

        Assert.Equal(KararTipi.OtomatikCikis, karar.Tip);
    }

    [Fact]
    public void Otomatik_kayit_gunun_son_kaydindan_once_olamaz()
    {
        // Personel 10:05'te mola butonuna bastı, son klavye/fare hareketi 10:00'da kaldı.
        var durum = new AnlikDurum(PersonelDurumu.Molada, Saat10.AddMinutes(5), false);

        var karar = YeniMotor().Degerlendir(Saat10.AddMinutes(130), Saat10, durum);

        Assert.Equal(KararTipi.OtomatikCikis, karar.Tip);
        Assert.Equal(Saat10.AddMinutes(5), karar.Zaman);
    }

    [Fact]
    public void Moladan_donuste_bir_kez_sorulur()
    {
        var motor = YeniMotor();
        var molaBaslangici = Saat10;
        var durum = new AnlikDurum(PersonelDurumu.Molada, molaBaslangici, true);
        var donus = Saat10.AddMinutes(25);

        var ilk = motor.Degerlendir(donus, donus, durum);
        var ikinci = motor.Degerlendir(donus.AddSeconds(15), donus.AddSeconds(10), durum);

        Assert.Equal(KararTipi.DonusSor, ilk.Tip);
        Assert.Equal(molaBaslangici, ilk.Zaman);
        Assert.Equal(KararTipi.Yok, ikinci.Tip);
    }

    [Fact]
    public void Yeni_molada_donus_yeniden_sorulur()
    {
        var motor = YeniMotor();
        var birinci = new AnlikDurum(PersonelDurumu.Molada, Saat10, true);
        var ikinci = new AnlikDurum(PersonelDurumu.Molada, Saat10.AddHours(2), true);

        motor.Degerlendir(Saat10.AddMinutes(20), Saat10.AddMinutes(20), birinci);
        var karar = motor.Degerlendir(Saat10.AddHours(2).AddMinutes(20), Saat10.AddHours(2).AddMinutes(20), ikinci);

        Assert.Equal(KararTipi.DonusSor, karar.Tip);
    }

    [Fact]
    public void Elle_mola_sonrasi_masada_kalan_personele_sorulmaz()
    {
        var durum = new AnlikDurum(PersonelDurumu.Molada, Saat10, SonKayitOtomatik: false);

        var karar = YeniMotor().Degerlendir(Saat10.AddMinutes(1), Saat10.AddMinutes(1), durum);

        Assert.Equal(KararTipi.Yok, karar.Tip);
    }

    [Fact]
    public void Elle_mola_sonrasi_uzaklasip_donene_sorulur()
    {
        var motor = YeniMotor();
        var durum = new AnlikDurum(PersonelDurumu.Molada, Saat10, SonKayitOtomatik: false);

        motor.Degerlendir(Saat10.AddMinutes(5), Saat10, durum);            // 5 dk hareketsiz: masadan ayrıldı
        var karar = motor.Degerlendir(Saat10.AddMinutes(20), Saat10.AddMinutes(20), durum);

        Assert.Equal(KararTipi.DonusSor, karar.Tip);
    }

    [Fact]
    public void Molada_bilgisayar_basinda_degilken_sorulmaz()
    {
        var durum = new AnlikDurum(PersonelDurumu.Molada, Saat10, true);

        var karar = YeniMotor().Degerlendir(Saat10.AddMinutes(40), Saat10, durum);

        Assert.Equal(KararTipi.Yok, karar.Tip);
    }

    [Fact]
    public void Giris_yapilmamisken_kullanim_hatirlatilir_ve_aralik_beklenir()
    {
        var motor = YeniMotor();

        var ilk = motor.Degerlendir(Saat10, Saat10, AnlikDurum.Bos);
        var erken = motor.Degerlendir(Saat10.AddMinutes(10), Saat10.AddMinutes(10), AnlikDurum.Bos);
        var sonra = motor.Degerlendir(Saat10.AddMinutes(30), Saat10.AddMinutes(30), AnlikDurum.Bos);

        Assert.Equal(KararTipi.GirisHatirlat, ilk.Tip);
        Assert.Equal(KararTipi.Yok, erken.Tip);
        Assert.Equal(KararTipi.GirisHatirlat, sonra.Tip);
    }

    [Fact]
    public void Elle_cikis_yapan_rahatsiz_edilmez()
    {
        var durum = new AnlikDurum(PersonelDurumu.Cikti, Saat10, SonKayitOtomatik: false);

        var karar = YeniMotor().Degerlendir(Saat10.AddMinutes(5), Saat10.AddMinutes(5), durum);

        Assert.Equal(KararTipi.Yok, karar.Tip);
    }

    [Fact]
    public void Sistemce_cikarilan_personel_donunce_hatirlatilir()
    {
        var durum = new AnlikDurum(PersonelDurumu.Cikti, Saat10, SonKayitOtomatik: true);

        var karar = YeniMotor().Degerlendir(Saat10.AddHours(3), Saat10.AddHours(3), durum);

        Assert.Equal(KararTipi.GirisHatirlat, karar.Tip);
    }

    [Fact]
    public void Ayar_guncellenince_yeni_esik_gecerli_olur()
    {
        var motor = YeniMotor();
        motor.AyarlariGuncelle(new PdksAyarlari(30, 120));

        var karar = motor.Degerlendir(Saat10.AddMinutes(20), Saat10, Iceride(Saat10.AddHours(-1)));

        Assert.Equal(KararTipi.Yok, karar.Tip);
    }
}
