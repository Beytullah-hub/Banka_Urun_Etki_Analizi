using System.IO;
using System.Text.Json;

namespace BankProductImpact.Presentation.Configuration;

/// <summary>
/// Uygulama ayarları (dosya yolu koda gömülmez, ayardan okunur). appsettings.json
/// bulunamazsa veya bozuksa güvenli varsayılanlar kullanılır.
/// </summary>
public sealed class AppConfiguration
{
    /// <summary>Veri kaynağı: "SQLite" (varsayılan) veya bellek-içi yerleşik veri için başka bir değer.</summary>
    public string DataSource { get; set; } = "SQLite";

    /// <summary>SQLite veritabanı dosya yolu (göreli ise uygulama klasörüne göre çözülür).</summary>
    public string DatabasePath { get; set; } = "Data/ProductImpact.db";

    public bool UsesSqlite => string.Equals(DataSource, "SQLite", StringComparison.OrdinalIgnoreCase);

    /// <summary>SQLite yolunu mutlak yola çevirir (göreliyse uygulama klasörüne göre).</summary>
    public string ResolveDatabasePath() =>
        Path.IsPathRooted(DatabasePath) ? DatabasePath : Path.Combine(AppContext.BaseDirectory, DatabasePath);

    /// <summary>appsettings.json'ın mutlak yolu (çalışan exe'nin yanındaki).</summary>
    public static string SettingsFilePath => Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    /// <summary>
    /// Veritabanı yolunu appsettings.json'a yazar (arayüzden değiştirme için). Yalnızca
    /// desteklenen alanlar yazılır; hesaplanan üyeler dışarıda bırakılır.
    /// </summary>
    public static void SaveDatabasePath(string databasePath)
    {
        var json = JsonSerializer.Serialize(
            new { DataSource = "SQLite", DatabasePath = databasePath },
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsFilePath, json);
    }

    public static AppConfiguration Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path))
            return new AppConfiguration();

        try
        {
            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<AppConfiguration>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return config ?? new AppConfiguration();
        }
        catch (Exception)
        {
            // Bozuk ayar dosyası uygulamayı düşürmemeli; varsayılanlarla devam edilir.
            return new AppConfiguration();
        }
    }
}
