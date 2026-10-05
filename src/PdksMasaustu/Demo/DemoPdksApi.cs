using System.Net;
using PdksMasaustu.Core.Api;
using PdksMasaustu.Core.Modeller;
using PdksMasaustu.Core.Oturum;

namespace PdksMasaustu.Demo;

/// <summary>
/// --demo ile açıldığında gerçek API yerine kullanılır: sunucu gerektirmeden
/// uygulamayı denemek ve ekran görüntüsü almak için uydurma bir ekip ve gün üretir.
/// Sunucudaki sıra kurallarının (giriş → mola → çıkış) aynısını uygular.
/// </summary>
internal sealed class DemoPdksApi(IOturumKasasi kasa, TimeProvider saat) : IPdksApi
{
    private static readonly PdksAyarlari Ayarlar = new(15, 120);
    private readonly Lock _kilit = new();
    private readonly List<HareketKaydi> _kayitlar = [];
    private int _sonId = 100;

    private static readonly (string Ad, string Departman, PersonelDurumu Durum, int GirisDk, int SonDk, bool Oto)[] Ekip =
    [
        ("Ahmet Yıldız", "Yazılım", PersonelDurumu.Iceride, 8 * 60 + 41, 8 * 60 + 41, false),
        ("Zeynep Kaya", "Yazılım", PersonelDurumu.Molada, 8 * 60 + 55, 12 * 60 + 5, true),
        ("Mehmet Demir", "Destek", PersonelDurumu.Iceride, 9 * 60 + 2, 10 * 60 + 30, false),
        ("Elif Şahin", "Destek", PersonelDurumu.Iceride, 8 * 60 + 30, 11 * 60 + 15, false),
        ("Can Öztürk", "Muhasebe", PersonelDurumu.Cikti, 8 * 60 + 15, 12 * 60 + 40, false),
        ("Ayşe Arslan", "Muhasebe", PersonelDurumu.Molada, 9 * 60 + 10, 12 * 60 + 20, false),
        ("Burak Çelik", "Satış", PersonelDurumu.Gelmedi, 0, 0, false),
        ("Selin Koç", "Satış", PersonelDurumu.Iceride, 8 * 60 + 48, 8 * 60 + 48, false),
        ("Emre Aydın", "Yazılım", PersonelDurumu.Cikti, 8 * 60 + 5, 11 * 60 + 58, true),
    ];

    private DateTimeOffset Bugun => new(saat.GetLocalNow().Date, saat.GetLocalNow().Offset);

    public Task<GirisYaniti> GirisYapAsync(string kimlik, string sifre, CancellationToken ct = default)
    {
        var yanit = new GirisYaniti("demo", new Personel(1, string.IsNullOrWhiteSpace(kimlik) ? "demo" : kimlik, "Deniz Yılmaz", 1));
        kasa.Kaydet(new OturumBilgisi(yanit.Token, yanit.Personel));

        lock (_kilit)
        {
            // Sabah girişi ve öğle molası hazır gelsin; ekran boş görünmesin.
            if (_kayitlar.Count == 0)
            {
                var simdi = saat.GetUtcNow();
                Ekle(HareketTipi.Giris, simdi.AddMinutes(-252), false);
                Ekle(HareketTipi.MolaGiris, simdi.AddMinutes(-101), true);
                Ekle(HareketTipi.MolaCikis, simdi.AddMinutes(-58), false);
            }
        }
        return Task.FromResult(yanit);
    }

    /// <summary>Demo modunda logo yoktur; uygulama genel ikonla görünür.</summary>
    public Task<MarkaAyarlari> AyarlarGetirAsync(CancellationToken ct = default) =>
        Task.FromResult(new MarkaAyarlari(null, null, null, null));

    public Task<DurumYaniti> DurumGetirAsync(CancellationToken ct = default)
    {
        lock (_kilit)
        {
            var kayitlar = _kayitlar.ToList();
            return Task.FromResult(new DurumYaniti(DurumBul(kayitlar.LastOrDefault()?.Tip), kayitlar, Ayarlar, true, saat.GetUtcNow()));
        }
    }

    public Task<HareketYaniti> HareketGonderAsync(HareketIstegi istek, CancellationToken ct = default)
    {
        lock (_kilit)
        {
            var son = _kayitlar.LastOrDefault()?.Tip;
            var acik = son is HareketTipi.Giris or HareketTipi.MolaCikis;
            var hata = istek.Tip switch
            {
                HareketTipi.Giris when son is not (null or HareketTipi.Cikis) => "Zaten giriş yapılmış",
                HareketTipi.MolaGiris when !acik => "Açık giriş kaydınız yok",
                HareketTipi.MolaCikis when son != HareketTipi.MolaGiris => "Devam eden mola yok",
                HareketTipi.Cikis when !acik && son != HareketTipi.MolaGiris => "Açık giriş kaydınız yok",
                _ => null,
            };
            if (hata is not null) throw new PdksApiHatasi(HttpStatusCode.BadRequest, hata);

            var zaman = istek.Otomatik && istek.Zaman is { } z ? z : saat.GetUtcNow();
            if (istek.Tip == HareketTipi.Cikis && son == HareketTipi.MolaGiris) Ekle(HareketTipi.MolaCikis, zaman, istek.Otomatik);
            var kayit = Ekle(istek.Tip, zaman, istek.Otomatik);
            return Task.FromResult(new HareketYaniti("Tamam", kayit.Tip, kayit.Zaman, kayit.Otomatik, DurumBul(kayit.Tip)));
        }
    }

