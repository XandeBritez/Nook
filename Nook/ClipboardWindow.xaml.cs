using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinClipboard = System.Windows.Clipboard;

namespace Nook;

/// <summary>Janela do histórico (modeless). Lista atualiza via Changed.</summary>
public partial class ClipboardWindow : Window
{
    private readonly AppSettings _settings;

    public ClipboardWindow(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        PersistCheck.IsChecked = settings.ClipboardPersist;
        ClipboardHistory.Changed += Refresh;
        Refresh();
    }

    protected override void OnClosed(EventArgs e)
    {
        ClipboardHistory.Changed -= Refresh;
        base.OnClosed(e);
    }

    public void FocusSearch()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void Refresh() => Dispatcher.Invoke(ApplyFilter);

    private void ApplyFilter()
    {
        string q = SearchBox.Text.Trim();
        var items = ClipboardHistory.Items;
        ClipList.ItemsSource = string.IsNullOrEmpty(q)
            ? items
            : items.Where(i => i.Text.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        StatusText.Text = $"{ClipList.Items.Count} item(ns)";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) =>
        ApplyFilter();

    private ClipItem? Selected => ClipList.SelectedItem as ClipItem;

    private void CopyButton_Click(object sender, RoutedEventArgs e) => CopySelected();

    private void ClipList_DoubleClick(object sender, MouseButtonEventArgs e) => CopySelected();

    private void ClipList_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { CopySelected(); e.Handled = true; }
        else if (e.Key == Key.Delete) { DeleteSelected(); e.Handled = true; }
    }

    private void CopySelected()
    {
        if (Selected is not { } s) return;
        try
        {
            WinClipboard.SetText(s.Text);
            StatusText.Text = "Copiado! Ctrl+V onde quiser.";
        }
        catch
        {
            StatusText.Text = "Clipboard ocupado, tente de novo.";
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e) => DeleteSelected();

    private void DeleteSelected()
    {
        if (Selected is { } s) ClipboardHistory.Remove(s);
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        ClipboardHistory.Clear();
        StatusText.Text = "Histórico limpo.";
    }

    private void PersistCheck_Changed(object sender, RoutedEventArgs e)
    {
        ClipboardHistory.SetPersist(PersistCheck.IsChecked == true, _settings);
    }
}
