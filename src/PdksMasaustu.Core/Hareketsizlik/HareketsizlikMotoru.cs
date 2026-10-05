using PdksMasaustu.Core.Modeller;

namespace PdksMasaustu.Core.Hareketsizlik;

public enum KararTipi
{
    /// <summary>Yapılacak bir şey yok.</summary>
    Yok,

    /// <summary>İçerideyken eşik aşıldı: son hareket saatiyle mola başlatılır.</summary>
    OtomatikMola,

    /// <summary>Çıkış eşiği aşıldı: (açık mola kapatılıp) son hareket saatiyle çıkış yazılır.</summary>
    OtomatikCikis,

    /// <summary>Moladayken klavye/fare yeniden kullanıldı: personele "moladan dön" sorulur.</summary>
    DonusSor,

    /// <summary>Giriş yapılmamışken bilgisayar kullanılıyor: "mesaiye başla" hatırlatılır.</summary>
    GirisHatirlat,
}

/// <param name="Zaman">Otomatik kayıtta sunucuya gönderilecek saat (son hareket); diğerlerinde ilgili olayın saati.</param>
public sealed record Karar(KararTipi Tip, DateTimeOffset? Zaman = null)
{
    public static Karar Yok { get; } = new(KararTipi.Yok);
}

/// <summary>Personelin motorun karar vermek için ihtiyaç duyduğu anlık durumu.</summary>
public sealed record AnlikDurum(PersonelDurumu Durum, DateTimeOffset? SonKayitZamani, bool SonKayitOtomatik)
{
    public static AnlikDurum Bos { get; } = new(PersonelDurumu.Gelmedi, null, false);
}

/// <summary>
/// Klavye/fare hareketsizliğine göre ne yapılacağına karar verir.
/// Windows'a bağımlı değildir: şimdiki zaman ve son girdi zamanı dışarıdan verilir,
/// böylece tüm senaryolar birim testle doğrulanır.
///
/// Aynı soruyu tekrar tekrar sormamak için küçük bir hafıza tutar
/// (hangi mola için dönüş sorulduğu, son giriş hatırlatması).
/// </summary>
public sealed class HareketsizlikMotoru(PdksAyarlari ayarlar, TimeSpan hatirlatmaAraligi)
{
    /// <summary>Son girdi bu süreden yeniyse personel bilgisayar başında sayılır.</summary>
    public static readonly TimeSpan AktiflikEsigi = TimeSpan.FromSeconds(30);

    /// <summary>Elle başlatılan molada bu kadar hareketsizlik görülürse personel masadan ayrılmış sayılır.</summary>
    public static readonly TimeSpan UzaklasmaEsigi = TimeSpan.FromMinutes(2);

    private DateTimeOffset? _donusSorulanMola;
    private DateTimeOffset? _uzaklasilanMola;
    private DateTimeOffset? _sonHatirlatma;

    public PdksAyarlari Ayarlar { get; private set; } = ayarlar;

    /// <summary>Sunucudaki eşikler değiştiğinde çağrılır; yeni sürüm gerekmez.</summary>
    public void AyarlariGuncelle(PdksAyarlari yeni) => Ayarlar = yeni;

    public Karar Degerlendir(DateTimeOffset simdi, DateTimeOffset sonGirdi, AnlikDurum durum)
    {
        var hareketsizlik = simdi - sonGirdi;
        var bilgisayarBasinda = hareketsizlik < AktiflikEsigi;

        switch (durum.Durum)
        {
            case PersonelDurumu.Iceride:
                // Bilgisayar uyku/kilitte kaldıysa mola eşiği atlanıp doğrudan çıkış eşiğine varılmış olabilir.
                if (hareketsizlik >= Ayarlar.CikisEsigi) return Otomatik(KararTipi.OtomatikCikis, sonGirdi, durum);
                if (hareketsizlik >= Ayarlar.MolaEsigi) return Otomatik(KararTipi.OtomatikMola, sonGirdi, durum);
                return Karar.Yok;

            case PersonelDurumu.Molada:
                // Elle başlatılıp hiç dönülmeyen mola da çıkış eşiğinde kapanır.
                if (hareketsizlik >= Ayarlar.CikisEsigi) return Otomatik(KararTipi.OtomatikCikis, sonGirdi, durum);

                // Butona basıp bilgisayarı kullanmaya devam eden personele hemen "dönün" denmez;
                // soru ancak masadan ayrıldığı görüldükten sonra (otomatik molada zaten ayrılmıştır) sorulur.
                if (hareketsizlik >= UzaklasmaEsigi) _uzaklasilanMola = durum.SonKayitZamani;
                var uzaklasildi = durum.SonKayitOtomatik || _uzaklasilanMola == durum.SonKayitZamani;

                if (bilgisayarBasinda && uzaklasildi && sonGirdi > (durum.SonKayitZamani ?? DateTimeOffset.MinValue)
                                      && _donusSorulanMola != durum.SonKayitZamani)
                {
                    _donusSorulanMola = durum.SonKayitZamani;
                    return new Karar(KararTipi.DonusSor, durum.SonKayitZamani);
                }
                return Karar.Yok;

            case PersonelDurumu.Gelmedi:
            case PersonelDurumu.Cikti when durum.SonKayitOtomatik:
                // Elle çıkış yapıp bilgisayarı kullanmaya devam eden rahatsız edilmez;
                // yalnız hiç giriş yapmamış ya da sistemce çıkarılmış personel uyarılır.
                if (bilgisayarBasinda && (_sonHatirlatma is null || simdi - _sonHatirlatma >= hatirlatmaAraligi))
                {
                    _sonHatirlatma = simdi;
                    return new Karar(KararTipi.GirisHatirlat, simdi);
                }
                return Karar.Yok;

            default:
                return Karar.Yok;
        }
    }

    /// <summary>
    /// Otomatik kaydın saati son hareket saatidir; ancak günün son kaydından önceye
    /// düşemez (ör. mola butonuna basıp hiç dokunmadan ayrılan personel).
    /// </summary>
    private static Karar Otomatik(KararTipi tip, DateTimeOffset sonGirdi, AnlikDurum durum)
    {
        var zaman = durum.SonKayitZamani is { } son && son > sonGirdi ? son : sonGirdi;
        return new Karar(tip, zaman);
    }
}
