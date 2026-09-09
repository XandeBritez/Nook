using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nook;

/// <summary>Persistência JSON compartilhada: um só lugar com options,
/// load-or-default e save tolerante a falha (nunca derruba o app).</summary>
internal static class JsonStore
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Lê <paramref name="path"/> ou cria via <paramref name="factory"/> (e tenta salvar).</summary>
    public static T LoadOrCreate<T>(string path, Func<T> factory) where T : class
    {
        try
        {
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<T>(File.ReadAllText(path), Opts);
                if (loaded != null) return loaded;
            }
        }
        catch { /* cai para o default */ }

        var defaults = factory();
        TrySave(path, defaults);
        return defaults;
    }

    /// <summary>Tenta ler <paramref name="path"/>. Retorna null se ausente/inválido.</summary>
    public static T? TryLoad<T>(string path) where T : class
    {
        try
        {
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Opts);
        }
        catch { return null; }
    }

    /// <summary>Tenta salvar. Retorna false (sem throw) se falhar.</summary>
    public static bool TrySave<T>(string path, T value)
    {
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(value, Opts));
            return true;
        }
        catch { return false; }
    }
}
