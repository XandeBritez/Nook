using System.IO;
using System.Windows;
using WinClipboard = System.Windows.Clipboard;

namespace Nook;

/// <summary>Item do histórico (só texto na v1).</summary>
public sealed class ClipItem
{
    public string Text { get; set; } = string.Empty;
    public DateTime When { get; set; } = DateTime.Now;

    /// <summary>Prévia de uma linha para a lista.</summary>
    public string Preview
    {
        get
        {
            string one = Text.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return one.Length <= 70 ? one : one[..70] + "…";
        }
    }

    public string Time => When.ToString("HH:mm");
}

/// <summary>Histórico da área de transferência. Listener vive no Nook;
/// janela consome via Changed. Sem admin, só texto.</summary>
public static class ClipboardHistory
{
    public const int MaxItems = 25;
    private const int MaxChars = 20000;

    private static readonly List<ClipItem> _items = new();
    private static readonly object _lock = new();
    private static bool _persist;
    private static Debouncer? _saver; // coalesce writes (1 Ctrl+C = 0 writes imediatos)

    public static event Action? Changed;

    public static IReadOnlyList<ClipItem> Items
    {
        get { lock (_lock) return _items.ToList(); }
    }

    public static string JsonPath =>
        Path.Combine(AppContext.BaseDirectory, "clipboard.json");

    public static void Start(IntPtr hwnd, bool persist)
    {
        _persist = persist;
        if (persist) Load();
        _saver ??= new Debouncer(TimeSpan.FromMilliseconds(500));
        try { Native.AddClipboardFormatListener(hwnd); } catch { /* sem listener, sem histórico */ }
    }

    public static void Stop(IntPtr hwnd, bool persist)
    {
        try { Native.RemoveClipboardFormatListener(hwnd); } catch { }
        lock (_lock)
        {
            if (!persist)
            {
                _items.Clear();
                try { if (File.Exists(JsonPath)) File.Delete(JsonPath); } catch { }
            }
            else WriteNow(); // flush: não perde a última cópia ao sair
        }
    }

    /// <summary>Lê o clipboard atual para o histórico (chamado no WM_CLIPBOARDUPDATE).</summary>
    public static void Capture()
    {
        string? text;
        try
        {
            if (!WinClipboard.ContainsText()) return;
            text = WinClipboard.GetText();
        }
        catch { return; } // clipboard travado por outro app — ignora
        if (string.IsNullOrWhiteSpace(text)) return;
        if (text.Length > MaxChars) text = text[..MaxChars];
        Add(text);
    }

    public static void Add(string text)
    {
        lock (_lock)
        {
            var existing = _items.FirstOrDefault(i => i.Text == text);
            if (existing != null)
            {
                // Comportamento padrão de gerenciadores: existente volta ao topo.
                _items.Remove(existing);
                existing.When = DateTime.Now;
                _items.Insert(0, existing);
            }
            else
            {
                _items.Insert(0, new ClipItem { Text = text });
                while (_items.Count > MaxItems) _items.RemoveAt(_items.Count - 1);
            }
        }
        PersistIfEnabled();
        Changed?.Invoke();
    }

    public static void Remove(ClipItem item)
    {
        lock (_lock) _items.Remove(item);
        PersistIfEnabled();
        Changed?.Invoke();
    }

    public static void Clear()
    {
        lock (_lock) _items.Clear();
        PersistIfEnabled();
        Changed?.Invoke();
    }

    public static void SetPersist(bool persist, AppSettings settings)
    {
        _persist = persist;
        settings.ClipboardPersist = persist;
        try { settings.Save(); } catch { }
        if (persist) PersistIfEnabled();
        else try { if (File.Exists(JsonPath)) File.Delete(JsonPath); } catch { }
    }

    private static void PersistIfEnabled()
    {
        if (!_persist) return;
        if (_saver == null) { WriteNow(); return; }
        _saver.Call(WriteNow); // grava 1x, 500ms após a última mudança
    }

    private static void WriteNow()
    {
        List<ClipItem> snapshot;
        lock (_lock) snapshot = _items.ToList();
        JsonStore.TrySave(JsonPath, snapshot); // volátil por natureza: falha silenciosa
    }

    private static void Load()
    {
        var list = JsonStore.TryLoad<List<ClipItem>>(JsonPath);
        if (list == null) return;
        lock (_lock)
        {
            _items.Clear();
            _items.AddRange(list.Where(i => !string.IsNullOrWhiteSpace(i.Text)).Take(MaxItems));
        }
    }
}
