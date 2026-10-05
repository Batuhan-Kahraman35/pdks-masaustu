using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PdksMasaustu.Altyapi;
using PdksMasaustu.Core.Modeller;
using PdksMasaustu.Core.Oturum;
using PdksMasaustu.Core.Takip;
using PdksMasaustu.Gorunumler;
using PdksMasaustu.GorunumModelleri;
using PdksMasaustu.Tepsi;

namespace PdksMasaustu;

/// <summary>
/// Uygulama akışı: oturum yoksa giriş ekranı → takip başlar → pencere kapanınca
/// tepside çalışmaya devam eder. Oturum süresi dolarsa giriş ekranına dönülür.
/// </summary>
public partial class App : Application
{
    private readonly string[] _args;
    private readonly TekOrnek _tekOrnek;
    private readonly UygulamaSecenekleri _secenekler;
    private IHost _host = null!;
    private bool _donusPenceresiAcik;

    internal App(string[] args, TekOrnek tekOrnek)
    {
        _args = args;
        _tekOrnek = tekOrnek;
        _secenekler = UygulamaSecenekleri.Oku(args);
    }

    private T Servis<T>() where T : notnull => _host.Services.GetRequiredService<T>();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        TurkceAyarla();
        DispatcherUnhandledException += Yakalanmamis;
        TaskScheduler.UnobservedTaskException += (_, a) => { Sistem.HataYaz(a.Exception); a.SetObserved(); };
        // Arka plan iş parçacığındaki hata süreci kapatır; en azından iz bırakır.
        AppDomain.CurrentDomain.UnhandledException += (_, a) => { if (a.ExceptionObject is Exception h) Sistem.HataYaz(h); };

        _host = HostKurulumu.Olustur(_args, _secenekler);
        if (!_secenekler.Demo) Sistem.OtomatikBaslatmayiKaydet();

        // Önce önbellekteki logo (anında), sonra sunucudaki güncel logo (arka planda).
        var marka = Servis<Marka>();
        marka.OnbellektenYukle();
        _ = marka.YenileAsync();

        var takip = Servis<TakipKoordinatoru>();
        takip.Bildirim += b => Dispatcher.InvokeAsync(() => BildirimGoster(b));
        takip.OturumKapandi += () => Dispatcher.InvokeAsync(() => OturumKapandi("Oturum süreniz doldu. Lütfen yeniden giriş yapın."));

        var tepsi = Servis<TepsiYoneticisi>();
        tepsi.AcIstendi += AnaPencereyiGoster;
        tepsi.OturumuKapatIstendi += () => OturumKapandi(null);
        tepsi.KapatIstendi += Kapat;
        tepsi.Olustur();

        _tekOrnek.Uyandirildi += () => Dispatcher.InvokeAsync(AnaPencereyiGoster);

        if (!_secenekler.Demo)
        {
            var guncelleme = Servis<GuncellemeServisi>();
            guncelleme.GuncellemeHazir += surum => Dispatcher.InvokeAsync(() =>
                tepsi.GuncellemeHazir(surum, () => guncelleme.Kur(arkaPlanda: false)));
            // Kurulum süreci kapatıp yeniden açar; tepside "hayalet" simge kalmasın.
            guncelleme.KurulumBasliyor += () => Dispatcher.Invoke(() =>
            {
                Servis<TakipKoordinatoru>().Durdur();
                tepsi.Dispose();
            });
            guncelleme.Baslat();
        }

        if (Servis<IOturumKasasi>().Oku() is null)
            GirisEkrani();
        else
            TakibiBaslat(pencereAc: !_secenekler.ArkaPlan);
    }

    /// <summary>Windows'un bölge ayarından bağımsız olarak tarih/saat biçimleri Türkçe olur (DatePicker dahil).</summary>
    private static void TurkceAyarla()
    {
        var tr = CultureInfo.GetCultureInfo("tr-TR");
        CultureInfo.DefaultThreadCurrentCulture = tr;
        CultureInfo.DefaultThreadCurrentUICulture = tr;
        Thread.CurrentThread.CurrentCulture = tr;
        Thread.CurrentThread.CurrentUICulture = tr;
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(tr.IetfLanguageTag)));
    }

    private void GirisEkrani()
    {
        var pencere = Servis<GirisPenceresi>();
        if (pencere.ShowDialog() == true)
            TakibiBaslat(pencereAc: true);
        else
            Kapat();
    }

    private void TakibiBaslat(bool pencereAc)
    {
        Servis<AnaGorunumModeli>().OturumuYukle();
        Servis<TakipKoordinatoru>().Baslat();
        _ = Servis<AnaGorunumModeli>().CihaziEslestirAsync();
        if (pencereAc) AnaPencereyiGoster();
    }

    private void AnaPencereyiGoster()
    {
        if (Servis<IOturumKasasi>().Oku() is null) return;   // giriş ekranı açıkken
        Servis<AnaPencere>().OneGetir();
    }

    private void OturumKapandi(string? bildirim)
    {
        Servis<TakipKoordinatoru>().Durdur();
        Servis<IOturumKasasi>().Sil();
        Servis<AnaPencere>().Hide();
        if (bildirim is not null) Servis<TepsiYoneticisi>().Bildir(Servis<Marka>().UygulamaAdi, bildirim);
        GirisEkrani();
    }

    private void BildirimGoster(TakipBildirimi b)
    {
        var tepsi = Servis<TepsiYoneticisi>();
        switch (b.Tip)
        {
            case BildirimTipi.DonusSor:
                DonusSor(b.Mesaj);
                break;
            case BildirimTipi.GirisHatirlat:
                tepsi.Bildir(b.Baslik, b.Mesaj, AnaPencereyiGoster);
                break;
            default:
                tepsi.Bildir(b.Baslik, b.Mesaj, AnaPencereyiGoster);
                break;
        }
    }

    /// <summary>Moladan dönüşte sağ altta soru penceresi; aynı anda yalnız bir tane açılır.</summary>
    private void DonusSor(string mesaj)
    {
        if (_donusPenceresiAcik) return;
        _donusPenceresiAcik = true;
        try
        {
            if (new DonusPenceresi(Servis<Marka>().UygulamaAdi, mesaj).ShowDialog() == true)
            {
                var model = Servis<AnaGorunumModeli>();
                if (model.MoladanDonCommand.CanExecute(null)) model.MoladanDonCommand.Execute(null);
            }
        }
        finally
        {
            _donusPenceresiAcik = false;
        }
    }

    private void Kapat()
    {
        var model = Servis<AnaGorunumModeli>();
        if (!_secenekler.Demo && model.Durum is PersonelDurumu.Iceride or PersonelDurumu.Molada)
        {
            var yanit = MessageBox.Show(
                "Uygulama kapatılırsa otomatik mola ve çıkış takibi durur.\nYine de kapatılsın mı?",
                Servis<Marka>().UygulamaAdi, MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (yanit != MessageBoxResult.Yes) return;
        }

        Servis<TakipKoordinatoru>().Dispose();
        Servis<TepsiYoneticisi>().Dispose();
        var pencere = Servis<AnaPencere>();
        pencere.KapanisaIzinVer = true;
        pencere.Close();
        _host.Dispose();
        Shutdown();
    }

    private void Yakalanmamis(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Sistem.HataYaz(e.Exception);
        e.Handled = true;
        MessageBox.Show($"Beklenmeyen bir hata oluştu ve kaydedildi:\n{e.Exception.Message}\n\nGünlük: {Yollar.HataGunlugu}",
            _host?.Services.GetService<Marka>()?.UygulamaAdi ?? "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
