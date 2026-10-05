using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using PdksMasaustu.Core.Takip;

namespace PdksMasaustu.Altyapi;

internal static class Sistem
{
    private const string CalistirAnahtari = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string UygulamaAdi = "PdksMasaustu";

    public static string Surum { get; } =
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "0.0.0";

    /// <summary>Kayıtlara yazılan cihaz bilgisi. Makine kimliği Windows kurulumuna özgü MachineGuid'dir.</summary>
    public static CihazBilgisi CihazBilgisiOku()
    {
        var model = $"PdksMasaustu {Surum} ({RuntimeInformation.OSDescription.Trim()})";
        string? kimlik = null;
        try
        {
            using var anahtar = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            kimlik = anahtar?.GetValue("MachineGuid") as string;
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException)
        {
            // Kısıtlı hesaplarda okunamayabilir; bilgisayar adına düşülür.
        }
        return new CihazBilgisi(model[..Math.Min(model.Length, 200)], kimlik ?? Environment.MachineName);
    }

    /// <summary>Windows oturumu açıldığında uygulamayı tepsiye küçültülmüş olarak başlatır.</summary>
    public static void OtomatikBaslatmayiKaydet()
    {
        if (Environment.ProcessPath is not { } yol) return;
        using var anahtar = Registry.CurrentUser.CreateSubKey(CalistirAnahtari);
        anahtar.SetValue(UygulamaAdi, $"\"{yol}\" --arka-plan");
    }

    public static void OtomatikBaslatmayiKaldir()
    {
        using var anahtar = Registry.CurrentUser.OpenSubKey(CalistirAnahtari, writable: true);
        anahtar?.DeleteValue(UygulamaAdi, throwOnMissingValue: false);
    }

    /// <summary>Yakalanmamış hataları kullanıcı veri klasöründeki günlüğe yazar.</summary>
    public static void HataYaz(Exception hata)
    {
        try
        {
            Directory.CreateDirectory(Yollar.VeriKlasoru);
            File.AppendAllText(Yollar.HataGunlugu, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] v{Surum}{Environment.NewLine}{hata}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // Günlük yazılamazsa uygulama yine de çalışmaya devam eder.
        }
    }
}
