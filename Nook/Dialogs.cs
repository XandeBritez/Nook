namespace Nook;

/// <summary>Caixas de diálogo compartilhadas (sempre como usuário atual, sem UAC).</summary>
internal static class Dialogs
{
    public static bool Confirm(string text, string title = "Nook") =>
        System.Windows.MessageBox.Show(text, title,
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question)
        == System.Windows.MessageBoxResult.Yes;

    public static void Info(string text, string title = "Nook") =>
        System.Windows.MessageBox.Show(text, title,
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);

    public static void Warn(string text, string title = "Nook") =>
        System.Windows.MessageBox.Show(text, title,
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Warning);

    public static void Error(string text, string title = "Nook") =>
        System.Windows.MessageBox.Show(text, title,
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error);
}
