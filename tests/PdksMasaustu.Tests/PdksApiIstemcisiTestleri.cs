using System.Net;
using PdksMasaustu.Core.Api;
using PdksMasaustu.Core.Modeller;
using PdksMasaustu.Core.Oturum;

namespace PdksMasaustu.Tests;

public class PdksApiIstemcisiTestleri
{
    private static readonly OturumBilgisi AcikOturum = new("tok123", new Personel(7, "a@b.c", "Ayşe Yılmaz", 1));

    private static (PdksApiIstemcisi, SahteHttp, BellekOturumKasasi) Kur(HttpStatusCode durum, string govde, OturumBilgisi? oturum = null)
    {
        var sahte = new SahteHttp(durum, govde);
        var kasa = new BellekOturumKasasi(oturum);
        var http = new HttpClient(sahte) { BaseAddress = new Uri("https://ornek-sirket.com/pdks-api/") };
        return (new PdksApiIstemcisi(http, kasa), sahte, kasa);
    }

    [Fact]
    public async Task Giris_basarili_olunca_oturum_kaydedilir_ve_alt_dizin_korunur()
    {
        var (api, sahte, kasa) = Kur(HttpStatusCode.OK,
            """{"token":"yeni","personel":{"id":7,"email":"a@b.c","adSoyad":"Ayşe Yılmaz","subeId":1}}""");

        var yanit = await api.GirisYapAsync("a@b.c", "gizli");

        Assert.Equal("yeni", kasa.Oturum?.Token);
        Assert.Equal("Ayşe Yılmaz", yanit.Personel.AdSoyad);
        Assert.Equal("https://ornek-sirket.com/pdks-api/auth/login", sahte.SonIstek!.RequestUri!.ToString());
        Assert.Null(sahte.SonIstek.Headers.Authorization);
    }

    [Fact]
    public async Task Durum_snake_case_enumlari_ve_tarihleri_cozer()
    {
        var (api, sahte, _) = Kur(HttpStatusCode.OK, """
            {"durum":"molada","ekipGorebilir":true,"sunucuZamani":"2026-10-03T07:30:00.000Z",
             "ayarlar":{"hareketsizMolaDk":15,"hareketsizCikisDk":120},
             "kayitlar":[
               {"id":1,"tip":"giris","zaman":"2026-10-03T06:00:00.000Z","otomatik":false,"sube":"Merkez"},
               {"id":2,"tip":"mola_giris","zaman":"2026-10-03T07:10:00.000Z","otomatik":true,"sube":"Merkez"}]}
            """, AcikOturum);

        var durum = await api.DurumGetirAsync();

        Assert.Equal(PersonelDurumu.Molada, durum.Durum);
        Assert.Equal(HareketTipi.MolaGiris, durum.SonKayit!.Tip);
        Assert.True(durum.SonKayit.Otomatik);
        Assert.Equal(new DateTimeOffset(2026, 10, 3, 10, 10, 0, TimeSpan.FromHours(3)), durum.SonKayit.Zaman);
        Assert.Equal("Bearer tok123", sahte.SonIstek!.Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task Otomatik_hareket_tip_ve_zamani_dogru_bicimde_gonderir()
    {
        var (api, sahte, _) = Kur(HttpStatusCode.OK,
            """{"mesaj":"Mola baslatildi (otomatik)","tip":"mola_giris","zaman":"2026-10-03T07:00:00.000Z","otomatik":true,"durum":"molada"}""",
            AcikOturum);
        var sonHareket = new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.FromHours(3));

        await api.HareketGonderAsync(new HareketIstegi(HareketTipi.MolaGiris, Otomatik: true, Zaman: sonHareket));

        Assert.Contains("\"tip\":\"mola_giris\"", sahte.SonGovde);
        Assert.Contains("\"otomatik\":true", sahte.SonGovde);
        Assert.Contains("\"zaman\":\"2026-10-03T10:00:00+03:00\"", sahte.SonGovde);
        Assert.DoesNotContain("cihazId", sahte.SonGovde); // null alanlar gönderilmez
    }

    [Fact]
    public async Task Is_kurali_hatasi_sunucu_mesajiyla_firlatilir()
    {
        var (api, _, _) = Kur(HttpStatusCode.BadRequest, """{"hata":"Zaten giris yapilmis"}""", AcikOturum);

        var hata = await Assert.ThrowsAsync<PdksApiHatasi>(() => api.HareketGonderAsync(new HareketIstegi(HareketTipi.Giris)));

        Assert.Equal("Zaten giris yapilmis", hata.Message);
        Assert.Equal(HttpStatusCode.BadRequest, hata.DurumKodu);
    }

    [Fact]
    public async Task Json_olmayan_hata_govdesinde_durum_kodu_yazilir()
    {
        var (api, _, _) = Kur(HttpStatusCode.BadGateway, "<html>502</html>", AcikOturum);

        var hata = await Assert.ThrowsAsync<PdksApiHatasi>(() => api.DurumGetirAsync());

        Assert.Equal("Sunucu hatası (502)", hata.Message);
    }

    [Fact]
    public async Task Suresi_dolan_token_silinir()
    {
        var (api, _, kasa) = Kur(HttpStatusCode.Unauthorized, """{"hata":"Gecersiz veya suresi dolmus token"}""", AcikOturum);

        await Assert.ThrowsAsync<OturumGecersizHatasi>(() => api.DurumGetirAsync());

        Assert.Null(kasa.Oturum);
    }

    [Fact]
    public async Task Oturum_yokken_istek_gonderilmez()
    {
        var (api, sahte, _) = Kur(HttpStatusCode.OK, "{}");

        await Assert.ThrowsAsync<OturumGecersizHatasi>(() => api.EkipDurumGetirAsync());

        Assert.Null(sahte.SonIstek);
    }

    [Fact]
    public async Task Ekip_gecmisi_tarih_ve_kullaniciyi_sorguya_yazar()
    {
        var (api, sahte, _) = Kur(HttpStatusCode.OK,
            """{"kayitlar":[{"id":5,"tip":"cikis","zaman":"2026-10-02T15:00:00.000Z","otomatik":true,"sube":null,"qrIle":0}]}""",
            AcikOturum);

        var kayitlar = await api.EkipGecmisGetirAsync(42, new DateOnly(2026, 10, 2));

        Assert.EndsWith("masaustu/ekip/gecmis?kullaniciId=42&tarih=2026-10-02", sahte.SonIstek!.RequestUri!.ToString());
        Assert.False(Assert.Single(kayitlar).QrIleYapildi);
    }
}
