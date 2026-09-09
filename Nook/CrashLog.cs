using System.IO;
using System.Text;

namespace Nook;

/// <summary>Grava exceções fatais num .log ao lado do exe (e no TEMP como fallback).</summary>
internal static class CrashLog
{
    public static string Path { get; } = ResolvePath();

    private static string ResolvePath()
    {
        try
        {
            return System.IO.Path.Combine(AppContext.BaseDirectory, "Nook_crash.log");
        }
        catch
        {
            return System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Nook_crash.log");
        }
    }

    public static void Write(string stage, Exception? ex)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {stage}");
            sb.AppendLine(ex?.ToString() ?? "(sem exceção)");
            sb.AppendLine(new string('-', 60));
            File.AppendAllText(Path, sb.ToString());
        }
        catch { /* último recurso: nunca derrubar o app por causa do log */ }
    }
}
