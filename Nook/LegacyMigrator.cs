using System.IO;

namespace Nook;

/// <summary>Migração one-time do nome antigo (HubApp → Nook).
/// Copia os .json e preserva o autostart. Silenciosa e à prova de falha:
/// nunca trava o boot.</summary>
internal static class LegacyMigrator
{
    private static readonly string[] DataFiles =
        new[] { "settings.json", "shortcuts.json", "turbo.json", "clipboard.json" };

    public static void MigrateAll()
    {
        MigrateData();
        Autostart.MigrateLegacy();
    }

    /// <summary>Copia os .json da pasta antiga (HubApp/publish) se não
    /// existirem ao lado do Nook.exe. Copia por arquivo: aproveita o que achar.</summary>
    public static void MigrateData()
    {
        try
        {
            string current = AppContext.BaseDirectory;
            // BaseDirectory termina com separador ("...\\publish\\") e o
            // GetParent NÃO o ignora (retornaria o próprio dir). Normaliza antes.
            current = current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string? hubRoot = Directory.GetParent(current)?.Parent?.FullName;
            if (hubRoot == null) return;
            string legacy = Path.Combine(hubRoot, "HubApp", "publish");
            if (!Directory.Exists(legacy)) return;

            foreach (string name in DataFiles)
            {
                try
                {
                    string dest = Path.Combine(current, name);
                    string src = Path.Combine(legacy, name);
                    if (!File.Exists(dest) && File.Exists(src))
                        File.Copy(src, dest);
                }
                catch { /* tenta o próximo arquivo */ }
            }
        }
        catch { /* sem migração, o app cria defaults */ }
    }
}
