using System.Diagnostics;
using System.Windows;
using BankProductImpact.Infrastructure.Sqlite;
using BankProductImpact.Presentation.Configuration;
using Microsoft.Win32;

namespace BankProductImpact.Presentation;

/// <summary>
/// Veritabanı yolunu arayüzden değiştirme penceresi. Yeni yolu appsettings.json'a yazar
/// ve uygulamayı yeniden başlatır (ayar yalnızca açılışta okunduğu için). İş mantığı yok;
/// yalnızca görünüm + dosya seçimi + yeniden başlatma (view sorumlulukları).
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();

        var config = AppConfiguration.Load();
        CurrentPathText.Text = config.ResolveDatabasePath();
        NewPathBox.Text = config.DatabasePath;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Veritabanı dosyasını seçin",
            Filter = "SQLite veritabanı (*.db)|*.db|Tüm dosyalar (*.*)|*.*",
            CheckFileExists = false, // henüz oluşmamış/paylaşımdaki dosya da seçilebilsin
            CheckPathExists = false
        };
        if (dialog.ShowDialog(this) == true)
            NewPathBox.Text = dialog.FileName;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var newPath = NewPathBox.Text.Trim();
        if (newPath.Length == 0)
        {
            ShowFeedback("Veritabanı yolu boş olamaz.");
            return;
        }

        // Yolu açmayı deneyip kullanıcıyı bilgilendir (yine de kaydetmesine izin verilir;
        // paylaşım henüz hazır olmayabilir).
        if (!TryOpen(newPath, out var openError))
        {
            var proceed = MessageBox.Show(
                $"Bu yola şu an bağlanılamadı:\n{openError}\n\nYine de kaydedilsin mi? " +
                "(Paylaşım daha sonra hazır olacaksa Evet diyebilirsiniz.)",
                "Bağlantı Uyarısı", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (proceed != MessageBoxResult.Yes)
                return;
        }

        try
        {
            AppConfiguration.SaveDatabasePath(newPath);
        }
        catch (Exception ex)
        {
            ShowFeedback($"Ayar kaydedilemedi: {ex.Message}");
            return;
        }

        RestartApplication();
    }

    private static bool TryOpen(string databasePath, out string error)
    {
        try
        {
            var factory = new SqliteConnectionFactory(databasePath);
            using var connection = factory.Open();
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static void RestartApplication()
    {
        var exePath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exePath))
            Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });

        System.Windows.Application.Current.Shutdown();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void ShowFeedback(string message)
    {
        FeedbackText.Text = message;
        FeedbackText.Visibility = Visibility.Visible;
    }
}
