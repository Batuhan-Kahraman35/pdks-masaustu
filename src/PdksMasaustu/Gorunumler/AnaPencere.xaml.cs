using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using PdksMasaustu.GorunumModelleri;

namespace PdksMasaustu.Gorunumler;

public partial class AnaPencere : Window
{
    private readonly AnaGorunumModeli _model;

    /// <summary>true olduğunda kapatma gerçekten kapatır; aksi halde pencere tepsiye gizlenir.</summary>
    public bool KapanisaIzinVer { get; set; }

    public AnaPencere(AnaGorunumModeli model)
    {
        InitializeComponent();
        DataContext = _model = model;
        Activated += (_, _) =>
        {
            TarihMetni.Text = DateTime.Today.ToString("d MMMM yyyy, dddd", CultureInfo.GetCultureInfo("tr-TR"));
            TarihSecici.DisplayDateEnd = DateTime.Today;
        };
    }

    /// <summary>Tepsiden ya da ikinci örnekten çağrılır: gizliyse gösterir, öne getirir.</summary>
    public void OneGetir()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        Topmost = true;   // Windows odak kısıtlamasını aşmak için kısa süreli
        Topmost = false;
        Focus();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!KapanisaIzinVer)
        {
            e.Cancel = true;
            Hide();
            _model.Ekip.Etkinlestir(false);
        }
        base.OnClosing(e);
    }

    private void Sekmeler_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source == Sekmeler) _model.Ekip.Etkinlestir(Sekmeler.SelectedItem == EkipSekmesi);
    }
}
