using System.Diagnostics;
using System.IO;

namespace Nook;

/// <summary>Config do Turbo em turbo.json (editável à mão).</summary>
public sealed class TurboConfig
{
    public bool Confirm { get; set; } = true;
    public bool TrimMemory { get; set; } = true;
    /// <summary>Nomes de processo SEM .exe (ex.: "AdobeARM"). Só a sessão atual.</summary>
    public List<string> Processes { get; set; } = new();
}

internal static class Turbo
{
    public static string JsonPath =>
        Path.Combine(AppContext.BaseDirectory, "turbo.json");
    public static TurboConfig LoadOrCreateDefault() =>
        JsonStore.LoadOrCreate(JsonPath, () => new TurboConfig
        {
            // Helpers conhecidos por gastar RAM à toa. Conservador de propósito.
            Processes = new() { "AdobeARM", "OneDriveSetup", "CCXProcess" },
        });

    public static async Task RunAsync()
    {
        var cfg = LoadOrCreateDefault();

        if (cfg.Confirm)
        {
            if (!Dialogs.Confirm(
                "Turbo: encerrar helpers da lista e compactar memória dos apps?",
                "Nook — Turbo")) return;
        }

        // Trabalho pesado fora da UI thread; o await volta para a UI no fim.
        var result = await Task.Run(() =>
        {
            int ownId = Environment.ProcessId;
            int ownSession = Process.GetCurrentProcess().SessionId;
            double freeBefore = Monitor.FreeGb();
            int killed = 0, trimmed = 0;

            // 1) Encerra só processos da lista, só da sessão atual, nunca a si mesmo.
            foreach (string raw in cfg.Processes)
            {
                string name = raw.Trim().Trim('"');
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    name = name[..^4];
                if (name.Length == 0) continue;
                Process[] found;
                try { found = Process.GetProcessesByName(name); }
                catch { continue; }
                foreach (var p in found)
                {
                    try
                    {
                        using (p)
                        {
                            if (p.Id == ownId || p.SessionId != ownSession) continue;
                            p.Kill();
                            killed++;
                        }
                    }
                    catch { /* acesso negado / já saiu — ignora */ }
                }
            }

            // 2) Compacta working set (seguro: SO traz páginas de volta sob demanda).
            if (cfg.TrimMemory)
            {
                foreach (var p in Process.GetProcesses())
                {
                    try
                    {
                        using (p)
                        {
                            if (p.Id == ownId || p.SessionId != ownSession) continue;
                            if (Native.EmptyWorkingSet(p.Handle)) trimmed++;
                        }
                    }
                    catch { /* sistema/protegidos — ignora */ }
                }
            }

            double freeAfter = Monitor.FreeGb();
            return (killed, trimmed, freeBefore, freeAfter);
        });

        Dialogs.Info(
            $"Turbo concluído:\n• {result.killed} processo(s) encerrado(s)\n• {result.trimmed} processo(s) compactado(s)\n• RAM livre: {result.freeBefore:F1} → {result.freeAfter:F1} GB",
            "Nook — Turbo");
    }
}
