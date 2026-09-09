using System.Drawing;
using WinForms = System.Windows.Forms;

namespace Nook;

/// <summary>Ícone da bandeja (tray) com menu Sair / Autostart.</summary>
internal sealed class Tray : IDisposable
{
    private readonly WinForms.NotifyIcon _icon;
    private readonly WinForms.ToolStripMenuItem _autostartItem;
    private readonly WinForms.ToolStripMenuItem _clipsItem;

    public event Action? OpenRequested;
    public event Action? EditRequested;
    public event Action? ClipboardRequested;
    public event Action? AboutRequested;

    public Tray()
    {
        _icon = new WinForms.NotifyIcon
        {
            Text = "Nook — atalhos",
            Icon = LoadAppIcon(),
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => OpenRequested?.Invoke();

        var menu = new WinForms.ContextMenuStrip();
        var open = new WinForms.ToolStripMenuItem("Abrir Nook", null,
            (_, _) => OpenRequested?.Invoke());
        var edit = new WinForms.ToolStripMenuItem("Editar atalhos…", null,
            (_, _) => EditRequested?.Invoke());
        var clips = new WinForms.ToolStripMenuItem("📋 Área de transferência", null,
            (_, _) => ClipboardRequested?.Invoke());
        _clipsItem = clips;
        _autostartItem = new WinForms.ToolStripMenuItem("Iniciar com o Windows")
        { Checked = Autostart.IsEnabled(), CheckOnClick = false };
        _autostartItem.Click += (_, _) =>
        {
            Autostart.SetEnabled(!Autostart.IsEnabled());
            _autostartItem.Checked = Autostart.IsEnabled();
        };
        var openFolder = new WinForms.ToolStripMenuItem("Abrir pasta (editar shortcuts.json)",
            null, (_, _) => ProcessLauncher.Open(AppContext.BaseDirectory));
        var about = new WinForms.ToolStripMenuItem("Sobre o Nook", null,
            (_, _) => AboutRequested?.Invoke());
        var exit = new WinForms.ToolStripMenuItem("Sair", null,
            (_, _) => System.Windows.Application.Current.Shutdown());

        menu.Items.Add(open);
        menu.Items.Add(edit);
        menu.Items.Add(clips);
        menu.Items.Add(_autostartItem);
        menu.Items.Add(openFolder);
        menu.Items.Add(about);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(exit);
        _icon.ContextMenuStrip = menu;
    }

    /// <summary>Ícone oficial (icon.ico embutido). Fallback: ícone padrão.</summary>
    private static Icon LoadAppIcon()
    {
        try
        {
            var stream = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/icon.ico"))?.Stream;
            if (stream != null)
            {
                using (stream) return new Icon(stream);
            }
        }
        catch { /* cai para o fallback */ }
        return SystemIcons.Application; // nunca quebra o tray
    }

    public void RefreshAutostart() => _autostartItem.Checked = Autostart.IsEnabled();

    public void SetClipsVisible(bool visible) => _clipsItem.Visible = visible;

    /// <summary>Aviso no canto (não rouba foco) — usado pelo Pomodoro.</summary>
    public void ShowBalloon(string title, string text)
    {
        try { _icon.ShowBalloonTip(4000, title, text, WinForms.ToolTipIcon.Info); }
        catch { /* balloon é melhor-esforço */ }
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
