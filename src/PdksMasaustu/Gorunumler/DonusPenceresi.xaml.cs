using System.Windows;

namespace PdksMasaustu.Gorunumler;

/// <summary>Moladayken bilgisayar yeniden kullanılınca ekranın sağ altında açılan soru.</summary>
public partial class DonusPenceresi : Window
{
    public DonusPenceresi(string uygulamaAdi, string mesaj)
    {
        InitializeComponent();
        Title = $"{uygulamaAdi} · Moladasınız";
        MesajMetni.Text = mesaj;

        Loaded += (_, _) =>
        {
            var alan = SystemParameters.WorkArea;
            Left = alan.Right - ActualWidth - 16;
            Top = alan.Bottom - ActualHeight - 16;
        };
    }

    private void Don_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Devam_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
