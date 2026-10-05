namespace PdksMasaustu.Altyapi;

/// <summary>
/// Uygulamanın kullanıcı oturumu başına tek örnek çalışmasını sağlar. İkinci
/// örnek açılmak istenirse çalışan örneğe sinyal gönderip kapanır; çalışan
/// örnek penceresini öne getirir.
/// </summary>
internal sealed class TekOrnek : IDisposable
{
    private const string Onek = @"Local\PdksMasaustu";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _sinyal;
    private readonly CancellationTokenSource _iptal = new();

    public bool IlkOrnekMi { get; }

    /// <summary>Başka bir örnek açılmaya çalışıldığında (arka plan iş parçacığından) tetiklenir.</summary>
    public event Action? Uyandirildi;

    /// <param name="kip">Demo kendi kilidini kullanır; gerçek kurulum açıkken de çalışabilir.</param>
    public TekOrnek(string kip = "")
    {
        _mutex = new Mutex(initiallyOwned: true, $"{Onek}{kip}.TekOrnek", out var yeniOlusturuldu);
        IlkOrnekMi = yeniOlusturuldu;
        _sinyal = new EventWaitHandle(false, EventResetMode.AutoReset, $"{Onek}{kip}.Uyandir");

        if (IlkOrnekMi)
        {
            new Thread(Dinle) { IsBackground = true, Name = "TekOrnek" }.Start();
        }
    }

    public void DigerOrnegiUyandir() => _sinyal.Set();

    private void Dinle()
    {
        var bekle = new WaitHandle[] { _sinyal, _iptal.Token.WaitHandle };
        while (WaitHandle.WaitAny(bekle) == 0)
        {
            Uyandirildi?.Invoke();
        }
    }

    public void Dispose()
    {
        _iptal.Cancel();
        if (IlkOrnekMi) _mutex.ReleaseMutex();
        _mutex.Dispose();
        _sinyal.Dispose();
    }
}
