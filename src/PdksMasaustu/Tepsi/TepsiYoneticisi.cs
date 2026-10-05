using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using PdksMasaustu.Core.Modeller;
using PdksMasaustu.Gorunumler;
using PdksMasaustu.GorunumModelleri;

namespace PdksMasaustu.Tepsi;

/// <summary>
/// Sistem tepsisi simgesi: rengi personelin durumunu gösterir (yeşil içeride,
/// turuncu molada, kırmızı çıktı, gri gelmedi). Sağ tık menüsü ana penceredeki
/// komutların aynısını kullanır; hangi işlemin yapılabileceği tek yerden yönetilir.
/// </summary>
public sealed class TepsiYoneticisi : IDisposable
{
    private readonly AnaGorunumModeli _model;
    private readonly string _ad;
    private readonly Dictionary<PersonelDurumu, System.Drawing.Icon> _ikonlar = [];
    private TaskbarIcon? _ikon;
    private Action? _bildirimTiklaninca;

    public event Action? AcIstendi;
    public event Action? OturumuKapatIstendi;
    public event Action? KapatIstendi;

    public TepsiYoneticisi(AnaGorunumModeli model)
    {
        _model = model;
        _ad = model.Marka.UygulamaAdi;
        _model.PropertyChanged += Model_PropertyChanged;
    }

    public void Olustur()
    {
        var menu = new ContextMenu();
        menu.Items.Add(Oge("Pencereyi aç", () => AcIstendi?.Invoke(), yazi: FontWeights.SemiBold));
        menu.Items.Add(new Separator());
        menu.Items.Add(Oge("Giriş Yap", _model.GirisYapCommand));
        menu.Items.Add(Oge("Molaya Çık", _model.MolayaCikCommand));
        menu.Items.Add(Oge("Moladan Dön", _model.MoladanDonCommand));
        menu.Items.Add(Oge("Çıkış Yap", _model.CikisYapCommand));
        menu.Items.Add(new Separator());
        menu.Items.Add(Oge("Oturumu kapat", () => OturumuKapatIstendi?.Invoke()));
        menu.Items.Add(Oge("Uygulamadan çık", () => KapatIstendi?.Invoke()));

        _ikon = new TaskbarIcon
        {
            ToolTipText = _ad,
            Icon = Ikon(PersonelDurumu.Gelmedi),
            ContextMenu = menu,
            MenuActivation = PopupActivationMode.RightClick,
            NoLeftClickDelay = true,
        };
        _ikon.TrayLeftMouseUp += (_, _) => AcIstendi?.Invoke();
        _ikon.TrayBalloonTipClicked += (_, _) => _bildirimTiklaninca?.Invoke();
        _ikon.ForceCreate(enablesEfficiencyMode: false);
    }

    /// <summary>İndirilen sürüm için menünün en üstüne "kur" öğesi ekler ve bildirim gösterir.</summary>
    public void GuncellemeHazir(string surum, Action kur)
    {
        if (_ikon?.ContextMenu is not { } menu) return;
        menu.Items.Insert(0, Oge($"Güncellemeyi şimdi kur (v{surum})", kur, FontWeights.SemiBold));
        menu.Items.Insert(1, new Separator());
        Bildir("Yeni sürüm hazır",
            $"{_ad} v{surum} indirildi. Bilgisayardan ayrıldığınızda otomatik kurulacak; hemen kurmak için tıklayın.", kur);
    }

    /// <summary>Windows bildirimi gösterir; tıklanırsa verilen eylem çalışır.</summary>
    public void Bildir(string baslik, string mesaj, Action? tiklaninca = null)
    {
        _bildirimTiklaninca = tiklaninca;
        _ikon?.ShowNotification(baslik, mesaj, NotificationIcon.Info);
    }

    private void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_ikon is null || e.PropertyName is not (nameof(AnaGorunumModeli.Durum) or nameof(AnaGorunumModeli.DurumAciklama)))
            return;

        var durum = _model.Durum ?? PersonelDurumu.Gelmedi;
        _ikon.Icon = Ikon(durum);
        // Windows ipucu metnini 127 karakterle sınırlar.
        var ipucu = $"{_ad} · {Gorunum.DurumMetni(durum)}\n{_model.DurumAciklama}";
        _ikon.ToolTipText = ipucu.Length > 127 ? ipucu[..127] : ipucu;
    }

    private static MenuItem Oge(string baslik, ICommand komut) => new() { Header = baslik, Command = komut };

    private static MenuItem Oge(string baslik, Action eylem, FontWeight? yazi = null)
    {
        var oge = new MenuItem { Header = baslik };
        if (yazi is { } y) oge.FontWeight = y;
        oge.Click += (_, _) => eylem();
        return oge;
    }

    /// <summary>Durum rengindeki yuvarlak simge; ikon dosyası gerekmeden çizilir ve önbelleğe alınır.</summary>
    private System.Drawing.Icon Ikon(PersonelDurumu durum)
    {
        if (_ikonlar.TryGetValue(durum, out var hazir)) return hazir;

        const int boyut = 64;
        var gorsel = new DrawingVisual();
        using (var dc = gorsel.RenderOpen())
        {
            dc.DrawEllipse(Gorunum.DurumRengi(durum), null, new Point(boyut / 2.0, boyut / 2.0), boyut / 2.0 - 2, boyut / 2.0 - 2);
            var metin = new FormattedText("P", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                40, Brushes.White, 1.0);
            dc.DrawText(metin, new Point((boyut - metin.Width) / 2, (boyut - metin.Height) / 2));
        }
        var bitmap = new RenderTargetBitmap(boyut, boyut, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(gorsel);

        // IconSource yalnız dosya/URI kaynaklı görsel kabul ettiği için bellekte çizilen simge
        // PNG → System.Drawing.Bitmap → Icon yoluyla doğrudan Icon özelliğine verilir.
        var kodlayici = new PngBitmapEncoder();
        kodlayici.Frames.Add(BitmapFrame.Create(bitmap));
        using var akis = new MemoryStream();
        kodlayici.Save(akis);
        akis.Position = 0;
        using var gdiBitmap = new System.Drawing.Bitmap(akis);
        return _ikonlar[durum] = System.Drawing.Icon.FromHandle(gdiBitmap.GetHicon());
    }

    public void Dispose()
    {
        _model.PropertyChanged -= Model_PropertyChanged;
        _ikon?.Dispose();
        _ikon = null;
        foreach (var i in _ikonlar.Values) i.Dispose();
        _ikonlar.Clear();
    }
}
