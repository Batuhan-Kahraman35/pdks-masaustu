using PdksMasaustu.Core.Modeller;

namespace PdksMasaustu.Core.Takip;

/// <summary>Günün hareketlerinden çalışma (mola hariç) ve mola sürelerini hesaplar.</summary>
public sealed record GunOzeti(TimeSpan Calisma, TimeSpan Mola, DateTimeOffset? IlkGiris)
{
    public static GunOzeti Hesapla(IEnumerable<HareketKaydi> kayitlar, DateTimeOffset simdi)
    {
        DateTimeOffset? iceriBaslangic = null, molaBaslangic = null, ilkGiris = null;
        TimeSpan calisma = TimeSpan.Zero, mola = TimeSpan.Zero;

        foreach (var k in kayitlar.OrderBy(k => k.Zaman).ThenBy(k => k.Id))
        {
            switch (k.Tip)
            {
                case HareketTipi.Giris:
                    iceriBaslangic = k.Zaman;
                    ilkGiris ??= k.Zaman;
                    break;
                case HareketTipi.MolaGiris:
                    calisma += Sure(ref iceriBaslangic, k.Zaman);
                    molaBaslangic = k.Zaman;
                    break;
                case HareketTipi.MolaCikis:
                    mola += Sure(ref molaBaslangic, k.Zaman);
                    iceriBaslangic = k.Zaman;
                    break;
                case HareketTipi.Cikis:
                    calisma += Sure(ref iceriBaslangic, k.Zaman);
                    mola += Sure(ref molaBaslangic, k.Zaman);
                    break;
            }
        }

        // Açık kalan dönem şu ana kadar sayılır.
        calisma += Sure(ref iceriBaslangic, simdi);
        mola += Sure(ref molaBaslangic, simdi);
        return new GunOzeti(calisma, mola, ilkGiris);
    }

    private static TimeSpan Sure(ref DateTimeOffset? baslangic, DateTimeOffset bitis)
    {
        if (baslangic is not { } b) return TimeSpan.Zero;
        baslangic = null;
        return bitis > b ? bitis - b : TimeSpan.Zero;
    }
}
