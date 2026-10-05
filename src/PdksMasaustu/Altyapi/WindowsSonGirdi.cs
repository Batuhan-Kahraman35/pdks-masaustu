using System.Runtime.InteropServices;
using PdksMasaustu.Core.Takip;

namespace PdksMasaustu.Altyapi;

/// <summary>
/// Son klavye/fare hareketini GetLastInputInfo ile okur. Genel klavye kancası
/// kurulmaz: tuş içeriği hiç görülmez, yalnız "en son ne zaman" bilgisi alınır
/// (antivirüslere takılmaz, gizlilik açısından da en hafif yöntemdir).
/// </summary>
internal sealed partial class WindowsSonGirdi(TimeProvider saat) : ISonGirdiKaynagi
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetLastInputInfo(ref LASTINPUTINFO bilgi);

    public DateTimeOffset SonGirdi()
    {
        var simdi = saat.GetUtcNow();
        var bilgi = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref bilgi)) return simdi;

        // dwTime 32 bit milisaniye sayacıdır (~49,7 günde bir başa döner);
        // işaretsiz çıkarma taşmayı doğru ele alır.
        var gecen = unchecked((uint)Environment.TickCount - bilgi.dwTime);
        return simdi - TimeSpan.FromMilliseconds(gecen);
    }
}
