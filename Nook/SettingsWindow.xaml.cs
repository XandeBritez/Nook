using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;

namespace Nook;

/// <summary>Editor visual dos atalhos. Edita uma cópia; Salvar persiste no JSON.</summary>
public partial class SettingsWindow : Window
{
    private readonly ObservableCollection<Shortcut> _items;
    private readonly AppSettings _settings;
    private readonly Action _applyAppearance;
    private readonly Action _applyBrushes;
    private readonly Action _applyLayout;
    private readonly Func<string, string?> _tryHotkey;
    private readonly Debouncer _opacitySaver = new(TimeSpan.FromMilliseconds(500));
    // Começa true: o WPF dispara ValueChanged/SelectionChanged DURANTE o
    // InitializeComponent (ex.: Slider ao aplicar Minimum/Maximum), quando
    // os controles ainda nem existem. Só libera no fim do construtor.
    private bool _loading = true;

    public SettingsWindow(List<Shortcut> current, AppSettings settings, Action applyAppearance, Action applyBrushes, Action applyLayout, Func<string, string?> tryHotkey)
    {
        _settings = settings;
        _applyAppearance = applyAppearance;
        _applyBrushes = applyBrushes;
        _applyLayout = applyLayout;
        _tryHotkey = tryHotkey;
        InitializeComponent();

        // Cópia de trabalho: Cancelar descarta tudo.
        _items = new ObservableCollection<Shortcut>(
            current.Select(s => new Shortcut
            {
                Name = s.Name,
                Icon = s.Icon,
                FileName = s.FileName,
                Arguments = s.Arguments,
                WorkingDirectory = s.WorkingDirectory,
                FallbackPaths = s.FallbackPaths?.ToList(),
                Builtin = s.Builtin,
                Tooltip = s.Tooltip,
            }));

        BuiltinBox.ItemsSource = ShortcutStore.BuiltinOptions.Select(o => o.Label).ToList();
        IconBox.ItemsSource = EmojiGallery.All;
        ShortcutListBox.ItemsSource = _items;
        if (_items.Count > 0)
            ShortcutListBox.SelectedIndex = 0;
        StatusText.Text = $"{_items.Count} botões";

        // Aba Aparência reflete o settings atual.
        _loading = true;
        try
        {
            ThemeBox.SelectedIndex = _settings.Theme == "light" ? 1 : 0;
            SizeBox.SelectedIndex = _settings.Size switch { "S" => 0, "L" => 2, _ => 1 };
            CornerBox.SelectedIndex = _settings.Corner switch
            {
                "bottom-left" => 1, "top-right" => 2, "top-left" => 3, _ => 0,
            };
            ColumnsBox.SelectedIndex = _settings.Columns switch { 2 => 1, 3 => 2, 4 => 3, _ => 0 };
            PageSizeBox.SelectedIndex = _settings.PageSize == 16 ? 1 : 0;
            OpacitySlider.Value = _settings.Opacity;
            OpacityLabel.Text = $"{(int)_settings.Opacity}%";
            HotkeyBox.Text = _settings.Hotkey;
            FocusBox.Text = _settings.PomodoroFocusMin.ToString();
            BreakBox.Text = _settings.PomodoroBreakMin.ToString();
            ShowPomoCheck.IsChecked = _settings.ShowPomodoro;
            ShowClipsCheck.IsChecked = _settings.ShowClips;
            try
            {
                var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                VersionText.Text = "v" + (v != null ? $"{v.Major}.{v.Minor}.{v.Build}" : "1.1.0");
            }
            catch { VersionText.Text = "v1.1.0"; }
        }
        finally { _loading = false; }
    }

    /// <summary>Abre o editor direto na aba Sobre (usado pelo tray).</summary>
    public void ShowAbout() => MainTabs.SelectedIndex = 2;

