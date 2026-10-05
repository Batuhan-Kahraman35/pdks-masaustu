using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PdksMasaustu.Altyapi;
using PdksMasaustu.Core;
using PdksMasaustu.Core.Api;
using PdksMasaustu.Core.Oturum;
using PdksMasaustu.Core.Takip;
using PdksMasaustu.Demo;
using PdksMasaustu.Gorunumler;
using PdksMasaustu.GorunumModelleri;
using PdksMasaustu.Tepsi;

namespace PdksMasaustu;

/// <summary>Komut satırı seçenekleri: --demo (sunucusuz deneme), --arka-plan (Windows açılışı: pencere açılmaz).</summary>
public sealed record UygulamaSecenekleri(bool Demo, bool ArkaPlan)
{
    public static UygulamaSecenekleri Oku(string[] args) =>
        new(args.Contains("--demo", StringComparer.OrdinalIgnoreCase), args.Contains("--arka-plan", StringComparer.OrdinalIgnoreCase));
}

internal static class HostKurulumu
{
    public static IHost Olustur(string[] args, UygulamaSecenekleri secenekler)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,   // appsettings.json exe'nin yanında
        });
        builder.Logging.ClearProviders();

        var s = builder.Services;
        s.Configure<PdksApiSecenekleri>(builder.Configuration.GetSection(PdksApiSecenekleri.Bolum));
        s.Configure<GuncellemeSecenekleri>(builder.Configuration.GetSection(GuncellemeSecenekleri.Bolum));
        s.AddSingleton<GuncellemeServisi>();
        s.Configure<MarkaSecenekleri>(builder.Configuration.GetSection(MarkaSecenekleri.Bolum));
        s.AddSingleton<Marka>();
        s.AddSingleton(secenekler);
        s.AddSingleton(TimeProvider.System);
        s.AddSingleton(Sistem.CihazBilgisiOku());
        s.AddSingleton<ISonGirdiKaynagi, WindowsSonGirdi>();
        s.AddSingleton<IVeriKoruyucu, DpapiKoruyucu>();

        if (secenekler.Demo)
        {
            s.AddSingleton<IOturumKasasi, DemoOturumKasasi>();
            s.AddSingleton<IPdksApi, DemoPdksApi>();
        }
        else
        {
            s.AddPdksCore(Yollar.OturumDosyasi);
        }

        s.AddSingleton<TakipKoordinatoru>();
        s.AddSingleton<EkipGorunumModeli>();
        s.AddSingleton<AnaGorunumModeli>();
        s.AddTransient<GirisGorunumModeli>();
        s.AddSingleton<AnaPencere>();
        s.AddTransient<GirisPenceresi>();
        s.AddSingleton<TepsiYoneticisi>();

        return builder.Build();
    }
}
