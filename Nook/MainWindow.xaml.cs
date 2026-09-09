using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Nook;

public partial class MainWindow : Window
{
    private const double SideMargin = 12;      // respiro lateral
    private const double VertMargin = 14;      // respiro superior/inferior
    private const double HoverMargin = 8;      // tolerância ao redor da janela
    private const int HoverPollMs = 250; // fallback lento; o caminho rápido é por eventos
    private const int CollapseDelayMs = 350;

    private readonly DispatcherTimer _hoverTimer;
    private readonly DispatcherTimer _tickTimer; // 1s: monitor + pomodoro juntos
    private string _pomoPhase = "idle"; // idle | focus | break
    private int _pomoRemaining;
    private bool _pomoRunning;
    private List<Shortcut> _shortcuts;
    private AppSettings _settings;
    private int _pageIndex; // página atual no modo 1 linha paginada (Columns=3)
    private double _collapsedSize = 34;
    private double _expandedWidth = 112;
    private IntPtr _hwnd;
    private HwndSource? _hwndSource;
    private bool _hotkeyRegistered;
    private bool _clipListening;
    private const int HotkeyId = 0xB001;
    private bool _expanded;
    private bool _pinned;
    private DateTime _collapseAt = DateTime.MinValue;

    public event Action? SettingsRequested;
    public event Action? ClipboardRequested;
    public event Action<string, string>? PomodoroNotify;

