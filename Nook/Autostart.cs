using Microsoft.Win32;
using System.IO;

namespace Nook;

/// <summary>Auto-start via HKCU (não pede admin).</summary>
internal static class Autostart
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Nook";
    private const string LegacyValueName = "HubApp";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            var v = key?.GetValue(ValueName) as string;
            if (string.IsNullOrEmpty(v)) return false;
            return v.Contains(AppContext.BaseDirectory.TrimEnd('\\', '/'),
                StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true)
                ?? Registry.CurrentUser.CreateSubKey(RunKey);
            if (key == null) return;
            if (enabled)
            {
                string exe = Path.Combine(AppContext.BaseDirectory, "Nook.exe");
                key.SetValue(ValueName, $"\"{exe}\" --minimized", RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, false);
            }
        }
        catch { /* silencioso: sem elevação não há o que fazer além de HKCU */ }
    }

    /// <summary>Migração one-time: remove o valor legado "HubApp" (que apontaria
    /// para um exe que não existe mais) e preserva o comportamento — se ligava
    /// antes, liga o novo.</summary>
    public static void MigrateLegacy()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (key == null) return;
            var old = key.GetValue(LegacyValueName) as string;
            if (string.IsNullOrEmpty(old)) return;
            key.DeleteValue(LegacyValueName, false);
            if (!IsEnabled()) SetEnabled(true);
        }
        catch { /* silencioso */ }
    }
}
