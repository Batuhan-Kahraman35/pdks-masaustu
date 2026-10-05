using System.Net;
using Microsoft.Extensions.Time.Testing;
using PdksMasaustu.Core.Api;
using PdksMasaustu.Core.Modeller;
using PdksMasaustu.Core.Takip;

namespace PdksMasaustu.Tests;

public class TakipKoordinatoruTestleri
{
    private static readonly DateTimeOffset Saat10 = new(2026, 10, 3, 7, 0, 0, TimeSpan.Zero);
    private static readonly CihazBilgisi Cihaz = new("PdksMasaustu test", "makine-1");

    private sealed class SahteGirdi : ISonGirdiKaynagi
    {
        public DateTimeOffset Zaman { get; set; }
        public DateTimeOffset SonGirdi() => Zaman;
    }

    private sealed class SahteApi : IPdksApi
    {
        public DurumYaniti Durum { get; set; } = DurumOlustur(PersonelDurumu.Iceride, HareketTipi.Giris, Saat10.AddHours(-2));
        public List<HareketIstegi> Gonderilenler { get; } = [];
        public Exception? HareketHatasi { get; set; }
        public Exception? DurumHatasi { get; set; }

        public Task<DurumYaniti> DurumGetirAsync(CancellationToken ct = default) =>
            DurumHatasi is null ? Task.FromResult(Durum) : Task.FromException<DurumYaniti>(DurumHatasi);

        public Task<HareketYaniti> HareketGonderAsync(HareketIstegi istek, CancellationToken ct = default)
        {
            Gonderilenler.Add(istek);
            if (HareketHatasi is not null) return Task.FromException<HareketYaniti>(HareketHatasi);
            return Task.FromResult(new HareketYaniti("ok", istek.Tip, istek.Zaman ?? Saat10, istek.Otomatik, PersonelDurumu.Molada));
        }

        public Task<GirisYaniti> GirisYapAsync(string kimlik, string sifre, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<MarkaAyarlari> AyarlarGetirAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EkipDurumYaniti> EkipDurumGetirAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<EkipGecmisKaydi>> EkipGecmisGetirAsync(int kullaniciId, DateOnly tarih, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<UzakCihaz> CihazEslestirAsync(string anakartUuid, string? biosSeri, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private static DurumYaniti DurumOlustur(PersonelDurumu durum, HareketTipi sonTip, DateTimeOffset sonZaman) =>
        new(durum, [new HareketKaydi(1, sonTip, sonZaman, false, "Merkez")], new PdksAyarlari(15, 120), false, sonZaman);

    [Fact]
    public async Task Hareketsiz_personel_icin_otomatik_mola_son_hareket_saatiyle_gonderilir()
    {
        var api = new SahteApi();
        var girdi = new SahteGirdi { Zaman = Saat10 };
        var saat = new FakeTimeProvider(Saat10.AddMinutes(16));
        using var koordinator = new TakipKoordinatoru(api, girdi, saat, Cihaz);
        var bildirimler = new List<TakipBildirimi>();
        koordinator.Bildirim += bildirimler.Add;

        await koordinator.TurAsync();

        var istek = Assert.Single(api.Gonderilenler);
        Assert.Equal(HareketTipi.MolaGiris, istek.Tip);
        Assert.True(istek.Otomatik);
        Assert.Equal(Saat10, istek.Zaman);
        Assert.Equal("makine-1", istek.CihazId);
        Assert.Equal(BildirimTipi.OtomatikMolaYapildi, Assert.Single(bildirimler).Tip);
    }

    [Fact]
    public async Task Sunucunun_reddettigi_otomatik_kayit_tekrar_gonderilmez()
    {
        var api = new SahteApi { HareketHatasi = new PdksApiHatasi(HttpStatusCode.BadRequest, "Otomatik kayit zamani izin verilen aralikta degil") };
        var saat = new FakeTimeProvider(Saat10.AddMinutes(16));
        using var koordinator = new TakipKoordinatoru(api, new SahteGirdi { Zaman = Saat10 }, saat, Cihaz);
        var bildirimler = new List<TakipBildirimi>();
        koordinator.Bildirim += bildirimler.Add;

        await koordinator.TurAsync();
        saat.Advance(TimeSpan.FromSeconds(15));
        await koordinator.TurAsync();

        Assert.Single(api.Gonderilenler);
        Assert.Equal(BildirimTipi.OtomatikKayitBasarisiz, Assert.Single(bildirimler).Tip);
    }

    [Fact]
    public async Task Ag_hatasinda_cevrimdisi_olur_ve_sonraki_turda_duzelir()
    {
        var api = new SahteApi { DurumHatasi = new HttpRequestException("ağ yok") };
        var saat = new FakeTimeProvider(Saat10);
        using var koordinator = new TakipKoordinatoru(api, new SahteGirdi { Zaman = Saat10 }, saat, Cihaz);

        await koordinator.TurAsync();
        Assert.True(koordinator.Cevrimdisi);

        api.DurumHatasi = null;
        saat.Advance(TimeSpan.FromSeconds(15));
        await koordinator.TurAsync();
        Assert.False(koordinator.Cevrimdisi);
        Assert.NotNull(koordinator.Durum);
    }

    [Fact]
    public async Task Suresi_dolan_oturumda_takip_durur()
    {
        var api = new SahteApi { DurumHatasi = new OturumGecersizHatasi("token süresi doldu") };
        using var koordinator = new TakipKoordinatoru(api, new SahteGirdi { Zaman = Saat10 }, new FakeTimeProvider(Saat10), Cihaz);
        var kapandi = false;
        koordinator.OturumKapandi += () => kapandi = true;

        await koordinator.TurAsync();

        Assert.True(kapandi);
        Assert.Null(koordinator.Durum);
    }

    [Fact]
    public async Task Elle_hareket_cihaz_bilgisiyle_ve_zamansiz_gonderilir()
    {
        var api = new SahteApi();
        using var koordinator = new TakipKoordinatoru(api, new SahteGirdi { Zaman = Saat10 }, new FakeTimeProvider(Saat10), Cihaz);

        await koordinator.HareketYapAsync(HareketTipi.MolaGiris);

        var istek = Assert.Single(api.Gonderilenler);
        Assert.False(istek.Otomatik);
        Assert.Null(istek.Zaman); // elle işlemde saati sunucu yazar
        Assert.Equal("PdksMasaustu test", istek.CihazModeli);
    }
}