    public MainWindow(List<Shortcut> shortcuts, AppSettings settings)
    {
        _shortcuts = shortcuts;
        _settings = settings;
        InitializeComponent();
        ShortcutList.ItemsSource = _shortcuts;
        ApplySettings(_settings);

        // Reancora só quando o tamanho muda de verdade (animação,
        // conteúdo) — bem mais barato que a cada pass de layout.
        SizeChanged += (_, _) => Anchor();

        _hoverTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(HoverPollMs),
            DispatcherPriority.Background,
            OnHoverTick, Dispatcher);
        _tickTimer = new DispatcherTimer(
            TimeSpan.FromSeconds(1),
            DispatcherPriority.Background,
            OnTick, Dispatcher);
        _pomoRemaining = _settings.PomodoroFocusMin * 60;
    }

    /// <summary>Tick único de 1s: monitor (CPU/RAM) + pomodoro.</summary>
    private void OnTick(object? sender, EventArgs e)
    {
        OnMonitorTick(sender, e);
        OnPomoTick(sender, e);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        Native.MakeOverlayNoActivate(_hwnd);
        _hwndSource = HwndSource.FromHwnd(_hwnd);
        _hwndSource?.AddHook(WndProc);
        string? hkErr = TrySetHotkey(_settings.Hotkey);
        if (hkErr != null)
            System.Windows.MessageBox.Show($"Atalho global '{_settings.Hotkey}' indisponível:\n{hkErr}",
                "Nook", MessageBoxButton.OK, MessageBoxImage.Warning);
        ApplyModules(); // visibilidade + listener do clipboard (se habilitado)
        DockToCorner(collapsed: true);
        _hoverTimer.Start();
        _tickTimer.Start();
        SystemEvents.DisplaySettingsChanged += (_, _) =>
        {
            Anchor();
            if (_hwnd != IntPtr.Zero)
                Native.KeepTopMost(_hwnd, (int)Left, (int)Top, (int)ActualWidth, (int)ActualHeight);
        };
    }

    protected override void OnClosed(EventArgs e)
    {
        _hoverTimer.Stop();
        _tickTimer.Stop();
        if (_clipListening)
            ClipboardHistory.Stop(_hwnd, _settings.ClipboardPersist);
        _hwndSource?.RemoveHook(WndProc);
        if (_hotkeyRegistered && _hwnd != IntPtr.Zero)
            Native.UnregisterHotKey(_hwnd, HotkeyId);
        base.OnClosed(e);
    }

    // ---------- hotkey global ----------

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            ToggleViaHotkey();
            handled = true;
        }
        else if (msg == Native.WM_CLIPBOARDUPDATE)
        {
            try { ClipboardHistory.Capture(); } catch { /* nunca derruba o Nook */ }
        }
        return IntPtr.Zero;
    }

    private void ToggleViaHotkey()
    {
        if (_expanded)
        {
            _pinned = false; // solta o pin para o toggle fechar de verdade
            UpdatePinLabel();
            SetExpanded(false);
        }
        else
        {
            PulseExpand(); // fixa aberto; próximo toque fecha
        }
    }

    /// <summary>Troca o atalho global. Retorna null se ok ou a mensagem de erro.</summary>
    public string? TrySetHotkey(string spec)
    {
        if (!Hotkey.TryParse(spec?.Trim(), out uint mod, out uint vk))
            return "Formato inválido. Use algo como Ctrl+Alt+H (com ao menos um modificador).";
        if (_hwnd != IntPtr.Zero)
        {
            if (_hotkeyRegistered)
            {
                Native.UnregisterHotKey(_hwnd, HotkeyId);
                _hotkeyRegistered = false;
            }
            if (!Native.RegisterHotKey(_hwnd, HotkeyId, mod, vk))
                return "Este atalho já está em uso por outro programa.";
            _hotkeyRegistered = true;
        }
        _settings.Hotkey = spec.Trim();
        try { _settings.Save(); } catch { /* roda em memória */ }
        return null;
    }

    public void PulseExpand()
    {
        _pinned = true;
        UpdatePinLabel();
        SetExpanded(true);
    }

    /// <summary>Troca a lista de botões (usado após salvar no editor).</summary>
    public void SetShortcuts(List<Shortcut> shortcuts)
    {
        _shortcuts = shortcuts;
        _pageIndex = 0; // volta à primeira página após edição
        UpdatePagedView();
    }

    /// <summary>Botões visíveis por página nos modos 1x/2x: 8 ou 16 (do settings).</summary>
    private int PagedPageSize => _settings.PageSize == 16 ? 16 : 8;

    /// <summary>Modos com paginação (3 = 1x, 4 = 2x).</summary>
    private bool IsPagedMode => _settings.Columns is 3 or 4;

    /// <summary>Nº de páginas nos modos 1x/2x (8 por página). Sempre ≥ 1.</summary>
    private int PageCount
    {
        get
        {
            if (_shortcuts.Count == 0) return 1;
            return Math.Max(1, (_shortcuts.Count + PagedPageSize - 1) / PagedPageSize);
        }
    }

    /// <summary>Aplica a fatia visível + estado do pager conforme o modo atual.</summary>
    private void UpdatePagedView()
    {
        if (!IsPagedMode)
        {
            // Modos 1/2: lista cheia, sem pager.
            PagerRow.Visibility = Visibility.Collapsed;
            // List<> não notifica: força regeneração reatribuindo.
            ShortcutList.ItemsSource = null;
            ShortcutList.ItemsSource = _shortcuts;
            return;
        }

        _pageIndex = Math.Clamp(_pageIndex, 0, PageCount - 1);
        var page = _shortcuts.Skip(_pageIndex * PagedPageSize).Take(PagedPageSize).ToList();
        ShortcutList.ItemsSource = null;
        ShortcutList.ItemsSource = page;
        PageLabel.Text = $"{_pageIndex + 1}/{PageCount}";
        PagerRow.Visibility = PageCount > 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void GoPage(int delta)
    {
        if (!IsPagedMode || PageCount <= 1) return;
        _pageIndex = (_pageIndex + delta + PageCount) % PageCount; // giro circular
        UpdatePagedView();
    }

    private void PrevPage_Click(object sender, RoutedEventArgs e) => GoPage(-1);

    private void NextPage_Click(object sender, RoutedEventArgs e) => GoPage(1);

    // ---------- drag & drop (.exe/.lnk viram botão) ----------

    private void Panel_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = HasDroppableFile(e) ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void Panel_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] files) return;
        int added = 0;
        foreach (string f in files)
        {
            if (TryMakeShortcut(f) is { } s && !ShortcutExists(s))
            {
                _shortcuts.Add(s);
                added++;
            }
        }
        if (added == 0) return;
        try { ShortcutStore.Save(_shortcuts); } catch { /* roda em memória */ }
        SetShortcuts(_shortcuts);
        PulseExpand(); // mostra o resultado
    }

    private static bool HasDroppableFile(System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)) return false;
        return (e.Data.GetData(System.Windows.DataFormats.FileDrop) as string[])?
            .Any(f => IsDroppableExt(Path.GetExtension(f))) == true;
    }

    private static bool IsDroppableExt(string ext) =>
        ext.Equals(".exe", StringComparison.OrdinalIgnoreCase)
        || ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
        || ext.Equals(".bat", StringComparison.OrdinalIgnoreCase)
        || ext.Equals(".cmd", StringComparison.OrdinalIgnoreCase);

    private bool ShortcutExists(Shortcut s) =>
        _shortcuts.Any(x =>
            string.Equals(x.FileName, s.FileName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Arguments ?? "", s.Arguments ?? "", StringComparison.Ordinal));

    /// <summary>Converte arquivo arrastado em Shortcut. .lnk é resolvido para o destino.</summary>
    public static Shortcut? TryMakeShortcut(string path)
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

    // ---------- aparência + ancoragem (4 cantos) ----------

    /// <summary>Aplica tema/tamanho/canto/opacidade (editor ou startup).</summary>
    public void ApplySettings(AppSettings s)
    {
        _settings = s;
        (_collapsedSize, _expandedWidth) = s.Size switch
        {
            "S" => (28.0, 96.0),
            "L" => (42.0, 140.0),
            _ => (34.0, 112.0),
        };

        ApplyBrushesCore();

        CollapsedTab.Width = CollapsedTab.Height = _collapsedSize;

        // Pomodoro parado acompanha duração configurada.
        if (!_pomoRunning && _pomoPhase == "idle")
        {
            _pomoRemaining = s.PomodoroFocusMin * 60;
            UpdatePomoLabel();
        }

        ApplyLayoutCore();
        UpdatePagedView();

        // Redimensiona o estado atual sem animar (e invalida onDone pendente).
        _animGen++;
        BeginAnimation(WidthProperty, null);
        BeginAnimation(HeightProperty, null);
        if (_expanded)
        {
            Width = _expandedWidth;
            Height = double.NaN;
        }
        else
        {
            Width = _collapsedSize;
            Height = _collapsedSize;
        }
        UpdateLayout();
        Anchor();
        ApplyModules();
    }

    /// <summary>Só brushes de tema/opacidade, sem layout: preview barato do slider.</summary>
    public void ApplyBrushes() => ApplyBrushesCore();

    private void ApplyBrushesCore()
    {
        var s = _settings;
        bool light = s.Theme == "light";
        SetBrush("NookBtnBg", light ? "#FFFFFF" : "#2D2D30");
        SetBrush("NookBtnHover", light ? "#E2E2E2" : "#3E3E42");
        SetBrush("NookBtnPressed", "#007ACC");
        SetBrush("NookFg", light ? "#1A1A1A" : "#FFFFFF");
        SetBrush("NookCardBorder", light ? "#66000000" : "#33FFFFFF");

        // Opacidade só no fundo do cartão (círculo incluso).
        // NOTA: substitui o brush em vez de mudar .Color — após a primeira
        // renderização o WPF congela os brushes do dicionário (read-only).
        byte alpha = (byte)(255 * s.Opacity / 100.0);
        Resources["NookCardBg"] = new SolidColorBrush(light
            ? System.Windows.Media.Color.FromArgb(alpha, 0xF2, 0xF2, 0xF2)
            : System.Windows.Media.Color.FromArgb(alpha, 0x1E, 0x1E, 0x1E));
    }

    /// <summary>Só painel/template por modo (sem resize): usado ao trocar 8/16.</summary>
    public void RefreshLayoutView()
    {
        ApplyLayoutCore();
        UpdatePagedView();
        UpdateLayout();
        Anchor();
    }

    private void ApplyLayoutCore()
    {
        var s = _settings;
        // 1 coluna = lista (ícone + nome); 2 colunas = grade (só ícone);
        // 3 = 1x: 8 botões por página, um abaixo do outro + setas ◀ ▶ por clique.
        // 4 = 2x: 8 botões por página em 2 colunas (4 fileiras) + setas ◀ ▶ por clique.
        if (s.Columns == 3)
        {
            ShortcutList.ItemsPanel = (ItemsPanelTemplate)Resources["Grid1Col"];
            ShortcutList.ItemTemplate = (DataTemplate)Resources["GridButtonTemplate"];
        }
        else if (s.Columns == 4)
        {
            ShortcutList.ItemsPanel = (ItemsPanelTemplate)Resources["Grid2Col"];
            ShortcutList.ItemTemplate = (DataTemplate)Resources["GridButtonTemplate"];
        }
        else
        {
            _pageIndex = 0;
            bool grid = s.Columns == 2;
            ShortcutList.ItemsPanel = (ItemsPanelTemplate)Resources[grid ? "Grid2Col" : "Grid1Col"];
            ShortcutList.ItemTemplate = (DataTemplate)Resources[grid ? "GridButtonTemplate" : "ListButtonTemplate"];
        }
    }

    /// <summary>Mostra/esconde módulos + liga/desliga o listener do clipboard.</summary>
    private void ApplyModules()
    {
        PomoRow.Visibility = _settings.ShowPomodoro ? Visibility.Visible : Visibility.Collapsed;
        ClipsButton.Visibility = _settings.ShowClips ? Visibility.Visible : Visibility.Collapsed;

        if (!_settings.ShowPomodoro && _pomoRunning)
        {
            _pomoRunning = false;
            _pomoPhase = "idle";
            _pomoRemaining = _settings.PomodoroFocusMin * 60;
            PomoButton.Content = "▶";
            UpdatePomoLabel();
        }

        if (_settings.ShowClips && !_clipListening && _hwnd != IntPtr.Zero)
        {
            ClipboardHistory.Start(_hwnd, _settings.ClipboardPersist);
            _clipListening = true;
        }
        else if (!_settings.ShowClips && _clipListening)
        {
            ClipboardHistory.Stop(_hwnd, _settings.ClipboardPersist);
            _clipListening = false;
            try { _clipWin?.Close(); } catch { }
        }
    }

    private void SetBrush(string key, string hex)
    {
        // Substitui (não muta) pelo mesmo motivo do NookCardBg acima.
        Resources[key] = new SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
    }

    private void Anchor()
    {
        var work = SystemParameters.WorkArea; // já desconta a taskbar
        double w = ActualWidth > 0 ? ActualWidth : Width;
        double h = ActualHeight > 0 ? ActualHeight : _collapsedSize;
        bool left = _settings.Corner is "top-left" or "bottom-left";
        bool top = _settings.Corner is "top-left" or "top-right";
        Left = left ? work.Left + SideMargin : work.Right - w - SideMargin;
        Top = top ? work.Top + VertMargin : work.Bottom - h - VertMargin;
    }

    private void DockToCorner(bool collapsed)
    {
        if (collapsed)
        {
            Card.Visibility = Visibility.Collapsed;
            CollapsedTab.Visibility = Visibility.Visible;
            Width = _collapsedSize;
            Height = _collapsedSize;
        }
        else
        {
            Card.Visibility = Visibility.Visible;
            CollapsedTab.Visibility = Visibility.Collapsed;
            Width = _expandedWidth;
            Height = double.NaN; // volta ao SizeToContent
        }
        _expanded = !collapsed;
        UpdateLayout();
        Anchor();
    }

    // ---------- hover / auto-hide (eventos primeiro, timer como fallback) ----------

    /// <summary>Caminho rápido: mouse entrou no cartão → abre na hora.</summary>
    private void Panel_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_pinned) return;
        _collapseAt = DateTime.MinValue;
        if (!_expanded) SetExpanded(true);
    }

    /// <summary>Mouse saiu → arma o colapso com atraso (o timer executa).</summary>
    private void Panel_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_pinned || !_expanded) return;
        if (_collapseAt == DateTime.MinValue)
            _collapseAt = DateTime.UtcNow.AddMilliseconds(CollapseDelayMs);
    }

    private void OnHoverTick(object? sender, EventArgs e)
    {
        if (_pinned) return;
        if (!Native.GetCursorPos(out var pt)) return;

        bool cursorNear =
            pt.X >= Left - HoverMargin && pt.X <= Left + ActualWidth + HoverMargin &&
            pt.Y >= Top - HoverMargin && pt.Y <= Top + ActualHeight + HoverMargin;

        if (cursorNear)
        {
            _collapseAt = DateTime.MinValue;
            if (!_expanded) SetExpanded(true);
        }
        else if (_expanded)
        {
            if (_collapseAt == DateTime.MinValue)
                _collapseAt = DateTime.UtcNow.AddMilliseconds(CollapseDelayMs);
            else if (DateTime.UtcNow >= _collapseAt)
                SetExpanded(false);
        }
    }

    private void SetExpanded(bool expand)
    {
        if (_expanded == expand) return;
        _expanded = expand;

        if (expand)
        {
            // Mostra o conteúdo, mede a altura final e anima
            // largura + altura JUNTAS: abre na diagonal.
            Card.Visibility = Visibility.Visible;
            CollapsedTab.Visibility = Visibility.Collapsed;
            Card.Measure(new System.Windows.Size(_expandedWidth, double.PositiveInfinity));
            double targetH = Math.Max(Card.DesiredSize.Height, _collapsedSize);
            AnimateSize(_expandedWidth, targetH, onDone: () =>
            {
                if (!_expanded) return;
                Height = double.NaN; // volta a acompanhar o conteúdo
            });
        }
        else
        {
            // Congela a altura atual e encolhe os dois eixos juntos:
            // fecha na diagonal em direção ao círculo.
            Height = ActualHeight;
            AnimateSize(_collapsedSize, _collapsedSize, onDone: () =>
            {
                if (_expanded) return; // reabriu no meio da animação
                Card.Visibility = Visibility.Collapsed;
                CollapsedTab.Visibility = Visibility.Visible;
                Height = _collapsedSize;
                Anchor();
            });
        }
    }

    private int _animGen; // invalida onDone de animação superada por hover rápido

    private void AnimateSize(double targetW, double targetH, Action? onDone)
    {
        // Mesmo easing e duração nos dois eixos = movimento diagonal reto.
        int gen = ++_animGen;
        var dur = new Duration(TimeSpan.FromMilliseconds(300));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var animW = new DoubleAnimation(Width, targetW, dur) { EasingFunction = ease };
        var animH = new DoubleAnimation(Height, targetH, dur) { EasingFunction = ease };
        if (onDone != null)
        {
            int pending = 2;
            void Done(object? s, EventArgs e)
            {
                // Animação trocada no meio do caminho (ou cancelada):
                // o onDone da animação atual assume o estado final.
                if (--pending == 0 && gen == _animGen) onDone();
            }
            animW.Completed += Done;
            animH.Completed += Done;
        }
        // LayoutUpdated ancora Left/Top a cada frame: o canto inferior
        // direito fica parado e o movimento sai na diagonal.
        BeginAnimation(WidthProperty, animW);
        BeginAnimation(HeightProperty, animH);
    }

    private void UpdatePinLabel() =>
        PinButton.Content = _pinned ? "📍" : "📌";

    // ---------- UI events ----------

    private void ShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: Shortcut s })
            Actions.Launch(s);
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        _pinned = !_pinned;
        UpdatePinLabel();
        SetExpanded(_pinned);
    }

    private void GearButton_Click(object sender, RoutedEventArgs e) =>
        SettingsRequested?.Invoke();

    private ClipboardWindow? _clipWin;

    /// <summary>Abre (ou foca) a janela do histórico. Instância única.</summary>
    public void OpenClipboard()
    {
        if (!_settings.ShowClips) return;
        try
        {
            if (_clipWin != null)
            {
                if (!_clipWin.IsVisible) _clipWin.Show();
                _clipWin.Activate();
                _clipWin.FocusSearch();
                return;
            }
            _clipWin = new ClipboardWindow(_settings) { Owner = this };
            _clipWin.Closed += (_, _) => _clipWin = null;
            _clipWin.Show();
            _clipWin.FocusSearch();
        }
        catch (Exception ex)
        {
            CrashLog.Write("OpenClipboard", ex);
        }
    }

    private void TurboButton_Click(object sender, RoutedEventArgs e) =>
        _ = Turbo.RunAsync(); // roda em background; avisa ao concluir

    private void ClipsButton_Click(object sender, RoutedEventArgs e) =>
        ClipboardRequested?.Invoke();

    // ---------- pomodoro ----------

    private void PomoButton_Click(object sender, RoutedEventArgs e)
    {
        if (_pomoRunning)
        {
            _pomoRunning = false;
            PomoButton.Content = "▶";
        }
        else
        {
            if (_pomoPhase == "idle") StartPomoPhase("focus");
            _pomoRunning = true;
            PomoButton.Content = "⏸";
        }
    }

    private void PomoLabel_Reset(object sender, MouseButtonEventArgs e)
    {
        _pomoRunning = false;
        _pomoPhase = "idle";
        _pomoRemaining = _settings.PomodoroFocusMin * 60;
        PomoButton.Content = "▶";
        UpdatePomoLabel();
    }

    private void StartPomoPhase(string phase)
    {
        _pomoPhase = phase;
        _pomoRemaining = (phase == "focus"
            ? _settings.PomodoroFocusMin
            : _settings.PomodoroBreakMin) * 60;
        UpdatePomoLabel();
    }

    private void OnPomoTick(object? sender, EventArgs e)
    {
        if (!_pomoRunning) return;
        if (--_pomoRemaining > 0) { UpdatePomoLabel(); return; }

        // Fase acabou: avisa (balloon + som) e já engata a próxima.
        if (_pomoPhase == "focus")
        {
            AlertAsync(focusEnded: true);
            PomodoroNotify?.Invoke("Hora da pausa! ☕",
                $"Descanse por {_settings.PomodoroBreakMin} min.");
            StartPomoPhase("break");
        }
        else
        {
            AlertAsync(focusEnded: false);
            PomodoroNotify?.Invoke("De volta ao foco! 🍅",
                $"Foco por {_settings.PomodoroFocusMin} min.");
            StartPomoPhase("focus");
        }
    }

    private void UpdatePomoLabel()
    {
        string icon = _pomoPhase == "break" ? "☕" : "🍅";
        PomoLabel.Text = $"{icon} {_pomoRemaining / 60:D2}:{_pomoRemaining % 60:D2}";
    }

    /// <summary>Sons do sistema (sem console): fim do foco = 3x, fim da pausa = 2x.</summary>
    private static void AlertAsync(bool focusEnded) =>
        Task.Run(async () =>
        {
            try
            {
                var sound = focusEnded
                    ? System.Media.SystemSounds.Asterisk
                    : System.Media.SystemSounds.Exclamation;
                int times = focusEnded ? 3 : 2;
                for (int i = 0; i < times; i++)
                {
                    sound.Play();
                    await Task.Delay(300);
                }
            }
            catch { /* sem som, sem problema */ }
        });

    private void OnMonitorTick(object? sender, EventArgs e)
    {
        if (!_expanded) return; // colapsado: ninguém vê, não gasta ciclo
        double cpu = Monitor.SampleCpu();
        double ram = Monitor.RamPercent();
        CpuBar.Value = cpu;
        RamBar.Value = ram;
        CpuLabel.Text = $"{cpu:F0}%";
        RamLabel.Text = $"{ram:F0}%";
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e) =>
        System.Windows.Application.Current.Shutdown();
}
