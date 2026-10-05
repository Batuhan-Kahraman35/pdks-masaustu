using PdksMasaustu.Core.Api;
using PdksMasaustu.Core.Hareketsizlik;
using PdksMasaustu.Core.Modeller;

namespace PdksMasaustu.Core.Takip;

/// <summary>Son klavye/fare hareketinin zamanı (Windows'ta GetLastInputInfo).</summary>
public interface ISonGirdiKaynagi
{
    DateTimeOffset SonGirdi();
}

/// <summary>Kayıtlara yazılan cihaz bilgisi: Model = uygulama/sürüm/işletim sistemi, Id = makine kimliği.</summary>
public sealed record CihazBilgisi(string Model, string Id);

public enum BildirimTipi
{
    OtomatikMolaYapildi,
    OtomatikCikisYapildi,
    DonusSor,
    GirisHatirlat,
    OtomatikKayitBasarisiz,
}

public sealed record TakipBildirimi(BildirimTipi Tip, string Baslik, string Mesaj);

/// <summary>
/// Uygulamanın kalbi: durumu sunucudan tazeler, hareketsizlik motorunu belirli
/// aralıklarla çalıştırır ve kararları uygular. Arayüzden bağımsızdır; olaylar
/// arka plan iş parçacığından tetiklenir, arayüz kendi iş parçacığına aktarır.
/// </summary>
public sealed class TakipKoordinatoru(IPdksApi api, ISonGirdiKaynagi girdi, TimeProvider saat, CihazBilgisi cihaz) : IDisposable
{
    public static readonly TimeSpan KontrolAraligi = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan YenilemeAraligi = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan HatirlatmaAraligi = TimeSpan.FromMinutes(30);

    private readonly SemaphoreSlim _kilit = new(1, 1);
    private readonly HareketsizlikMotoru _motor = new(PdksAyarlari.Varsayilan, HatirlatmaAraligi);
    private DateTimeOffset _sonYenileme = DateTimeOffset.MinValue;
    private string? _bastirilanKarar;
    private CancellationTokenSource? _iptal;

    public DurumYaniti? Durum { get; private set; }

    /// <summary>Sunucuya son istek ulaşamadıysa true; bir sonraki başarılı istekte düzelir.</summary>
    public bool Cevrimdisi { get; private set; }

    public event Action? DurumDegisti;
    public event Action<TakipBildirimi>? Bildirim;
    public event Action? OturumKapandi;

    public void Baslat()
    {
        if (_iptal is not null) return;
        _iptal = new CancellationTokenSource();
        _ = DonguAsync(_iptal.Token);
    }

    public void Durdur()
    {
        // Dispose edilmez: döngü iptal edilen token ile çıkarken hâlâ ona erişiyor olabilir.
        _iptal?.Cancel();
        _iptal = null;
        Durum = null;
    }

    /// <summary>Personelin butonla yaptığı hareket. İş kuralı hatası PdksApiHatasi olarak arayüze döner.</summary>
    public async Task<HareketYaniti> HareketYapAsync(HareketTipi tip, CancellationToken ct = default)
    {
        await _kilit.WaitAsync(ct);
        try
        {
            var yanit = await api.HareketGonderAsync(new HareketIstegi(tip, CihazModeli: cihaz.Model, CihazId: cihaz.Id), ct);
            await YenileIcAsync(ct);
            return yanit;
        }
        catch (OturumGecersizHatasi)
        {
            OturumuKapat();
            throw;
        }
        finally
        {
            _kilit.Release();
        }
    }

    public async Task YenileAsync(CancellationToken ct = default)
    {
        await _kilit.WaitAsync(ct);
        try { await YenileIcAsync(ct); }
        finally { _kilit.Release(); }
    }

