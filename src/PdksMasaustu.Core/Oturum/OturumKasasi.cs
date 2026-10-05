using System.Text.Json;
using PdksMasaustu.Core.Api;
using PdksMasaustu.Core.Modeller;

namespace PdksMasaustu.Core.Oturum;

/// <summary>Giriş sonrası saklanan token ve personel bilgisi.</summary>
public sealed record OturumBilgisi(string Token, Personel Personel);

/// <summary>Veriyi diskte okunamaz hale getirir (Windows'ta DPAPI, kullanıcıya bağlı).</summary>
public interface IVeriKoruyucu
{
    byte[] Koru(byte[] veri);
    byte[] Coz(byte[] korunmus);
}

public interface IOturumKasasi
{
    OturumBilgisi? Oku();
    void Kaydet(OturumBilgisi oturum);
    void Sil();
}

/// <summary>
/// Oturumu tek bir şifreli dosyada tutar. Bozuk veya başka kullanıcıya ait
/// (çözülemeyen) dosya oturum yok sayılır; personel yeniden giriş yapar.
/// </summary>
public sealed class DosyaOturumKasasi(string dosyaYolu, IVeriKoruyucu koruyucu) : IOturumKasasi
{
    private readonly Lock _kilit = new();
    private OturumBilgisi? _onbellek;
    private bool _okundu;

    public OturumBilgisi? Oku()
    {
        lock (_kilit)
        {
            if (_okundu) return _onbellek;
            _okundu = true;
            try
            {
                if (!File.Exists(dosyaYolu)) return null;
                var json = koruyucu.Coz(File.ReadAllBytes(dosyaYolu));
                _onbellek = JsonSerializer.Deserialize<OturumBilgisi>(json, JsonAyarlari.Secenekler);
            }
            catch (Exception e) when (e is IOException or JsonException or System.Security.Cryptography.CryptographicException)
            {
                _onbellek = null;
            }
            return _onbellek;
        }
    }

    public void Kaydet(OturumBilgisi oturum)
    {
        lock (_kilit)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dosyaYolu)!);
            var json = JsonSerializer.SerializeToUtf8Bytes(oturum, JsonAyarlari.Secenekler);
            File.WriteAllBytes(dosyaYolu, koruyucu.Koru(json));
            _onbellek = oturum;
            _okundu = true;
        }
    }

    public void Sil()
    {
        lock (_kilit)
        {
            if (File.Exists(dosyaYolu)) File.Delete(dosyaYolu);
            _onbellek = null;
            _okundu = true;
        }
    }
}
