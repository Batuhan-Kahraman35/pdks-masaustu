using System.Collections.ObjectModel;
using System.Net;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdksMasaustu.Altyapi;
using PdksMasaustu.Core.Api;
using PdksMasaustu.Core.Modeller;
using PdksMasaustu.Core.Oturum;
using PdksMasaustu.Core.Takip;
using PdksMasaustu.Gorunumler;

namespace PdksMasaustu.GorunumModelleri;

/// <summary>Ana pencerenin "Bugün" bölümü ve tepsi menüsü bu modeli paylaşır.</summary>
public sealed partial class AnaGorunumModeli : ObservableObject
{
    private readonly TakipKoordinatoru _takip;
    private readonly IOturumKasasi _kasa;
    private readonly IPdksApi _api;
    private readonly TimeProvider _saat;
    private readonly DispatcherTimer _sureZamanlayici;

    public AnaGorunumModeli(TakipKoordinatoru takip, IOturumKasasi kasa, IPdksApi api, TimeProvider saat, EkipGorunumModeli ekip, UygulamaSecenekleri secenekler, Marka marka)
    {
        Marka = marka;
        _takip = takip;
        _kasa = kasa;
        _api = api;
        _saat = saat;
        Ekip = ekip;
        DemoModu = secenekler.Demo;

        _takip.DurumDegisti += () => Application.Current.Dispatcher.InvokeAsync(Guncelle);

        // Süreler sunucu yenilemesini beklemeden dakika dakika ilerlesin.
        _sureZamanlayici = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _sureZamanlayici.Tick += (_, _) => SureleriGuncelle();
        _sureZamanlayici.Start();
    }

    public Marka Marka { get; }
    public EkipGorunumModeli Ekip { get; }
    public ObservableCollection<HareketKaydi> Kayitlar { get; } = [];
    public string Surum { get; } = "v" + Sistem.Surum;
    public bool DemoModu { get; }

