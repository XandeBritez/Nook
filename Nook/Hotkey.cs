using System.Windows.Input;

namespace Nook;

/// <summary>Parse de atalho global estilo "Ctrl+Alt+H". Exige ao menos 1 modificador.</summary>
public static class Hotkey
{
    public const string Default = "Ctrl+Alt+H";

    public static bool TryParse(string? spec, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;
        if (string.IsNullOrWhiteSpace(spec)) return false;

        string[] parts = spec.Split('+',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return false; // sem modificador, sem registro

        foreach (string p in parts[..^1])
        {
            switch (p.ToLowerInvariant())
            {
                case "ctrl":
                case "control": modifiers |= Native.MOD_CONTROL; break;
                case "alt": modifiers |= Native.MOD_ALT; break;
                case "shift": modifiers |= Native.MOD_SHIFT; break;
                case "win":
                case "windows": modifiers |= Native.MOD_WIN; break;
                default: return false;
            }
        }
        if (modifiers == 0) return false;

        try
        {
            var key = (Key)new KeyConverter().ConvertFromString(parts[^1]);
            if (key == Key.None) return false;
            int v = KeyInterop.VirtualKeyFromKey(key);
            if (v <= 0 || v > 255) return false;
            vk = (uint)v;
            return true;
        }
        catch { return false; }
    }
}
