using System.IO;

namespace PdksMasaustu.Altyapi;

/// <summary>
/// Kullanıcı verisi kurulum klasöründen ayrı tutulur: Velopack güncellemede
/// kurulum klasörünü (%LocalAppData%\PdksMasaustu\current) baştan yazar.
/// </summary>
internal static class Yollar
{
    public static string VeriKlasoru { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PdksMasaustu");

    public static string OturumDosyasi => Path.Combine(VeriKlasoru, "oturum.bin");
    public static string HataGunlugu => Path.Combine(VeriKlasoru, "hata.log");
    public static string LogoDosyasi => Path.Combine(VeriKlasoru, "logo.bin");
}
