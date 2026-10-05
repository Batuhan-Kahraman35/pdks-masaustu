using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using PdksMasaustu.Core.Modeller;

namespace PdksMasaustu.Gorunumler;

/// <summary>Durum ve hareket tiplerinin renk, metin ve ikonları tek yerde.</summary>
public static class Gorunum
{
    private static SolidColorBrush Firca(byte r, byte g, byte b)
    {
        var f = new SolidColorBrush(Color.FromRgb(r, g, b));
        f.Freeze();
        return f;
    }

    public static readonly SolidColorBrush Yesil = Firca(0x2E, 0x9E, 0x5B);
    public static readonly SolidColorBrush Turuncu = Firca(0xF0, 0x8A, 0x24);
    public static readonly SolidColorBrush Mavi = Firca(0x2B, 0x88, 0xD8);
    public static readonly SolidColorBrush Kirmizi = Firca(0xD6, 0x45, 0x45);
    public static readonly SolidColorBrush Gri = Firca(0x8A, 0x8A, 0x8A);

    // Segoe Fluent Icons (Windows 11) / Segoe MDL2 Assets (Windows 10) karakterleri
    public const string IkonOynat = "";
    public const string IkonDuraklat = "";
    public const string IkonKapat = "";
    public const string IkonSaat = "";

    public static Brush DurumRengi(PersonelDurumu d) => d switch
    {
        PersonelDurumu.Iceride => Yesil,
        PersonelDurumu.Molada => Turuncu,
        PersonelDurumu.Cikti => Kirmizi,
        _ => Gri,
    };

    public static string DurumMetni(PersonelDurumu d) => d switch
    {
        PersonelDurumu.Iceride => "İçeride",
        PersonelDurumu.Molada => "Molada",
        PersonelDurumu.Cikti => "Çıkış yaptı",
        _ => "Gelmedi",
    };

    public static Brush TipRengi(HareketTipi t) => t switch
    {
        HareketTipi.Giris => Yesil,
        HareketTipi.MolaGiris => Turuncu,
        HareketTipi.MolaCikis => Mavi,
        _ => Kirmizi,
    };

    public static string TipMetni(HareketTipi t) => t switch
    {
        HareketTipi.Giris => "Giriş",
        HareketTipi.MolaGiris => "Mola başladı",
        HareketTipi.MolaCikis => "Moladan dönüş",
        _ => "Çıkış",
    };

    public static string TipIkonu(HareketTipi t) => t switch
    {
        HareketTipi.Giris or HareketTipi.MolaCikis => IkonOynat,
        HareketTipi.MolaGiris => IkonDuraklat,
        _ => IkonKapat,
    };

    public static string Sure(TimeSpan s) => s.TotalMinutes < 1 ? "0 dk" : s.TotalHours >= 1 ? $"{(int)s.TotalHours} sa {s.Minutes} dk" : $"{s.Minutes} dk";

    public static string Saat(DateTimeOffset? z) => z?.ToLocalTime().ToString("HH:mm", CultureInfo.GetCultureInfo("tr-TR")) ?? "–";
}

/// <summary>XAML'de Converter="{x:Static g:Donustur.DurumRengi}" şeklinde kullanılır.</summary>
public sealed class Donustur(Func<object?, object?> islev) : IValueConverter
{
    public static readonly Donustur DurumRengi = new(v => v is PersonelDurumu d ? Gorunum.DurumRengi(d) : Gorunum.Gri);
    public static readonly Donustur DurumMetni = new(v => v is PersonelDurumu d ? Gorunum.DurumMetni(d) : "");
    public static readonly Donustur TipRengi = new(v => v is HareketTipi t ? Gorunum.TipRengi(t) : Gorunum.Gri);
    public static readonly Donustur TipMetni = new(v => v is HareketTipi t ? Gorunum.TipMetni(t) : "–");
    public static readonly Donustur TipIkonu = new(v => v is HareketTipi t ? Gorunum.TipIkonu(t) : "");
    public static readonly Donustur Saat = new(v => Gorunum.Saat(v as DateTimeOffset?));
    public static readonly Donustur Gorunurluk = new(v => v is true ? Visibility.Visible : Visibility.Collapsed);
    public static readonly Donustur TersGorunurluk = new(v => v is true ? Visibility.Collapsed : Visibility.Visible);
    public static readonly Donustur DoluysaGorunur = new(v => string.IsNullOrEmpty(v as string) ? Visibility.Collapsed : Visibility.Visible);
    public static readonly Donustur BossaGorunur = new(v => string.IsNullOrEmpty(v as string) ? Visibility.Visible : Visibility.Collapsed);
    public static readonly Donustur SifirsaGorunur = new(v => v is 0 ? Visibility.Visible : Visibility.Collapsed);
    public static readonly Donustur NullsaGorunur = new(v => v is null ? Visibility.Visible : Visibility.Collapsed);
    public static readonly Donustur NullDegilseGorunur = new(v => v is null ? Visibility.Collapsed : Visibility.Visible);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => islev(value);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
