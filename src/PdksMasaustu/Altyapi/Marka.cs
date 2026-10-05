using System.IO;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Options;
using PdksMasaustu.Core.Api;

namespace PdksMasaustu.Altyapi;

/// <summary>appsettings.json → "Marka" bölümü. Repoda genel ad durur; gerçek ad yerel yapılandırmadadır.</summary>
public sealed class MarkaSecenekleri
{
    public const string Bolum = "Marka";

    public string UygulamaAdi { get; set; } = "PDKS Masaüstü";
}

/// <summary>
/// Uygulama adı ve logo. Logo portalın marka ayarından (tanim_site_ayarlari.site_ayarlari_logo_url)
/// çalışma anında okunur: panelden değiştirildiğinde uygulama da yeni logoyu gösterir.
/// Son indirilen logo yerelde saklanır; açılışta ve sunucuya ulaşılamadığında o kullanılır.
/// </summary>
public sealed partial class Marka(
    IOptions<MarkaSecenekleri> secenekler, IPdksApi api, IOptions<PdksApiSecenekleri> apiSecenekleri, UygulamaSecenekleri uygulama) : ObservableObject
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    /// <summary>Demo firmaya özgü hiçbir şey göstermez: ekran görüntüleri ve repoyu deneyenler için genel kalır.</summary>
    private readonly bool _demo = uygulama.Demo;

    public string UygulamaAdi { get; } = uygulama.Demo ? new MarkaSecenekleri().UygulamaAdi : secenekler.Value.UygulamaAdi;

    /// <summary>Logo henüz yoksa null; görünümler bu durumda genel saat simgesini gösterir.</summary>
    [ObservableProperty] private ImageSource? _logo;

    public void OnbellektenYukle()
    {
        if (_demo) return;
        try
        {
            if (File.Exists(Yollar.LogoDosyasi)) Logo = GorselOlustur(File.ReadAllBytes(Yollar.LogoDosyasi));
        }
        catch (Exception e) when (e is IOException or NotSupportedException or ArgumentException)
        {
            // Bozuk önbellek: sunucudan yeniden indirilir.
        }
    }

    public async Task YenileAsync()
    {
        try
        {
            var ayarlar = await api.AyarlarGetirAsync();
            if (string.IsNullOrWhiteSpace(ayarlar.LogoUrl)) return;

            // Logo adresi site köküne göredir (/admin/assets/...); API alt dizinde olsa da kökten çözülür.
            var adres = new Uri(new Uri(apiSecenekleri.Value.TabanAdres), ayarlar.LogoUrl);
            var veri = await Http.GetByteArrayAsync(adres);
            var gorsel = GorselOlustur(veri);   // görsel değilse burada hata verir, önbellek bozulmaz

            Directory.CreateDirectory(Yollar.VeriKlasoru);
            await File.WriteAllBytesAsync(Yollar.LogoDosyasi, veri);
            Logo = gorsel;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or PdksApiHatasi
                                       or IOException or NotSupportedException or UriFormatException)
        {
            // Logo süs öğesidir: alınamazsa önbellekteki ya da genel simge kullanılır.
        }
    }

    private static BitmapImage GorselOlustur(byte[] veri)
    {
        var gorsel = new BitmapImage();
        gorsel.BeginInit();
        gorsel.CacheOption = BitmapCacheOption.OnLoad;
        gorsel.StreamSource = new MemoryStream(veri);
        gorsel.EndInit();
        gorsel.Freeze();
        return gorsel;
    }
}