    public Task<EkipDurumYaniti> EkipDurumGetirAsync(CancellationToken ct = default)
    {
        var uyeler = Ekip.Select((p, i) => new EkipUyesi(
            i + 10, p.Ad, p.Departman, "Merkez",
            p.Durum switch
            {
                PersonelDurumu.Iceride => p.SonDk == p.GirisDk ? HareketTipi.Giris : HareketTipi.MolaCikis,
                PersonelDurumu.Molada => HareketTipi.MolaGiris,
                PersonelDurumu.Cikti => HareketTipi.Cikis,
                _ => null,
            },
            p.Durum == PersonelDurumu.Gelmedi ? null : Bugun.AddMinutes(p.SonDk),
            p.Durum == PersonelDurumu.Gelmedi ? null : p.Oto,
            p.Durum == PersonelDurumu.Gelmedi ? null : Bugun.AddMinutes(p.GirisDk),
            p.Durum)).ToList();

        var ozet = new EkipOzeti(
            uyeler.Count(u => u.Durum == PersonelDurumu.Iceride), uyeler.Count(u => u.Durum == PersonelDurumu.Molada),
            uyeler.Count(u => u.Durum == PersonelDurumu.Cikti), uyeler.Count(u => u.Durum == PersonelDurumu.Gelmedi));
        return Task.FromResult(new EkipDurumYaniti(ozet, uyeler));
    }

    public Task<IReadOnlyList<EkipGecmisKaydi>> EkipGecmisGetirAsync(int kullaniciId, DateOnly tarih, CancellationToken ct = default)
    {
        var p = Ekip[(kullaniciId - 10) % Ekip.Length];
        var gun = new DateTimeOffset(tarih.ToDateTime(TimeOnly.MinValue), saat.GetLocalNow().Offset);
        var bugunMu = tarih == DateOnly.FromDateTime(saat.GetLocalNow().Date);
        List<EkipGecmisKaydi> kayitlar = [];
        if (bugunMu && p.Durum == PersonelDurumu.Gelmedi) return Task.FromResult<IReadOnlyList<EkipGecmisKaydi>>(kayitlar);

        kayitlar.Add(new(1, HareketTipi.Giris, gun.AddMinutes(p.GirisDk == 0 ? 540 : p.GirisDk), false, "Merkez", 1));
        kayitlar.Add(new(2, HareketTipi.MolaGiris, gun.AddHours(12), false, "Merkez", 0));
        kayitlar.Add(new(3, HareketTipi.MolaCikis, gun.AddHours(12).AddMinutes(45), false, "Merkez", 0));
        if (!bugunMu)
        {
            kayitlar.Add(new(4, HareketTipi.MolaGiris, gun.AddHours(15).AddMinutes(30), true, "Merkez", 0));
            kayitlar.Add(new(5, HareketTipi.MolaCikis, gun.AddHours(15).AddMinutes(52), false, "Merkez", 0));
            kayitlar.Add(new(6, HareketTipi.Cikis, gun.AddHours(18).AddMinutes(4), false, "Merkez", 1));
        }
        return Task.FromResult<IReadOnlyList<EkipGecmisKaydi>>(kayitlar);
    }

    private HareketKaydi Ekle(HareketTipi tip, DateTimeOffset zaman, bool otomatik)
    {
        var kayit = new HareketKaydi(++_sonId, tip, zaman, otomatik, "Merkez");
        _kayitlar.Add(kayit);
        return kayit;
    }

    private static PersonelDurumu DurumBul(HareketTipi? son) => son switch
    {
        null => PersonelDurumu.Gelmedi,
        HareketTipi.MolaGiris => PersonelDurumu.Molada,
        HareketTipi.Cikis => PersonelDurumu.Cikti,
        _ => PersonelDurumu.Iceride,
    };
}

/// <summary>Demo oturumu diske yazılmaz; her açılışta giriş ekranı gelir.</summary>
internal sealed class DemoOturumKasasi : IOturumKasasi
{
    private OturumBilgisi? _oturum;
    public OturumBilgisi? Oku() => _oturum;
    public void Kaydet(OturumBilgisi oturum) => _oturum = oturum;
    public void Sil() => _oturum = null;
}
