using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using PdksMasaustu.Core.Api;
using PdksMasaustu.Core.Oturum;

namespace PdksMasaustu.Core;

public static class ServisKayitlari
{
    /// <summary>
    /// API istemcisi ve oturum kasasını kaydeder. IVeriKoruyucu ve
    /// PdksApiSecenekleri uygulama tarafından sağlanır.
    /// </summary>
    public static IServiceCollection AddPdksCore(this IServiceCollection servisler, string oturumDosyasi)
    {
        servisler.AddSingleton<IOturumKasasi>(sp =>
            new DosyaOturumKasasi(oturumDosyasi, sp.GetRequiredService<IVeriKoruyucu>()));

        servisler
            .AddHttpClient<IPdksApi, PdksApiIstemcisi>((sp, http) =>
            {
                var secenek = sp.GetRequiredService<IOptions<PdksApiSecenekleri>>().Value;
                var taban = secenek.TabanAdres.EndsWith('/') ? secenek.TabanAdres : secenek.TabanAdres + "/";
                http.BaseAddress = new Uri(taban);
                http.Timeout = secenek.ZamanAsimi;
            })
            .AddStandardResilienceHandler(o =>
            {
                // Hareket kaydı (POST) tekrar denenmez: ilk istek sunucuya ulaşıp yanıtı
                // kaybolduysa ikinci deneme aynı hareketi iki kez yazmaya çalışırdı.
                o.Retry.DisableForUnsafeHttpMethods();
            });

        return servisler;
    }
}