    [ObservableProperty] private string _personelAdi = "";
    [ObservableProperty] private string _durumBasligi = "Yükleniyor…";
    [ObservableProperty] private string _durumAciklama = "";
    [ObservableProperty] private string _calismaSuresi = "0 dk";
    [ObservableProperty] private string _molaSuresi = "0 dk";
    [ObservableProperty] private string _ilkGiris = "–";
    [ObservableProperty] private bool _cevrimdisi;
    [ObservableProperty] private string? _mesaj;
    [ObservableProperty] private bool _mesajHata;
    [ObservableProperty] private UzakCihaz? _cihaz;
    [ObservableProperty] private string? _cihazUyari;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GirisYapCommand), nameof(MolayaCikCommand), nameof(MoladanDonCommand), nameof(CikisYapCommand))]
    private PersonelDurumu? _durum;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GirisYapCommand), nameof(MolayaCikCommand), nameof(MoladanDonCommand), nameof(CikisYapCommand))]
    private bool _mesgul;

    private bool Bosta => !Mesgul && Durum is not null;
    private bool GirisYapilabilir => Bosta && Durum is PersonelDurumu.Gelmedi or PersonelDurumu.Cikti;
    private bool MolayaCikilabilir => Bosta && Durum == PersonelDurumu.Iceride;
    private bool MoladanDonulebilir => Bosta && Durum == PersonelDurumu.Molada;
    private bool CikisYapilabilir => Bosta && Durum is PersonelDurumu.Iceride or PersonelDurumu.Molada;

    [RelayCommand(CanExecute = nameof(GirisYapilabilir))]
    private Task GirisYap() => HareketAsync(HareketTipi.Giris);

    [RelayCommand(CanExecute = nameof(MolayaCikilabilir))]
    private Task MolayaCik() => HareketAsync(HareketTipi.MolaGiris);

    [RelayCommand(CanExecute = nameof(MoladanDonulebilir))]
    private Task MoladanDon() => HareketAsync(HareketTipi.MolaCikis);

    [RelayCommand(CanExecute = nameof(CikisYapilabilir))]
    private Task CikisYap() => HareketAsync(HareketTipi.Cikis);

    [RelayCommand]
    private async Task Yenile()
    {
        try
        {
            await _takip.YenileAsync();
        }
        catch (Exception e) when (e is not OturumGecersizHatasi)
        {
            MesajGoster("Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edin.", hata: true);
        }
    }

    /// <summary>Oturum açıldığında ya da kapandığında personel bilgisini tazeler.</summary>
    public void OturumuYukle()
    {
        PersonelAdi = _kasa.Oku()?.Personel.AdSoyad ?? "";
        Durum = null;
        Kayitlar.Clear();
        Mesaj = null;
        DurumBasligi = "Yükleniyor…";
        DurumAciklama = "";
        Cihaz = null;
        CihazUyari = null;
    }

    /// <summary>
    /// Bu bilgisayarı uzak yönetimdeki kaydıyla eşleştirir: sunucu cihaz açıklamasına
    /// personelin adını yazar ve cihaz bilgisini döner. Hata takibi etkilemez.
    /// </summary>
    public async Task CihaziEslestirAsync()
    {
        var (uuid, seri) = await Task.Run(Sistem.DonanimKimligiOku);   // WMI yavaş olabilir
        if (string.IsNullOrWhiteSpace(uuid))
        {
            CihazUyari = "Bilgisayar kimliği okunamadı ya da geçerli değil.";
            return;
        }

        try
        {
            Cihaz = await _api.CihazEslestirAsync(uuid, seri);
            CihazUyari = null;
        }
        catch (PdksApiHatasi h) when (h.DurumKodu == HttpStatusCode.NotFound)
        {
            Cihaz = null;
            CihazUyari = "Bu bilgisayarda uzak yönetim ajanı kurulu değil.";
        }
        catch (PdksApiHatasi h) when (h.DurumKodu == HttpStatusCode.BadRequest)
        {
            Cihaz = null;
            CihazUyari = "Bilgisayar kimliği uzak yönetimde tanınmadı.";
        }
        catch (PdksApiHatasi h) when (h.DurumKodu == HttpStatusCode.ServiceUnavailable)
        {
            Cihaz = null;
            CihazUyari = "Uzak yönetim bağlantısı henüz tanımlanmamış.";
        }
        catch (Exception e) when (e is not OturumGecersizHatasi)
        {
            // Bir sonraki açılışta yeniden denenir.
            Sistem.HataYaz(e);
        }
    }

    private async Task HareketAsync(HareketTipi tip)
    {
        Mesgul = true;
        Mesaj = null;
        try
        {
            var yanit = await _takip.HareketYapAsync(tip);
            MesajGoster($"{Gorunum.TipMetni(tip)} kaydedildi · {Gorunum.Saat(yanit.Zaman)}", hata: false);
        }
        catch (OturumGecersizHatasi)
        {
            // Uygulama giriş ekranına döner (App.OturumKapandi).
        }
        catch (PdksApiHatasi h)
        {
            MesajGoster(h.Message, hata: true);
        }
        catch (Exception)
        {
            MesajGoster("Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edin.", hata: true);
        }
        finally
        {
            Mesgul = false;
        }
    }

    private void MesajGoster(string mesaj, bool hata)
    {
        Mesaj = mesaj;
        MesajHata = hata;
    }

    private void Guncelle()
    {
        Cevrimdisi = _takip.Cevrimdisi;
        if (_takip.Durum is not { } d) return;

        Durum = d.Durum;

        Kayitlar.Clear();
        foreach (var k in d.Kayitlar.Reverse()) Kayitlar.Add(k);

        var son = d.SonKayit;
        var oto = son?.Otomatik == true ? " (otomatik)" : "";
        (DurumBasligi, DurumAciklama) = d.Durum switch
        {
            PersonelDurumu.Iceride => ("Mesaidesiniz", $"{Gorunum.Saat(son?.Zaman)} itibarıyla içeridesiniz{oto}"),
            PersonelDurumu.Molada => ("Moladasınız", $"{Gorunum.Saat(son?.Zaman)} itibarıyla moladasınız{oto}"),
            PersonelDurumu.Cikti => ("Mesai bitti", $"{Gorunum.Saat(son?.Zaman)} itibarıyla çıkış yaptınız{oto}"),
            _ => ("Mesaiye başlamadınız", "Bugün henüz giriş kaydınız yok"),
        };
        SureleriGuncelle();
    }

    private void SureleriGuncelle()
    {
        if (_takip.Durum is not { } d) return;
        var ozet = GunOzeti.Hesapla(d.Kayitlar, _saat.GetUtcNow());
        CalismaSuresi = Gorunum.Sure(ozet.Calisma);
        MolaSuresi = Gorunum.Sure(ozet.Mola);
        IlkGiris = Gorunum.Saat(ozet.IlkGiris);
    }
}