    private async Task DonguAsync(CancellationToken ct)
    {
        try
        {
            using var zamanlayici = new PeriodicTimer(KontrolAraligi, saat);
            do
            {
                await TurAsync(ct);
            }
            while (await zamanlayici.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Durdur() çağrıldı.
        }
    }

    /// <summary>Tek kontrol turu. Testler zamanlayıcıyı beklemeden doğrudan çağırır.</summary>
    internal async Task TurAsync(CancellationToken ct = default)
    {
        await _kilit.WaitAsync(ct);
        try
        {
            if (Durum is null || saat.GetUtcNow() - _sonYenileme >= YenilemeAraligi)
                await YenileIcAsync(ct);
            if (Durum is null) return;

            var son = Durum.SonKayit;
            var anlik = new AnlikDurum(Durum.Durum, son?.Zaman, son?.Otomatik ?? false);
            var karar = _motor.Degerlendir(saat.GetUtcNow(), girdi.SonGirdi(), anlik);
            await KararUygulaAsync(karar, anlik, ct);
        }
        catch (OturumGecersizHatasi)
        {
            OturumuKapat();
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Ağ kesintisi / zaman aşımı / sunucu hatası: döngü sürer, bir sonraki turda yeniden denenir.
            Cevrimdisi = true;
            DurumDegisti?.Invoke();
        }
        finally
        {
            _kilit.Release();
        }
    }

    private async Task KararUygulaAsync(Karar karar, AnlikDurum anlik, CancellationToken ct)
    {
        switch (karar.Tip)
        {
            case KararTipi.OtomatikMola:
            case KararTipi.OtomatikCikis:
                await OtomatikKayitAsync(karar, anlik, ct);
                break;

            case KararTipi.DonusSor:
                Bildirim?.Invoke(new TakipBildirimi(BildirimTipi.DonusSor, "Moladasınız",
                    $"{Yerel(karar.Zaman)} itibarıyla moladasınız. Mesaiye dönmek ister misiniz?"));
                break;

            case KararTipi.GirisHatirlat:
                Bildirim?.Invoke(new TakipBildirimi(BildirimTipi.GirisHatirlat, "Mesaiye başlamadınız",
                    "Bilgisayarı kullanıyorsunuz ancak giriş kaydınız yok. Giriş yapmak için tıklayın."));
                break;
        }
    }

    private async Task OtomatikKayitAsync(Karar karar, AnlikDurum anlik, CancellationToken ct)
    {
        // Sunucunun reddettiği karar, durum değişene kadar tekrar gönderilmez.
        var anahtar = $"{karar.Tip}|{anlik.SonKayitZamani:O}";
        if (anahtar == _bastirilanKarar) return;

        var mola = karar.Tip == KararTipi.OtomatikMola;
        var istek = new HareketIstegi(mola ? HareketTipi.MolaGiris : HareketTipi.Cikis,
            Otomatik: true, Zaman: karar.Zaman, CihazModeli: cihaz.Model, CihazId: cihaz.Id);
        try
        {
            await api.HareketGonderAsync(istek, ct);
            Bildirim?.Invoke(mola
                ? new TakipBildirimi(BildirimTipi.OtomatikMolaYapildi, "Molaya alındınız",
                    $"{_motor.Ayarlar.HareketsizMolaDk} dakikadır işlem yapılmadığı için {Yerel(karar.Zaman)} itibarıyla mola başlatıldı.")
                : new TakipBildirimi(BildirimTipi.OtomatikCikisYapildi, "Çıkış yapıldı",
                    $"Uzun süre işlem yapılmadığı için {Yerel(karar.Zaman)} itibarıyla çıkışınız yazıldı."));
        }
        catch (PdksApiHatasi h) when (h is not OturumGecersizHatasi && (int)h.DurumKodu is >= 400 and < 500)
        {
            _bastirilanKarar = anahtar;
            Bildirim?.Invoke(new TakipBildirimi(BildirimTipi.OtomatikKayitBasarisiz, "Otomatik kayıt yapılamadı", h.Message));
        }
        await YenileIcAsync(ct);
    }

    private async Task YenileIcAsync(CancellationToken ct)
    {
        Durum = await api.DurumGetirAsync(ct);
        _motor.AyarlariGuncelle(Durum.Ayarlar);
        _sonYenileme = saat.GetUtcNow();
        Cevrimdisi = false;
        DurumDegisti?.Invoke();
    }

    private void OturumuKapat()
    {
        Durdur();
        OturumKapandi?.Invoke();
    }

    private static string Yerel(DateTimeOffset? zaman) => zaman?.ToLocalTime().ToString("HH:mm") ?? "-";

    public void Dispose()
    {
        Durdur();
        _kilit.Dispose();
    }
}
