using PdksMasaustu.Altyapi;
using Velopack;

namespace PdksMasaustu;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Her şeyden önce: Velopack kurulum/kaldırma/güncelleme sırasında uygulamayı
        // özel parametrelerle kısa süreliğine çalıştırır; bu çağrı o durumda işini yapıp çıkar.
        VelopackApp.Build()
            .OnAfterInstallFastCallback(_ => Sistem.OtomatikBaslatmayiKaydet())
            .OnBeforeUninstallFastCallback(_ => Sistem.OtomatikBaslatmayiKaldir())
            .Run();

        using var tekOrnek = new TekOrnek(UygulamaSecenekleri.Oku(args).Demo ? ".Demo" : "");
        if (!tekOrnek.IlkOrnekMi)
        {
            // Uygulama zaten açık: onun penceresini öne getir ve çık.
            tekOrnek.DigerOrnegiUyandir();
            return;
        }

        var uygulama = new App(args, tekOrnek);
        uygulama.InitializeComponent();
        uygulama.Run();
    }
}
