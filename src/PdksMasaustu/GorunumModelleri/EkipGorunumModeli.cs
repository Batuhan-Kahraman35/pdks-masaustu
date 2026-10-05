using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdksMasaustu.Core.Api;
using PdksMasaustu.Core.Modeller;
using PdksMasaustu.Gorunumler;

namespace PdksMasaustu.GorunumModelleri;

/// <summary>
/// "Ekibim" sekmesi: kapsamdaki personelin anlık durumu ve seçilen personelin
/// günlük hareketleri. Kapsam (kim kimi görür) tamamen sunucuda belirlenir.
/// </summary>
public sealed partial class EkipGorunumModeli : ObservableObject
{
    private readonly IPdksApi _api;
    private readonly DispatcherTimer _yenileme;
    private CancellationTokenSource? _gecmisIptal;

    public EkipGorunumModeli(IPdksApi api)
    {
        _api = api;
        PersonelGorunumu = CollectionViewSource.GetDefaultView(Personel);
        PersonelGorunumu.Filter = o => o is EkipUyesi u && Eslesir(u);

        _yenileme = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _yenileme.Tick += async (_, _) => await YenileAsync();
    }

    public ObservableCollection<EkipUyesi> Personel { get; } = [];
    public ICollectionView PersonelGorunumu { get; }
    public ObservableCollection<EkipGecmisKaydi> Gecmis { get; } = [];

    [ObservableProperty] private EkipOzeti _ozet = new(0, 0, 0, 0);
    [ObservableProperty] private string _arama = "";
    [ObservableProperty] private PersonelDurumu? _durumFiltresi;
    [ObservableProperty] private EkipUyesi? _seciliUye;
    [ObservableProperty] private DateTime _seciliTarih = DateTime.Today;
    [ObservableProperty] private bool _yukleniyor;
    [ObservableProperty] private string? _hata;
    [ObservableProperty] private string _sonGuncelleme = "";
    [ObservableProperty] private string _gecmisOzeti = "";

    /// <summary>Yükleme başarılı ama kapsamda kimse yok (bağlı personeli olmayan kullanıcı).</summary>
    [ObservableProperty] private bool _ekipBos;

    /// <summary>Sekme görünürken dakikada bir yenilenir; gizliyken sunucuya istek atılmaz.</summary>
    public void Etkinlestir(bool etkin)
    {
        if (etkin)
        {
            _ = YenileAsync();
            _yenileme.Start();
        }
        else
        {
            _yenileme.Stop();
        }
    }

    [RelayCommand]
    private Task Yenile() => YenileAsync();

    /// <summary>Özet kutusuna tıklanınca listeyi o duruma süzer; aynı kutuya tekrar tıklamak süzmeyi kaldırır.</summary>
    [RelayCommand]
    private void DurumaSuz(PersonelDurumu? durum) => DurumFiltresi = DurumFiltresi == durum ? null : durum;

    partial void OnAramaChanged(string value) => PersonelGorunumu.Refresh();
    partial void OnDurumFiltresiChanged(PersonelDurumu? value) => PersonelGorunumu.Refresh();
    partial void OnSeciliUyeChanged(EkipUyesi? value) => _ = GecmisYukleAsync();
    partial void OnSeciliTarihChanged(DateTime value) => _ = GecmisYukleAsync();

    private bool Eslesir(EkipUyesi u) =>
        (DurumFiltresi is null || u.Durum == DurumFiltresi) &&
        (string.IsNullOrWhiteSpace(Arama)
         || u.AdSoyad.Contains(Arama, StringComparison.CurrentCultureIgnoreCase)
         || (u.Departman?.Contains(Arama, StringComparison.CurrentCultureIgnoreCase) ?? false));

    private async Task YenileAsync()
    {
        if (Yukleniyor) return;
        Yukleniyor = true;
        try
        {
            var yanit = await _api.EkipDurumGetirAsync();
            var seciliId = SeciliUye?.Id;

            Personel.Clear();
            foreach (var u in yanit.Personel) Personel.Add(u);
            Ozet = yanit.Ozet;
            EkipBos = Personel.Count == 0;
            Hata = null;
            SonGuncelleme = $"Son güncelleme {DateTime.Now:HH:mm}";

            // Yenileme seçimi bozmasın (sağdaki geçmiş paneli kapanmasın).
            if (seciliId is not null) SeciliUye = Personel.FirstOrDefault(u => u.Id == seciliId);
        }
        catch (OturumGecersizHatasi)
        {
            _yenileme.Stop();
        }
        catch (PdksApiHatasi h)
        {
            Hata = h.Message;
        }
        catch (Exception)
        {
            Hata = "Sunucuya ulaşılamadı.";
        }
        finally
        {
            Yukleniyor = false;
        }
    }

    private async Task GecmisYukleAsync()
    {
        _gecmisIptal?.Cancel();
        Gecmis.Clear();
        GecmisOzeti = "";
        if (SeciliUye is not { } uye) return;

        var iptal = _gecmisIptal = new CancellationTokenSource();
        try
        {
            var kayitlar = await _api.EkipGecmisGetirAsync(uye.Id, DateOnly.FromDateTime(SeciliTarih), iptal.Token);
            if (iptal.IsCancellationRequested) return;

            foreach (var k in kayitlar) Gecmis.Add(k);
            var ozet = Core.Takip.GunOzeti.Hesapla(
                kayitlar.Select(k => new HareketKaydi(k.Id, k.Tip, k.Zaman, k.Otomatik, k.Sube)),
                SeciliTarih.Date == DateTime.Today ? DateTimeOffset.Now : kayitlar.LastOrDefault()?.Zaman ?? DateTimeOffset.Now);
            GecmisOzeti = kayitlar.Count == 0
                ? "Bu gün için kayıt yok"
                : $"Çalışma {Gorunum.Sure(ozet.Calisma)} · Mola {Gorunum.Sure(ozet.Mola)}";
        }
        catch (OperationCanceledException)
        {
            // Yeni seçim yapıldı.
        }
        catch (Exception e) when (e is not OturumGecersizHatasi)
        {
            GecmisOzeti = e is PdksApiHatasi h ? h.Message : "Sunucuya ulaşılamadı.";
        }
    }
}
