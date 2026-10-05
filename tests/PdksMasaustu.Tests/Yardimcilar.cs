using System.Net;
using System.Text;
using PdksMasaustu.Core.Oturum;

namespace PdksMasaustu.Tests;

/// <summary>İstekleri kaydeden, yanıtı testin verdiği sahte HTTP katmanı.</summary>
internal sealed class SahteHttp(HttpStatusCode durum, string govde) : HttpMessageHandler
{
    public HttpRequestMessage? SonIstek { get; private set; }
    public string? SonGovde { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage istek, CancellationToken ct)
    {
        SonIstek = istek;
        SonGovde = istek.Content is null ? null : await istek.Content.ReadAsStringAsync(ct);
        return new HttpResponseMessage(durum) { Content = new StringContent(govde, Encoding.UTF8, "application/json") };
    }
}

internal sealed class BellekOturumKasasi(OturumBilgisi? baslangic = null) : IOturumKasasi
{
    public OturumBilgisi? Oturum { get; private set; } = baslangic;
    public OturumBilgisi? Oku() => Oturum;
    public void Kaydet(OturumBilgisi oturum) => Oturum = oturum;
    public void Sil() => Oturum = null;
}

/// <summary>Testte gerçek şifreleme yerine baytları ters çevirir; düz metin olarak yazılmadığı doğrulanabilir.</summary>
internal sealed class TersCeviriciKoruyucu : IVeriKoruyucu
{
    public byte[] Koru(byte[] veri) => veri.Reverse().ToArray();
    public byte[] Coz(byte[] korunmus) => korunmus.Reverse().ToArray();
}
