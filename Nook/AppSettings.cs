using System.IO;

namespace Nook;
/// <summary>Aparência do Nook persistida em settings.json.</summary>
public sealed class AppSettings
{
    /// <summary>"dark" ou "light".</summary>
    public string Theme { get; set; } = "dark";
    /// <summary>"S", "M" ou "L".</summary>
    public string Size { get; set; } = "M";
    /// <summary>"bottom-right", "bottom-left", "top-right" ou "top-left".</summary>
    public string Corner { get; set; } = "bottom-right";
    /// <summary>Opacidade do fundo do cartão (30–100).</summary>
    public double Opacity { get; set; } = 80;
    /// <summary>Colunas de botões: 1 (lista), 2 (grade), 3 (1x paginada) ou 4 (2x paginada).</summary>
    public int Columns { get; set; } = 1;
    /// <summary>Ícones por página nos modos 3/4. Opcional: 8 ou 16.</summary>
    public int PageSize { get; set; } = 8;
    /// <summary>Atalho global ex.: "Ctrl+Alt+H".</summary>
    public string Hotkey { get; set; } = Nook.Hotkey.Default;
    /// <summary>Manter histórico do clipboard entre sessões (padrão: apaga ao sair).</summary>
    public bool ClipboardPersist { get; set; } = false;
    /// <summary>Pomodoro: minutos de foco / pausa.</summary>
    public int PomodoroFocusMin { get; set; } = 25;
    public int PomodoroBreakMin { get; set; } = 5;
    /// <summary>Mostrar widget do Pomodoro / botão Clips no rodapé.</summary>
    public bool ShowPomodoro { get; set; } = true;
    public bool ShowClips { get; set; } = true;

    public static string JsonPath =>
        Path.Combine(AppContext.BaseDirectory, "settings.json");

    public static AppSettings LoadOrCreateDefault()
    {
        var s = JsonStore.LoadOrCreate(JsonPath, () => new AppSettings());
        s.Normalize();
        return s;
    }

    public void Save()
    {
        Normalize();
        JsonStore.TrySave(JsonPath, this);
    }

    private void Normalize()
    {
        Theme = Theme == "light" ? "light" : "dark";
        Size = Size is "S" or "M" or "L" ? Size : "M";
        Corner = Corner is "bottom-left" or "top-right" or "top-left" ? Corner : "bottom-right";
        Opacity = Math.Clamp(Opacity, 30, 100);
        Columns = Columns is 2 or 3 or 4 ? Columns : 1;
        PageSize = PageSize == 16 ? 16 : 8; // modos 1x/2x: 8 ou 16 botões por página
        if (!Nook.Hotkey.TryParse(Hotkey, out _, out _)) Hotkey = Nook.Hotkey.Default;
        PomodoroFocusMin = Math.Clamp(PomodoroFocusMin, 1, 120);
        PomodoroBreakMin = Math.Clamp(PomodoroBreakMin, 1, 60);
    }
}
