using System.Diagnostics;
using System.IO;

namespace Nook;

/// <summary>Ações executadas como usuário atual — nenhuma pede UAC/elevação.</summary>
internal static class Actions
{
    public static void Launch(Shortcut s)
    {
        try
        {
            // Ações embutidas (sem UAC): lock, mute, volup, voldown,
            // screenshot, taskmgr, darkmode, recycle, sleep, turbo, reboot, shutdown.
            if (!string.IsNullOrWhiteSpace(s.Builtin))
            {
                RunBuiltin(s.Builtin);
                return;
            }

            string file = Environment.ExpandEnvironmentVariables(s.FileName ?? string.Empty);
            string args = Environment.ExpandEnvironmentVariables(s.Arguments ?? string.Empty);
            string? workDir = string.IsNullOrWhiteSpace(s.WorkingDirectory)
                ? null
                : Environment.ExpandEnvironmentVariables(s.WorkingDirectory);

            // Resolve fallbacks (ex.: chrome em Program Files).
            file = ResolveExe(file, s.FallbackPaths);

            var psi = new ProcessStartInfo
            {
                FileName = file,
                Arguments = args,
                UseShellExecute = true, // abre como duplo-clique: sem prompt de elevação
                WorkingDirectory = workDir ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            };
            ProcessLauncher.StartOrWarn(psi, s.Name);
        }
        catch (Exception ex)
        {
            Dialogs.Warn($"Falha ao executar '{s.Name}':\n{ex.Message}");
        }
    }

    public static void LockPc()
    {
        // Não requer admin. Se falhar, fallback via rundll32 (também sem UAC).
        if (!Native.LockWorkStation())
        {
            ProcessLauncher.Open("rundll32.exe", "user32.dll,LockWorkStation");
        }
    }

    private static void RunBuiltin(string builtin)
    {
        switch (builtin.Trim().ToLowerInvariant())
        {
            case "lock":
                LockPc();
                break;
            case "mute":
                Native.TapMediaKey(Native.VK_VOLUME_MUTE);
                break;
            case "volup":
                Native.TapMediaKey(Native.VK_VOLUME_UP);
                break;
            case "voldown":
                Native.TapMediaKey(Native.VK_VOLUME_DOWN);
                break;
            case "screenshot":
                ScreenshotToPaint();
                break;
            case "taskmgr":
                ProcessLauncher.Open("taskmgr.exe");
                break;
            case "darkmode":
                ToggleDarkMode();
                break;
            case "recycle":
                EmptyRecycleBin();
                break;
            case "sleep":
                SuspendPc();
                break;
            case "reboot":
                RebootPc();
                break;
            case "shutdown":
                ShutdownPc();
                break;
            case "turbo":
                _ = Turbo.RunAsync(); // roda em background; avisa ao concluir
                break;
            default:
                Dialogs.Warn($"Ação embutida desconhecida: '{builtin}'.");
                break;
        }
    }

    private static void ScreenshotToPaint() => _ = ScreenshotToPaintAsync();

