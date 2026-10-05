namespace PdksMasaustu.Core.Modeller;

/// <summary>Personel_GirisCikis.tip değerleri. JSON'da snake_case yazılır (mola_giris).</summary>
public enum HareketTipi
{
    Giris,
    Cikis,
    MolaGiris,
    MolaCikis,
}

/// <summary>Personelin günün son kaydına göre anlık durumu.</summary>
public enum PersonelDurumu
{
    Gelmedi,
    Iceride,
    Molada,
    Cikti,
}

public sealed record Personel(int Id, string Email, string AdSoyad, int? SubeId);

public sealed record GirisYaniti(string Token, Personel Personel);

public sealed record HareketKaydi(int Id, HareketTipi Tip, DateTimeOffset Zaman, bool Otomatik, string? Sube);

/// <summary>Sunucudaki tanim_pdks_ayarlari tablosundan gelen eşikler (dakika).</summary>
public sealed record PdksAyarlari(int HareketsizMolaDk, int HareketsizCikisDk)
{
    public static PdksAyarlari Varsayilan { get; } = new(15, 120);

    public TimeSpan MolaEsigi => TimeSpan.FromMinutes(HareketsizMolaDk);
    public TimeSpan CikisEsigi => TimeSpan.FromMinutes(HareketsizCikisDk);
}

public sealed record DurumYaniti(
    PersonelDurumu Durum,
    IReadOnlyList<HareketKaydi> Kayitlar,
    PdksAyarlari Ayarlar,
    bool EkipGorebilir,
    DateTimeOffset SunucuZamani)
{
    /// <summary>Günün son kaydı; hiç kayıt yoksa null.</summary>
    public HareketKaydi? SonKayit => Kayitlar.Count > 0 ? Kayitlar[^1] : null;
}

/// <summary>
/// POST masaustu/hareket gövdesi. Elle yapılan işlemde Zaman gönderilmez,
/// sunucu kendi saatini yazar; otomatik kayıtta Zaman son klavye/fare hareketidir.
/// </summary>
public sealed record HareketIstegi(
    HareketTipi Tip,
    bool Otomatik = false,
    DateTimeOffset? Zaman = null,
    string? CihazModeli = null,
    string? CihazId = null);

public sealed record HareketYaniti(string Mesaj, HareketTipi Tip, DateTimeOffset Zaman, bool Otomatik, PersonelDurumu Durum);

public sealed record EkipUyesi(
    int Id,
    string AdSoyad,
    string? Departman,
    string? Sube,
    HareketTipi? SonTip,
    DateTimeOffset? SonZaman,
    bool? SonOtomatik,
    DateTimeOffset? IlkGiris,
    PersonelDurumu Durum);

public sealed record EkipOzeti(int Iceride, int Molada, int Cikti, int Gelmedi);

public sealed record EkipDurumYaniti(EkipOzeti Ozet, IReadOnlyList<EkipUyesi> Personel);

public sealed record EkipGecmisKaydi(int Id, HareketTipi Tip, DateTimeOffset Zaman, bool Otomatik, string? Sube, int QrIle)
{
    /// <summary>Kayıt mobil uygulamada QR okutularak mı yapıldı (masaüstünde qr_kod boştur).</summary>
    public bool QrIleYapildi => QrIle == 1;
}

internal sealed record EkipGecmisYaniti(IReadOnlyList<EkipGecmisKaydi> Kayitlar);

/// <summary>GET ayarlar: portal marka ayarları (tanim_site_ayarlari). Adresler site köküne göre olabilir.</summary>
public sealed record MarkaAyarlari(string? Baslik, string? LogoUrl, string? FaviconUrl, string? SiteUrl);
