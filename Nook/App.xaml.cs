namespace Nook;

public partial class App : System.Windows.Application
{
    private Tray? _tray;
    private MainWindow? _nook;
    private AppSettings _settings = new();

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        // Log global de crash: se algo falhar, o usuário vê o motivo em vez de "abriu e fechou".
        DispatcherUnhandledException += (_, args) =>
        {
            CrashLog.Write("DispatcherUnhandledException", args.Exception);
            args.Handled = false;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            CrashLog.Write("UnhandledException", args.ExceptionObject as Exception);

        try
        {
            base.OnStartup(e);

            LegacyMigrator.MigrateAll(); // HubApp → Nook (one-time, silencioso)

            var shortcuts = ShortcutStore.LoadOrCreateDefault();
            _settings = AppSettings.LoadOrCreateDefault();

            _nook = new MainWindow(shortcuts, _settings);
            MainWindow = _nook;
            _nook.SettingsRequested += () => OpenEditor();
            _nook.ClipboardRequested += () => _nook.OpenClipboard();
            _nook.PomodoroNotify += (title, text) => _tray?.ShowBalloon(title, text);

            _tray = new Tray();
            _tray.OpenRequested += () => _nook.PulseExpand();
            _tray.EditRequested += () => OpenEditor(selectAbout: false);
            _tray.AboutRequested += () => OpenEditor(selectAbout: true);
            _tray.ClipboardRequested += () => _nook.OpenClipboard();
            _tray.RefreshAutostart();
            _tray.SetClipsVisible(_settings.ShowClips);

            bool minimized = e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase);
            _nook.Show();
            if (!minimized)
                _nook.PulseExpand(); // mostra uma vez para o usuário descobrir a faixa

            Exit += (_, _) => _tray?.Dispose();
        }
        catch (Exception ex)
        {
            CrashLog.Write("OnStartup", ex);
            System.Windows.MessageBox.Show(
                $"O Nook falhou ao iniciar:\n{ex.GetType().Name}: {ex.Message}\n\nDetalhes em:\n{CrashLog.Path}",
                "Nook — erro ao iniciar",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void OpenEditor(bool selectAbout = false)
    {
        if (_nook == null) return;
        try
        {
            // Recarrega do disco (caso o JSON tenha sido editado à mão).
            var current = ShortcutStore.LoadOrCreateDefault();
            var dlg = new SettingsWindow(current, _settings, () =>
            {
                _nook.ApplySettings(_settings);
                _tray?.SetClipsVisible(_settings.ShowClips);
            }, () => _nook.ApplyBrushes(), () => _nook.RefreshLayoutView(), _nook.TrySetHotkey)
            {
                Owner = _nook
            };
            if (selectAbout) dlg.ShowAbout();
            if (dlg.ShowDialog() == true)
                _nook.SetShortcuts(dlg.UpdatedItems);
        }
        catch (Exception ex)
        {
            CrashLog.Write("OpenEditor", ex);
            System.Windows.MessageBox.Show(
                $"Falha ao abrir o editor:\n{ex.Message}",
                "Nook", System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }
}
