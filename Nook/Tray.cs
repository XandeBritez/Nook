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
            Icon = BuildNookIcon(),
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

    /// <summary>Ícone próprio do Nook (raio ⚡ num círculo escuro), gerado em runtime.</summary>
    private static Icon BuildNookIcon()
    {
        try
        {
            const int s = 32;
            using var bmp = new Bitmap(s, s, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                using (var bg = new SolidBrush(Color.FromArgb(0x1E, 0x1E, 0x1E)))
                    g.FillEllipse(bg, 0, 0, s - 1, s - 1);
                using (var border = new Pen(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)))
                    g.DrawEllipse(border, 0, 0, s - 1, s - 1);
                using var font = new Font(GetEmojiFont(), 19, FontStyle.Regular, GraphicsUnit.Pixel);
                using var fg = new SolidBrush(Color.FromArgb(0xFF, 0xD7, 0x00));
                var size = g.MeasureString("⚡", font);
                g.DrawString("⚡", font, fg,
                    (s - size.Width) / 2, (s - size.Height) / 2 - 1);
            }
            IntPtr h = bmp.GetHicon();
            try
            {
                using var tmp = Icon.FromHandle(h);
                return (Icon)tmp.Clone(); // clona: o handle original é destruído abaixo
            }
            finally { Native.DestroyIcon(h); }
        }
        catch
        {
            return SystemIcons.Application; // fallback: nunca quebrar o tray
        }
    }

    private static string GetEmojiFont()
    {
        foreach (string name in new[] { "Segoe UI Emoji", "Segoe UI Symbol" })
        {
            try
            {
                using var f = new FontFamily(name);
                return name;
            }
            catch { /* tenta a próxima */ }
        }
        return "Arial";
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