    private static async Task ScreenshotToPaintAsync()
    {
        // Captura todas as telas (VirtualScreen) em PNG no %TEMP% e abre no Paint.
        // Captura+save fora da UI thread; o await volta para a UI no fim.
        // Sem UAC, sem depender de clipboard/foco.
        try
        {
            var bounds = System.Windows.Forms.SystemInformation.VirtualScreen;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                throw new InvalidOperationException("Nenhuma tela detectada.");

            string tmp = Path.Combine(Path.GetTempPath(),
                $"NookPrint_{DateTime.Now:yyyyMMdd_HHmmss}.png");

            await Task.Run(() =>
            {
                using var bmp = new System.Drawing.Bitmap(bounds.Width, bounds.Height);
                using var g = System.Drawing.Graphics.FromImage(bmp);
                g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size,
                    System.Drawing.CopyPixelOperation.SourceCopy);
                bmp.Save(tmp, System.Drawing.Imaging.ImageFormat.Png);
            });

            ProcessLauncher.OpenSystem("mspaint.exe", $"\"{tmp}\"");
        }
        catch (Exception ex)
        {
            Dialogs.Warn($"Falha ao capturar a tela:\n{ex.Message}");
        }
    }

    private static void ToggleDarkMode()
    {
        // HKCU = sem admin. Alterna apps + sistema e avisa o shell.
        const string personalize =
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(personalize, true)
            ?? Microsoft.Win32.Registry.CurrentUser.CreateSubKey(personalize);
        if (key == null) return;

        int current = (key.GetValue("AppsUseLightTheme") as int?) ?? 1;
        int next = current == 0 ? 1 : 0;
        key.SetValue("AppsUseLightTheme", next, Microsoft.Win32.RegistryValueKind.DWord);
        key.SetValue("SystemUsesLightTheme", next, Microsoft.Win32.RegistryValueKind.DWord);

        Native.SendMessageTimeout(Native.HWND_BROADCAST, Native.WM_SETTINGCHANGE,
            UIntPtr.Zero, "ImmersiveColorSet", Native.SMTO_ABORTIFHUNG, 2000, out _);
    }

    private static void EmptyRecycleBin()
    {
        if (!Dialogs.Confirm("Esvaziar a lixeira de todas as unidades?")) return;

        int hr = Native.SHEmptyRecycleBin(IntPtr.Zero, null, Native.SHERB_NOCONFIRMATION);
        if (hr != 0)
            Dialogs.Warn($"Não foi possível esvaziar a lixeira (HRESULT 0x{hr:X8}).");
    }

    private static void SuspendPc()
    {
        if (!Dialogs.Confirm("Suspender o PC agora?")) return;

        // Sem UAC. Se o hardware não suportar sleep, retorna false.
        if (!Native.SetSuspendState(false, true, false))
            Dialogs.Warn("Este PC não suporta suspensão (ou ela está desabilitada).");
    }

    private static void RebootPc()
    {
        if (!Dialogs.Confirm(
            "Reiniciar o PC em 30 segundos?\n\nPara cancelar depois: Win+R → shutdown /a")) return;

        ProcessLauncher.Open("shutdown.exe",
            "/r /t 30 /c \"Reinício agendado pelo Nook (cancele com shutdown /a)\"",
            createNoWindow: true);
    }

    private static void ShutdownPc()
    {
        if (!Dialogs.Confirm(
            "Desligar o PC em 30 segundos?\n\nPara cancelar depois: Win+R → shutdown /a")) return;

        ProcessLauncher.Open("shutdown.exe",
            "/s /t 30 /c \"Desligamento agendado pelo Nook (cancele com shutdown /a)\"",
            createNoWindow: true);
    }

    private static string ResolveExe(string primary, List<string>? fallbacks)
    {
        if (HasExe(primary)) return primary;
        if (fallbacks == null) return primary;
        foreach (var raw in fallbacks)
        {
            string p = Environment.ExpandEnvironmentVariables(raw);
            if (HasExe(p)) return p;
        }
        return primary; // deixa o SO resolver via PATH e exibir erro amigável
    }

    private static bool HasExe(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            if (Path.IsPathRooted(path)) return File.Exists(path);
            // Não enraizado: tenta localizar via PATH.
            string? found = FindOnPath(path);
            return found != null;
        }
        catch { return false; }
    }

    private static string? FindOnPath(string exe)
    {
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv == null) return null;
        foreach (var dir in pathEnv.Split(';'))
        {
            try
            {
                string candidate = Path.Combine(dir.Trim().Trim('"'), exe);
                if (File.Exists(candidate)) return candidate;
                // tenta com extensões padrão quando não há extensão
                if (Path.GetExtension(exe) == string.Empty)
                {
                    foreach (var ext in new[] { ".exe", ".cmd", ".bat" })
                        if (File.Exists(candidate + ext)) return candidate + ext;
                }
            }
            catch { /* ignora dirs inválidos */ }
        }
        return null;
    }
}
