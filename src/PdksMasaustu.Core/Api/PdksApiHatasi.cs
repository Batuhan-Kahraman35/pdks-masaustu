using System.Net;

namespace PdksMasaustu.Core.Api;

/// <summary>API'nin { hata } gövdesiyle döndüğü iş kuralı veya sunucu hatası.</summary>
public class PdksApiHatasi(HttpStatusCode durumKodu, string mesaj) : Exception(mesaj)
{
    public HttpStatusCode DurumKodu { get; } = durumKodu;
}

/// <summary>Token yok, geçersiz ya da süresi dolmuş (401). Uygulama giriş ekranına döner.</summary>
public sealed class OturumGecersizHatasi(string mesaj) : PdksApiHatasi(HttpStatusCode.Unauthorized, mesaj);
