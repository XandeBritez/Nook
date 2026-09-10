namespace Nook;

/// <summary>Estado e regras do pomodoro. Sem dependência de UI: a Window só
/// escuta os eventos e empurra texto pros controles.</summary>
internal sealed class PomodoroService
{
    public string Phase { get; private set; } = "idle"; // idle | focus | break
    public int RemainingSeconds { get; private set; }
    public bool Running { get; private set; }

    /// <summary>Disparado sempre que o rótulo (ícone/tempo) deve ser redesenhado.</summary>
    public event Action? Changed;

    /// <summary>Disparado quando uma fase termina: (título, texto) para o balloon + som.</summary>
    public event Action<string, string>? PhaseEnded;

    /// <summary>Reseta pro estado parado, com duração de foco vinda das settings atuais.</summary>
    public void Reset(int focusMin)
    {
        Running = false;
        Phase = "idle";
        RemainingSeconds = focusMin * 60;
        Changed?.Invoke();
    }

    /// <summary>Settings mudaram: se parado e ocioso, acompanha a nova duração de foco.</summary>
    public void SyncIdleDuration(int focusMin)
    {
        if (Running || Phase != "idle") return;
        RemainingSeconds = focusMin * 60;
        Changed?.Invoke();
    }

    public void Toggle(int focusMin)
    {
        if (Running)
        {
            Running = false;
        }
        else
        {
            if (Phase == "idle") StartPhase("focus", focusMin, 0);
            Running = true;
        }
        Changed?.Invoke();
    }

    public void Tick(int focusMin, int breakMin)
    {
        if (!Running) return;
        RemainingSeconds--;
        if (RemainingSeconds > 0)
        {
            Changed?.Invoke();
            return;
        }

        // Fase acabou: avisa (balloon + som) e já engata a próxima.
        if (Phase == "focus")
        {
            AlertAsync(focusEnded: true);
            PhaseEnded?.Invoke("Hora da pausa! ☕", $"Descanse por {breakMin} min.");
            StartPhase("break", focusMin, breakMin);
        }
        else
        {
            AlertAsync(focusEnded: false);
            PhaseEnded?.Invoke("De volta ao foco! 🍅", $"Foco por {focusMin} min.");
            StartPhase("focus", focusMin, breakMin);
        }
    }

    public string LabelText
    {
        get
        {
            string icon = Phase == "break" ? "☕" : "🍅";
            return $"{icon} {RemainingSeconds / 60:D2}:{RemainingSeconds % 60:D2}";
        }
    }

    private void StartPhase(string phase, int focusMin, int breakMin)
    {
        Phase = phase;
        RemainingSeconds = (phase == "focus" ? focusMin : breakMin) * 60;
        Changed?.Invoke();
    }

    /// <summary>Sons do sistema (sem console): fim do foco = 3x, fim da pausa = 2x.</summary>
    private static void AlertAsync(bool focusEnded) =>
        Task.Run(async () =>
        {
            try
            {
                var sound = focusEnded
                    ? System.Media.SystemSounds.Asterisk
                    : System.Media.SystemSounds.Exclamation;
                int times = focusEnded ? 3 : 2;
                for (int i = 0; i < times; i++)
                {
                    sound.Play();
                    await Task.Delay(300);
                }
            }
            catch { /* sem som, sem problema */ }
        });
}
