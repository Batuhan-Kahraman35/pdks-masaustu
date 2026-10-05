using CommunityToolkit.Mvvm.ComponentModel;
using PdksMasaustu.Core.Api;

namespace PdksMasaustu.GorunumModelleri;

public sealed partial class GirisGorunumModeli(IPdksApi api, UygulamaSecenekleri secenekler, Altyapi.Marka marka) : ObservableObject
{
    public Altyapi.Marka Marka { get; } = marka;

    [ObservableProperty] private string _kimlik = "";
    [ObservableProperty] private bool _mesgul;
    [ObservableProperty] private string? _hata;

    public bool DemoModu { get; } = secenekler.Demo;

    /// <summary>Şifre bağlanabilir bir özellik olarak tutulmaz (PasswordBox), çağrıda verilir.</summary>
    public async Task<bool> GirisYapAsync(string sifre)
    {
        if (!DemoModu && (string.IsNullOrWhiteSpace(Kimlik) || string.IsNullOrEmpty(sifre)))
        {
            Hata = "E-posta / TC kimlik no ve şifre gerekli.";
            return false;
        }

        Mesgul = true;
        Hata = null;
        try
        {
            await api.GirisYapAsync(Kimlik.Trim(), sifre);
            return true;
        }
        catch (PdksApiHatasi h)
        {
            Hata = h.Message;
        }
        catch (Exception)
        {
            Hata = "Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edin.";
        }
        finally
        {
            Mesgul = false;
        }
        return false;
    }
}
