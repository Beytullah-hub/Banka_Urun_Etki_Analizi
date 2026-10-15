using Microsoft.Data.Sqlite;

namespace BankProductImpact.Infrastructure.Sqlite;

/// <summary>
/// Kısa ömürlü SQLite bağlantıları üretir. Bağlantı dizesi/koda gömülü yol yoktur; yol
/// dışarıdan verilir (yerel ya da paylaşılan UNC yolu — ör. \\sunucu\paylasim\...).
///
/// PAYLAŞILAN (ÇOK KULLANICILI) KULLANIM NOTU: journal modu bilerek DELETE'tir, WAL DEĞİL.
/// WAL, bir veritabanı dosyasına birden fazla makineden ağ paylaşımı üzerinden erişimi
/// DESTEKLEMEZ (paylaşımlı bellek gerektirir). DELETE (klasik rollback journal), ağ
/// paylaşımında güvenle çalışan moddur. Yazımlar ayrıca uygulama-seviyesi tek yazıcı
/// kilidiyle (ApplicationWriteLock) sıralanır.
/// </summary>
public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string databasePath)
    {
        DatabasePath = databasePath;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true
        }.ToString();
    }

    public string DatabasePath { get; }

    /// <summary>Açık bir bağlantı döndürür. Çağıran (using ile) kapatmaktan sorumludur.</summary>
    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var pragma = connection.CreateCommand();
        // DELETE journal (ağ paylaşımı uyumlu), FK kontrolü, ağ gecikmesi/kilit için bekleme.
        pragma.CommandText = "PRAGMA journal_mode = DELETE; PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 15000;";
        pragma.ExecuteNonQuery();

        return connection;
    }
}