    private void RepoLink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        ProcessLauncher.Open(e.Uri.AbsoluteUri);
        e.Handled = true;
    }

    public List<Shortcut> UpdatedItems => _items.ToList();

    private Shortcut? Selected => ShortcutListBox.SelectedItem as Shortcut;

    // ---------- lista ----------

    private void ShortcutListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        LoadSelected();

    private void LoadSelected()
    {
        var s = Selected;
        _loading = true;
        try
        {
            bool has = s != null;
            NameBox.Text = s?.Name ?? string.Empty;
            IconBox.Text = s?.Icon ?? string.Empty;
            TooltipBox.Text = s?.Tooltip ?? string.Empty;

            bool isBuiltin = !string.IsNullOrWhiteSpace(s?.Builtin);
            TypeBox.SelectedIndex = isBuiltin ? 1 : 0;

            FileBox.Text = s?.FileName ?? string.Empty;
            ArgsBox.Text = s?.Arguments ?? string.Empty;
            WorkDirBox.Text = s?.WorkingDirectory ?? string.Empty;
            FallbackBox.Text = s?.FallbackPaths != null
                ? string.Join(Environment.NewLine, s.FallbackPaths)
                : string.Empty;

            int bi = ShortcutStore.BuiltinOptions
                .Select((o, i) => (o, i))
                .FirstOrDefault(x => x.o.Value == s?.Builtin).i;
            BuiltinBox.SelectedIndex = s?.Builtin != null ? bi : 0;

            ProgramPanel.Visibility = isBuiltin ? Visibility.Collapsed : Visibility.Visible;
            BuiltinPanel.Visibility = isBuiltin ? Visibility.Visible : Visibility.Collapsed;
            foreach (var box in new System.Windows.Controls.Control[] { NameBox, IconBox, TypeBox, FileBox, ArgsBox, WorkDirBox, FallbackBox, BuiltinBox, TooltipBox })
                box.IsEnabled = has;
        }
        finally { _loading = false; }
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var s = new Shortcut { Name = "Novo", Icon = "•", FileName = "notepad.exe" };
        _items.Add(s);
        ShortcutListBox.SelectedItem = s;
        StatusText.Text = $"{_items.Count} botões";
    }

    private void DelButton_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is { } s)
        {
            int i = ShortcutListBox.SelectedIndex;
            _items.Remove(s);
            if (_items.Count > 0)
                ShortcutListBox.SelectedIndex = Math.Min(i, _items.Count - 1);
            StatusText.Text = $"{_items.Count} botões";
        }
    }

    private void UpButton_Click(object sender, RoutedEventArgs e) => Move(-1);
    private void DownButton_Click(object sender, RoutedEventArgs e) => Move(1);

    private void Move(int delta)
    {
        if (Selected is not { } s) return;
        int i = _items.IndexOf(s);
        int j = i + delta;
        if (j < 0 || j >= _items.Count) return;
        _items.Move(i, j);
        ShortcutListBox.SelectedItem = s;
    }

    // ---------- formulário ----------

    private void FieldChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || Selected is not { } s) return;
        if (sender == NameBox) { s.Name = NameBox.Text; ShortcutListBox.Items.Refresh(); }
        else if (sender == TooltipBox) s.Tooltip = TooltipBox.Text;
        else if (sender == FileBox) s.FileName = NullIfEmpty(FileBox.Text);
        else if (sender == ArgsBox) s.Arguments = NullIfEmpty(ArgsBox.Text);
        else if (sender == WorkDirBox) s.WorkingDirectory = NullIfEmpty(WorkDirBox.Text);
        else if (sender == FallbackBox)
            s.FallbackPaths = FallbackBox.Text
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
    }

    private void TypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || Selected is not { } s) return;
        bool isBuiltin = TypeBox.SelectedIndex == 1;
        ProgramPanel.Visibility = isBuiltin ? Visibility.Collapsed : Visibility.Visible;
        BuiltinPanel.Visibility = isBuiltin ? Visibility.Visible : Visibility.Collapsed;
        if (isBuiltin)
        {
            s.Builtin = ShortcutStore.BuiltinOptions[Math.Max(BuiltinBox.SelectedIndex, 0)].Value;
            s.FileName = s.Arguments = s.WorkingDirectory = null;
            s.FallbackPaths = null;
        }
        else
        {
            s.Builtin = null;
            if (string.IsNullOrWhiteSpace(s.FileName)) s.FileName = "notepad.exe";
            LoadSelected(); // recarrega campos do modo programa
        }
    }

    private void BuiltinBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || Selected is not { } s) return;
        if (BuiltinBox.SelectedIndex >= 0)
            s.Builtin = ShortcutStore.BuiltinOptions[BuiltinBox.SelectedIndex].Value;
    }

    // Ícone: vale o escolhido na galeria ou qualquer emoji colado/digitado.
    private void IconBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        SyncIcon();

    private void IconBox_LostFocus(object sender, RoutedEventArgs e) =>
        SyncIcon();

    private void SyncIcon()
    {
        if (_loading || Selected is not { } s) return;
        s.Icon = string.IsNullOrWhiteSpace(IconBox.Text) ? "•" : IconBox.Text.Trim();
    }

    private void TestButton_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } s) return;
        Actions.Launch(new Shortcut
        {
            Name = s.Name, Icon = s.Icon, FileName = s.FileName,
            Arguments = s.Arguments, WorkingDirectory = s.WorkingDirectory,
            FallbackPaths = s.FallbackPaths?.ToList(), Builtin = s.Builtin,
        });
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_items.Count == 0)
        {
            MessageBox.Show("Adicione ao menos um botão.", "Nook",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        foreach (var s in _items)
        {
            if (string.IsNullOrWhiteSpace(s.Icon)) s.Icon = "•";
            if (string.IsNullOrWhiteSpace(s.Name))
            {
                MessageBox.Show("Todo botão precisa de um nome.", "Nook",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(s.Builtin) && string.IsNullOrWhiteSpace(s.FileName))
            {
                MessageBox.Show($"O botão '{s.Name}' precisa de um programa ou ação.", "Nook",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        try
        {
            ShortcutStore.Save(_items.ToList());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Falha ao salvar:\n{ex.Message}", "Nook",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        DialogResult = true;
        Close();
    }

    // ---------- aba Aparência (live: aplica + salva na hora) ----------

    private void AppearanceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _settings.Theme = ThemeBox.SelectedIndex == 1 ? "light" : "dark";
        _settings.Size = SizeBox.SelectedIndex switch { 0 => "S", 2 => "L", _ => "M" };
        _settings.Corner = CornerBox.SelectedIndex switch
        {
            1 => "bottom-left", 2 => "top-right", 3 => "top-left", _ => "bottom-right",
        };
        _settings.Columns = ColumnsBox.SelectedIndex switch { 1 => 2, 2 => 3, 3 => 4, _ => 1 };
        PersistAppearance();
    }

    private void PageSizeBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _settings.PageSize = PageSizeBox.SelectedIndex == 1 ? 16 : 8;
        try
        {
            _settings.Save();
            _applyLayout(); // repagina os modos 1x/2x sem rebuild completo
            StatusText.Text = "Ícones por página atualizados.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Falha ao salvar: {ex.Message}";
        }
    }

    private void OpacitySlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _settings.Opacity = OpacitySlider.Value;
        OpacityLabel.Text = $"{(int)OpacitySlider.Value}%";
        try { _applyBrushes(); } catch { /* preview é melhor-esforço */ }
        // Salva 1x ao fim do arrasto em vez de a cada tick.
        _opacitySaver.Call(() =>
        {
            try
            {
                _settings.Save();
                _applyBrushes();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Falha ao salvar aparência: {ex.Message}";
            }
        });
    }

    private void PersistAppearance()
    {
        try
        {
            _settings.Save();
            _applyAppearance();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Falha ao salvar aparência: {ex.Message}";
        }
    }

    private void HotkeyBox_LostFocus(object sender, RoutedEventArgs e) =>
        ApplyHotkeyBox();

    private void HotkeyBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
            ApplyHotkeyBox();
    }

    private void ApplyHotkeyBox()
    {
        if (_loading) return;
        string spec = HotkeyBox.Text.Trim();
        if (spec == _settings.Hotkey) return;
        string? err = _tryHotkey(spec);
        if (err != null)
        {
            MessageBox.Show(err, "Nook", MessageBoxButton.OK, MessageBoxImage.Warning);
            HotkeyBox.Text = _settings.Hotkey;
        }
        else
        {
            StatusText.Text = "Atalho global atualizado.";
        }
    }

    private void ModulesCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.ShowPomodoro = ShowPomoCheck.IsChecked == true;
        _settings.ShowClips = ShowClipsCheck.IsChecked == true;
        try
        {
            _settings.Save();
            _applyAppearance(); // esconde/mostra + religa listener e tray
            StatusText.Text = "Módulos atualizados.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Falha ao salvar: {ex.Message}";
        }
    }

    private void PomoBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        bool okF = int.TryParse(FocusBox.Text.Trim(), out int f);
        bool okB = int.TryParse(BreakBox.Text.Trim(), out int b);
        if (!okF || !okB)
        {
            MessageBox.Show("Use minutos inteiros (foco 1–120, pausa 1–60).", "Nook",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            FocusBox.Text = _settings.PomodoroFocusMin.ToString();
            BreakBox.Text = _settings.PomodoroBreakMin.ToString();
            return;
        }
        _settings.PomodoroFocusMin = Math.Clamp(f, 1, 120);
        _settings.PomodoroBreakMin = Math.Clamp(b, 1, 60);
        FocusBox.Text = _settings.PomodoroFocusMin.ToString();
        BreakBox.Text = _settings.PomodoroBreakMin.ToString();
        try
        {
            _settings.Save();
            _applyAppearance(); // Nook parado acompanha a nova duração
            StatusText.Text = "Pomodoro atualizado.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Falha ao salvar: {ex.Message}";
        }
    }

    private static string? NullIfEmpty(string t) =>
        string.IsNullOrWhiteSpace(t) ? null : t;
}
