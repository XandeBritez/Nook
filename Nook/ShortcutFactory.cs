using System.IO;
using System.Reflection;

namespace Nook;

/// <summary>Converte arquivos soltos (drag&amp;drop) em Shortcut. Sem UI: puro domínio.</summary>
internal static class ShortcutFactory
{
    public static bool IsDroppableExt(string ext) =>
        ext.Equals(".exe", StringComparison.OrdinalIgnoreCase)
        || ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
        || ext.Equals(".bat", StringComparison.OrdinalIgnoreCase)
        || ext.Equals(".cmd", StringComparison.OrdinalIgnoreCase);

    /// <summary>Converte arquivo arrastado em Shortcut. .lnk é resolvido para o destino.</summary>
    public static Shortcut? TryCreate(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !IsDroppableExt(Path.GetExtension(path)))
            return null;
        string name = Path.GetFileNameWithoutExtension(path);
        string file = path, args = "", work = "";

        if (Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase)
            && ResolveLnk(path) is { } r)
        {
            if (!string.IsNullOrWhiteSpace(r.Target)) file = r.Target;
            args = r.Args;
            work = r.WorkDir;
        }

        return new Shortcut
        {
            Name = string.IsNullOrWhiteSpace(name) ? "App" : name,
            Icon = "📦", // usuário ajusta pela ⚙️
            FileName = file,
            Arguments = string.IsNullOrWhiteSpace(args) ? null : args,
            WorkingDirectory = string.IsNullOrWhiteSpace(work) ? null : work,
            Tooltip = $"Abrir {name}",
        };
    }

    private static (string Target, string Args, string WorkDir)? ResolveLnk(string lnkPath)
    {
        // Sem dependências: WScript.Shell via COM (presente em todo Windows).
        object? shell = null;
        object? sc = null;
        try
        {
            Type? t = Type.GetTypeFromProgID("WScript.Shell");
            if (t == null) return null;
            shell = Activator.CreateInstance(t);
            if (shell == null) return null;
            sc = shell.GetType().InvokeMember("CreateShortcut",
                BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
            if (sc == null) return null;
            string target = sc.GetType().InvokeMember("TargetPath",
                BindingFlags.GetProperty, null, sc, null) as string ?? "";
            string a = sc.GetType().InvokeMember("Arguments",
                BindingFlags.GetProperty, null, sc, null) as string ?? "";
            string w = sc.GetType().InvokeMember("WorkingDirectory",
                BindingFlags.GetProperty, null, sc, null) as string ?? "";
            // Alvo vazio = .lnk quebrado: chamador usa o próprio .lnk (abre pelo shell).
            return (target, a, w);
        }
        catch { return null; }
        finally
        {
            // Libera os RCWs para não vazar COM a cada arrasto.
            if (sc != null)
                try { System.Runtime.InteropServices.Marshal.ReleaseComObject(sc); } catch { }
            if (shell != null)
                try { System.Runtime.InteropServices.Marshal.ReleaseComObject(shell); } catch { }
        }
    }
}
