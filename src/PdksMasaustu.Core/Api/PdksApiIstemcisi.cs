using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PdksMasaustu.Core.Modeller;
using PdksMasaustu.Core.Oturum;

namespace PdksMasaustu.Core.Api;

public interface IPdksApi
{
    Task<GirisYaniti> GirisYapAsync(string kimlik, string sifre, CancellationToken ct = default);
    Task<MarkaAyarlari> AyarlarGetirAsync(CancellationToken ct = default);
    Task<DurumYaniti> DurumGetirAsync(CancellationToken ct = default);
    Task<HareketYaniti> HareketGonderAsync(HareketIstegi istek, CancellationToken ct = default);
    Task<EkipDurumYaniti> EkipDurumGetirAsync(CancellationToken ct = default);
    Task<IReadOnlyList<EkipGecmisKaydi>> EkipGecmisGetirAsync(int kullaniciId, DateOnly tarih, CancellationToken ct = default);
    Task<UzakCihaz> CihazEslestirAsync(string anakartUuid, string? biosSeri, CancellationToken ct = default);
}

/// <summary>
/// pdks-api istemcisi. Yollar taban adrese görelidir (baştaki / yok), böylece
/// API bir alt dizinde (ör. /pdks-api/) yayınlansa da çalışır.
/// </summary>
public sealed class PdksApiIstemcisi(HttpClient http, IOturumKasasi kasa) : IPdksApi
{
    public async Task<GirisYaniti> GirisYapAsync(string kimlik, string sifre, CancellationToken ct = default)
    {
        using var istek = new HttpRequestMessage(HttpMethod.Post, "auth/login")
        {
            Content = JsonContent.Create(new { email = kimlik, sifre }, options: JsonAyarlari.Secenekler),
        };
        var yanit = await GonderAsync<GirisYaniti>(istek, tokenEkle: false, ct);
        kasa.Kaydet(new OturumBilgisi(yanit.Token, yanit.Personel));
        return yanit;
    }

    /// <summary>Marka ayarları herkese açıktır; giriş ekranında logo göstermek için token gerekmez.</summary>
    public Task<MarkaAyarlari> AyarlarGetirAsync(CancellationToken ct = default) =>
        GonderAsync<MarkaAyarlari>(new HttpRequestMessage(HttpMethod.Get, "ayarlar"), tokenEkle: false, ct);

    public Task<DurumYaniti> DurumGetirAsync(CancellationToken ct = default) =>
        GonderAsync<DurumYaniti>(new HttpRequestMessage(HttpMethod.Get, "masaustu/durum"), tokenEkle: true, ct);

    public Task<HareketYaniti> HareketGonderAsync(HareketIstegi istek, CancellationToken ct = default) =>
        GonderAsync<HareketYaniti>(new HttpRequestMessage(HttpMethod.Post, "masaustu/hareket")
        {
            Content = JsonContent.Create(istek, options: JsonAyarlari.Secenekler),
        }, tokenEkle: true, ct);

    public Task<EkipDurumYaniti> EkipDurumGetirAsync(CancellationToken ct = default) =>
        GonderAsync<EkipDurumYaniti>(new HttpRequestMessage(HttpMethod.Get, "masaustu/ekip/durum"), tokenEkle: true, ct);

    public async Task<IReadOnlyList<EkipGecmisKaydi>> EkipGecmisGetirAsync(int kullaniciId, DateOnly tarih, CancellationToken ct = default)
    {
        var adres = $"masaustu/ekip/gecmis?kullaniciId={kullaniciId}&tarih={tarih:yyyy-MM-dd}";
        var yanit = await GonderAsync<EkipGecmisYaniti>(new HttpRequestMessage(HttpMethod.Get, adres), tokenEkle: true, ct);
        return yanit.Kayitlar;
    }

    public Task<UzakCihaz> CihazEslestirAsync(string anakartUuid, string? biosSeri, CancellationToken ct = default) =>
        GonderAsync<UzakCihaz>(new HttpRequestMessage(HttpMethod.Put, "masaustu/cihaz")
        {
            Content = JsonContent.Create(new { anakartUuid, biosSeri }, options: JsonAyarlari.Secenekler),
        }, tokenEkle: true, ct);

    private async Task<T> GonderAsync<T>(HttpRequestMessage istek, bool tokenEkle, CancellationToken ct)
    {
        using (istek)
        {
            if (tokenEkle)
            {
                var oturum = kasa.Oku() ?? throw new OturumGecersizHatasi("Oturum açılmamış");
                istek.Headers.Authorization = new AuthenticationHeaderValue("Bearer", oturum.Token);
            }

            using var yanit = await http.SendAsync(istek, ct);
            if (yanit.IsSuccessStatusCode)
            {
                return await yanit.Content.ReadFromJsonAsync<T>(JsonAyarlari.Secenekler, ct)
                       ?? throw new PdksApiHatasi(yanit.StatusCode, "Sunucudan boş yanıt geldi");
            }

            var mesaj = await HataMesajiOkuAsync(yanit, ct);
            if (yanit.StatusCode == HttpStatusCode.Unauthorized && tokenEkle)
            {
                // Süresi dolan token saklanmaz; bir sonraki açılışta giriş ekranı gelir.
                kasa.Sil();
                throw new OturumGecersizHatasi(mesaj);
            }
            throw new PdksApiHatasi(yanit.StatusCode, mesaj);
        }
    }

    /// <summary>API hataları { hata, detay? } gövdesiyle döner; gövde yoksa durum kodu yazılır.</summary>
    private static async Task<string> HataMesajiOkuAsync(HttpResponseMessage yanit, CancellationToken ct)
    {
        try
        {
            var govde = await yanit.Content.ReadFromJsonAsync<HataGovdesi>(JsonAyarlari.Secenekler, ct);
            if (!string.IsNullOrWhiteSpace(govde?.Hata)) return govde.Hata;
        }
        catch (JsonException)
        {
            // Proxy/IIS kaynaklı HTML hata sayfası; aşağıdaki genel mesaja düşülür.
        }
        return $"Sunucu hatası ({(int)yanit.StatusCode})";
    }

    private sealed record HataGovdesi(string? Hata);
}
