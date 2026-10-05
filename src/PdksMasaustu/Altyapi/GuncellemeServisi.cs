using Microsoft.Extensions.Options;
using PdksMasaustu.Core.Takip;
using Velopack;

namespace PdksMasaustu.Altyapi;

/// <summary>appsettings.json → "Guncelleme" bölümü.</summary>
public sealed class GuncellemeSecenekleri
{
    public const string Bolum = "Guncelleme";

    /// <summary>Velopack paketlerinin yayınlandığı klasörün adresi (releases.win.json burada).</summary>
    public string Adres { get; set; } = "";

    public int KontrolAraligiDk { get; set; } = 30;
}

/// <summary>
/// Yeni sürümü arka planda indirir ve personeli bölmeden kurar: bilgisayar
/// belirli bir süre kullanılmadığında uygulama sessizce yeniden başlar.
/// Takip durumu sunucuda, hareketsizlik bilgisi Windows'ta tutulduğu için
/// yeniden başlatmada veri kaybı olmaz.
/// </summary>
public sealed class GuncellemeServisi(IOptions<GuncellemeSecenekleri> secenekler, ISonGirdiKaynagi girdi, TimeProvider saat) : IDisposable
{
    /// <summary>Bilgisayar bu kadar süre kullanılmadıysa güncelleme sormadan kurulur.</summary>
    public static readonly TimeSpan SessizKurulumEsigi = TimeSpan.FromMinutes(5);

    private readonly CancellationTokenSource _iptal = new();
    private UpdateManager? _yonetici;
    private UpdateInfo? _hazir;

    /// <summary>İndirilmiş ve kurulmayı bekleyen sürüm (ör. "0.1.1"); yoksa null.</summary>
    public string? HazirSurum => _hazir?.TargetFullRelease.Version.ToString();

    /// <summary>Yeni sürüm indirilip kurulmaya hazır olduğunda (arka plan iş parçacığından) tetiklenir.</summary>
    public event Action<string>? GuncellemeHazir;

    /// <summary>Kurulumdan hemen önce: tepsi simgesi kaldırılsın, pencereler kapansın.</summary>
    public event Action? KurulumBasliyor;

    public void Baslat()
    {
        var s = secenekler.Value;
        if (string.IsNullOrWhiteSpace(s.Adres)) return;

        _yonetici = new UpdateManager(s.Adres);
        // Geliştirme ortamında (dotnet run) uygulama Velopack ile kurulu değildir.
        if (!_yonetici.IsInstalled) return;

        _ = DonguAsync(TimeSpan.FromMinutes(Math.Max(5, s.KontrolAraligiDk)), _iptal.Token);
    }

    private async Task DonguAsync(TimeSpan aralik, CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), saat, ct);
            using var zamanlayici = new PeriodicTimer(TimeSpan.FromMinutes(1), saat);
            var sonKontrol = DateTimeOffset.MinValue;
            do
            {
                if (_hazir is null && saat.GetUtcNow() - sonKontrol >= aralik)
                {
                    sonKontrol = saat.GetUtcNow();
                    await KontrolEtAsync(ct);
                }
                if (_hazir is not null && saat.GetUtcNow() - girdi.SonGirdi() >= SessizKurulumEsigi)
                {
                    Kur(arkaPlanda: true);
                }
            }
            while (await zamanlayici.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private async Task KontrolEtAsync(CancellationToken ct)
    {
        try
        {
            var bilgi = await _yonetici!.CheckForUpdatesAsync();
            if (bilgi is null) return;

            await _yonetici.DownloadUpdatesAsync(bilgi, cancelToken: ct);
            _hazir = bilgi;
            GuncellemeHazir?.Invoke(HazirSurum!);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // Sunucuya ulaşılamadı vb.: bir sonraki aralıkta yeniden denenir.
            Sistem.HataYaz(e);
        }
    }

    /// <summary>İndirilmiş güncellemeyi kurar ve uygulamayı yeniden başlatır.</summary>
    /// <param name="arkaPlanda">true ise yeniden açılışta pencere gösterilmez (yalnız tepsi).</param>
    public void Kur(bool arkaPlanda)
    {
        if (_yonetici is null || _hazir is null) return;
        KurulumBasliyor?.Invoke();
        _yonetici.ApplyUpdatesAndRestart(_hazir.TargetFullRelease, arkaPlanda ? ["--arka-plan"] : []);
    }

    public void Dispose()
    {
        _iptal.Cancel();
        _iptal.Dispose();
    }
}
