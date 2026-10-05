using System.Windows;
using PdksMasaustu.GorunumModelleri;

namespace PdksMasaustu.Gorunumler;

public partial class GirisPenceresi : Window
{
    private readonly GirisGorunumModeli _model;

    public GirisPenceresi(GirisGorunumModeli model)
    {
        InitializeComponent();
        DataContext = _model = model;
        SurumMetni.Text = "v" + Altyapi.Sistem.Surum;
        Loaded += (_, _) => KimlikKutusu.Focus();
    }

    private async void GirisButonu_Click(object sender, RoutedEventArgs e)
    {
        GirisButonu.IsEnabled = false;
        try
        {
            if (await _model.GirisYapAsync(SifreKutusu.Password))
            {
                DialogResult = true;
            }
            else
            {
                SifreKutusu.Clear();
                SifreKutusu.Focus();
            }
        }
        finally
        {
            GirisButonu.IsEnabled = true;
        }
    }
}
