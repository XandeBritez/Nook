using System.Diagnostics;
using System.IO;

namespace Nook;

/// <summary>Abertura de processos compartilhada: sempre UseShellExecute
/// (comportamento de duplo-clique, sem prompt de elevação).</summary>
internal static class ProcessLauncher
{
    /// <summary>Tenta iniciar. Retorna false (sem throw) se falhar.</summary>
    private static bool TryStart(ProcessStartInfo psi)
    {
        try
        {
            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            CrashLog.Write("ProcessLauncher.TryStart", ex);
            return false;
        }
    }

    /// <summary>Inicia ou mostra aviso amigável com o motivo.</summary>
    public static void StartOrWarn(ProcessStartInfo psi, string what)
    {
        try
        {
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Dialogs.Warn($"Falha ao executar '{what}':\n{ex.Message}");
        }
    }

    /// <summary>Abre app/arquivo/pasta como duplo-clique.</summary>
    public static void Open(string file, string args = "", string? workDir = null, bool createNoWindow = false) =>
        StartOrWarn(new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            UseShellExecute = true,
            WorkingDirectory = workDir ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            CreateNoWindow = createNoWindow,
        }, Path.GetFileName(file));

    /// <summary>Tenta PATH e depois System32 (para utilitários do Windows).</summary>
    public static void OpenSystem(string exe, string args = "")
    {
        var psi = new ProcessStartInfo { FileName = exe, Arguments = args, UseShellExecute = true };
        if (TryStart(psi)) return;
        psi.FileName = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System), exe);
        StartOrWarn(psi, exe);
    }
}
