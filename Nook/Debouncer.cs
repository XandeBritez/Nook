using System.Windows.Threading;

namespace Nook;

/// <summary>Coalesce chamadas frequentes (slider, busca, persist): só executa
/// a última após <paramref name="delay"/> sem novas chamadas. Roda na UI thread.</summary>
internal sealed class Debouncer
{
    private readonly DispatcherTimer _timer;
    private Action? _pending;

    public Debouncer(TimeSpan delay)
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = delay,
        };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            var action = _pending;
            _pending = null;
            action?.Invoke();
        };
    }

    public void Call(Action action)
    {
        _pending = action;
        _timer.Stop();
        _timer.Start();
    }
}
