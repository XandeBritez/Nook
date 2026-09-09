using System.IO;

namespace Nook;

/// <summary>Modelo de atalho editável via shortcuts.json.</summary>
public sealed class Shortcut
{
    public string Name { get; set; } = string.Empty;
    /// <summary>Emoji ou letra exibida no botão (ex.: 🌐).</summary>
    public string Icon { get; set; } = "•";
    public string? FileName { get; set; }
    public string? Arguments { get; set; }
    public string? WorkingDirectory { get; set; }
    public List<string>? FallbackPaths { get; set; }
    /// <summary>Ação embutida. Valores: lock, mute, volup, voldown,
    /// screenshot, taskmgr, darkmode, recycle, sleep, turbo, reboot, shutdown.
    /// Nulo = lançar FileName.</summary>
    public string? Builtin { get; set; }
    public string? Tooltip { get; set; }
}

internal static class ShortcutStore
{
    public static string JsonPath =>
        Path.Combine(AppContext.BaseDirectory, "shortcuts.json");

    public static List<Shortcut> LoadOrCreateDefault()
    {
        var list = JsonStore.TryLoad<List<Shortcut>>(JsonPath);
        if (list is { Count: > 0 }) return list;
        var defaults = DefaultShortcuts();
        JsonStore.TrySave(JsonPath, defaults);
        return defaults;
    }

    public static void Save(List<Shortcut> list) =>
        JsonStore.TrySave(JsonPath, list);

    /// <summary>Ações embutidas disponíveis no editor (valor → descrição).</summary>
    public static IReadOnlyList<(string Value, string Label)> BuiltinOptions { get; } = new[]
    {
        ("lock", "🔒 Bloquear o PC"),
        ("mute", "🔇 Mudo"),
        ("volup", "🔊 Volume +"),
        ("voldown", "🔉 Volume −"),
        ("screenshot", "📸 Print da tela"),
        ("taskmgr", "📋 Gerenciador de tarefas"),
        ("darkmode", "🌙 Alternar tema escuro/claro"),
        ("recycle", "🗑️ Esvaziar lixeira"),
        ("sleep", "💤 Suspender o PC"),
        ("turbo", "🚀 Turbo (limpar RAM)"),
        ("reboot", "🔁 Reiniciar o PC"),
        ("shutdown", "⏻ Desligar o PC"),
    };

    public static List<Shortcut> DefaultShortcuts() => new()
    {
        new Shortcut
        {
            Name = "Chrome",
            Icon = "🌐",
            FileName = "chrome.exe",
            FallbackPaths = new()
            {
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
                @"%LocalAppData%\Google\Chrome\Application\chrome.exe",
            },
            Tooltip = "Abrir Google Chrome",
        },
        new Shortcut
        {
            Name = "VS Code",
            Icon = "🧩",
            FileName = "code.exe",
            FallbackPaths = new()
            {
                @"%LocalAppData%\Programs\Microsoft VS Code\Code.exe",
                @"C:\Program Files\Microsoft VS Code\Code.exe",
            },
            Tooltip = "Abrir Visual Studio Code",
        },
        new Shortcut
        {
            Name = "OpenCode",
            Icon = "⌨️",
            FileName = "cmd.exe",
            Arguments = "/k opencode",
            WorkingDirectory = "%USERPROFILE%",
            Tooltip = "Abrir OpenCode no terminal",
        },
        new Shortcut
        {
            Name = "Bloquear",
            Icon = "🔒",
            Builtin = "lock",
            Tooltip = "Bloquear o PC (Win+L)",
        },
        new Shortcut
        {
            Name = "Mudo",
            Icon = "🔇",
            Builtin = "mute",
            Tooltip = "Ativar/desativar mudo",
        },
        new Shortcut
        {
            Name = "Vol +",
            Icon = "🔊",
            Builtin = "volup",
            Tooltip = "Aumentar volume",
        },
        new Shortcut
        {
            Name = "Vol −",
            Icon = "🔉",
            Builtin = "voldown",
            Tooltip = "Diminuir volume",
        },
        new Shortcut
        {
            Name = "Print",
            Icon = "📸",
            Builtin = "screenshot",
            Tooltip = "Capturar tela e abrir no Paint",
        },
        new Shortcut
        {
            Name = "Tarefas",
            Icon = "📋",
            Builtin = "taskmgr",
            Tooltip = "Abrir Gerenciador de tarefas",
        },
        new Shortcut
        {
            Name = "Tema",
            Icon = "🌙",
            Builtin = "darkmode",
            Tooltip = "Alternar modo escuro/claro",
        },
        new Shortcut
        {
            Name = "Lixeira",
            Icon = "🗑️",
            Builtin = "recycle",
            Tooltip = "Esvaziar a lixeira (pede confirmação)",
        },
        new Shortcut
        {
            Name = "Suspender",
            Icon = "💤",
            Builtin = "sleep",
            Tooltip = "Suspender o PC (pede confirmação)",
        },
        new Shortcut
        {
            Name = "Turbo",
            Icon = "🚀",
            Builtin = "turbo",
            Tooltip = "Encerrar helpers e compactar RAM (pede confirmação)",
        },
        new Shortcut
        {
            Name = "Reiniciar",
            Icon = "🔁",
            Builtin = "reboot",
            Tooltip = "Reiniciar o PC em 30s (pede confirmação, cancelável)",
        },
        new Shortcut
        {
            Name = "Desligar",
            Icon = "⏻",
            Builtin = "shutdown",
            Tooltip = "Desligar o PC em 30s (pede confirmação, cancelável)",
        },
    };
}
