using System.Security.Cryptography;
using PdksMasaustu.Core.Oturum;

namespace PdksMasaustu.Altyapi;

/// <summary>
/// Windows DPAPI: veri, oturum açmış Windows kullanıcısının anahtarıyla şifrelenir.
/// Dosya başka kullanıcıya ya da bilgisayara kopyalansa çözülemez.
/// </summary>
internal sealed class DpapiKoruyucu : IVeriKoruyucu
{
    private static readonly byte[] Tuz = "PdksMasaustu.Oturum.v1"u8.ToArray();

    public byte[] Koru(byte[] veri) => ProtectedData.Protect(veri, Tuz, DataProtectionScope.CurrentUser);

    public byte[] Coz(byte[] korunmus) => ProtectedData.Unprotect(korunmus, Tuz, DataProtectionScope.CurrentUser);
}
