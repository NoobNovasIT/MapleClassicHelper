using MapleHelper.Native;

namespace MapleHelper.App.Services;

/// <summary>
/// Chỉ cho chạy một instance. Mở app lần hai sẽ báo instance đang chạy hiện cửa sổ chính rồi thoát.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activateSignal;
    private readonly RegisteredWaitHandle _registration;

    private SingleInstance(Mutex mutex, string name)
    {
        _mutex = mutex;
        _activateSignal = new EventWaitHandle(false, EventResetMode.AutoReset, SignalName(name));
        _registration = ThreadPool.RegisterWaitForSingleObject(_activateSignal,
            (_, _) => ActivationRequested?.Invoke(this, EventArgs.Empty),
            null, Timeout.Infinite, executeOnlyOnce: false);
    }

    /// <summary>Phát ra trên thread pool, nơi nhận phải tự marshal về UI thread.</summary>
    public event EventHandler? ActivationRequested;

    public static SingleInstance? TryAcquire(string name)
    {
        var mutex = new Mutex(initiallyOwned: true, $@"Local\{name}.Mutex", out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            return null;
        }
        return new SingleInstance(mutex, name);
    }

    public static void SignalExisting(string name)
    {
        ForegroundWindow.AllowAnyProcess();
        if (EventWaitHandle.TryOpenExisting(SignalName(name), out var signal))
        {
            using (signal)
            {
                signal.Set();
            }
        }
    }

    public void Dispose()
    {
        _registration.Unregister(null);
        _activateSignal.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }

    private static string SignalName(string name) => $@"Local\{name}.Activate";
}
