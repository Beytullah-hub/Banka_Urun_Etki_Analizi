using BankProductImpact.Infrastructure.Nodes;
using Microsoft.Data.Sqlite;

namespace BankProductImpact.Infrastructure.Sqlite;

/// <summary>
/// SQLite veritabanını hazırlar: yeni bir veritabanı oluşturuluyorsa node kataloğunu
/// <see cref="NodeCatalogSeed"/>'den seed eder. Seed işlemi tek bir transaction
/// içinde atomik yapılır. Makro/formül yoktur; tüm değerler uygulama katmanınca
/// parametreli sorgularla yazılır.
/// </summary>
public static class SqliteDatabaseInitializer
{
    /// <summary>Veritabanı/şema yoksa oluşturur; seed yalnızca fiziksel DB ilk kez oluşturulurken çalışır.</summary>
    public static void EnsureCreated(SqliteConnectionFactory factory)
    {
        var databasePath = Path.GetFullPath(factory.DatabasePath);
        var isNewDatabase = !File.Exists(databasePath) || new FileInfo(databasePath).Length == 0;
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using var connection = factory.Open();

        // DELETE journal (WAL DEĞİL): paylaşılan ağ dosyasına çok makineli erişim için gerekli.
        // Mevcut bir WAL veritabanını da bu satır kalıcı olarak DELETE moduna dönüştürür.
        Execute(connection, "PRAGMA journal_mode = DELETE;");

        foreach (var statement in SqliteSchema.CreateStatements)
            Execute(connection, statement);

        // Mevcut (eski sürümlü) veritabanlarını güvenli, idempotent biçimde yükselt.
        ApplyMigrations(connection);

        if (isNewDatabase)
            SeedNodes(connection);
    }

    private static void SeedNodes(SqliteConnection connection)
    {
        using var tx = connection.BeginTransaction();

        const string sql =
            "INSERT INTO Nodes (NodeId, Name, Description, IsActive, CanBeRoot, DisplayOrder, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy) " +
            "VALUES ($id, $name, $desc, $active, $root, $order, $created, $updated, $createdBy, $updatedBy);";

        foreach (var node in NodeCatalogSeed.BuildNodes())
        {
            Execute(connection, tx, sql,
                ("$id", node.Id.Value),
                ("$name", node.Name),
                ("$desc", (object?)node.Description),
                ("$active", node.IsActive ? 1 : 0),
                ("$root", node.CanBeRoot ? 1 : 0),
                ("$order", node.DisplayOrder),
                ("$created", node.CreatedAt.ToString("o", System.Globalization.CultureInfo.InvariantCulture)),
                ("$updated", node.UpdatedAt.ToString("o", System.Globalization.CultureInfo.InvariantCulture)),
                ("$createdBy", (object?)node.CreatedBy),
                ("$updatedBy", (object?)node.UpdatedBy));
        }

        tx.Commit();
    }

    /// <summary>
    /// Geriye uyumlu, idempotent şema yükseltmeleri. Var olan verileri korur; tekrar
    /// çalıştırıldığında hata vermez. Adımlar zincirlenir; eski (≤1.4) bir veritabanı
    /// tek geçişte güncel sürüme yükselir.
    ///
    /// 1.5: tek-katman bilgi modeli (NodeDetails kaldırıldı, sütunlar yeni adlara taşındı).
    /// 1.6: bilgi alanları yeniden tanımlandı → ekran adı, batch adı, kısa açıklama.
    /// </summary>
    private static void ApplyMigrations(SqliteConnection c)
    {
        // ---- 1.4 → 1.5 (varsa) ----
        RenameColumn(c, "NodeRelationDetails", "ContextDescription", "ShortDescription");
        RenameColumn(c, "NodeRelationDetails", "ImpactedSystems", "AffectedArea");
        RenameColumn(c, "NodeRelationDetails", "BusinessNotes", "Notes");
        // Genel (tüm başlıklarda ortak) bilgi katmanı kaldırıldı; artık bağlantıya özel tek katman var.
        Execute(c, "DROP TABLE IF EXISTS NodeDetails;");

        // ---- 1.5 → 1.6: alanlar ekran adı / batch adı / kısa açıklama ----
        // Etkilenen alan → ekran adı (ekran kodu bilgisi korunur), batch adı yeni eklenir, not kaldırılır.
        RenameColumn(c, "NodeRelationDetails", "AffectedArea", "ScreenName");
        EnsureColumn(c, "NodeRelationDetails", "BatchName", "TEXT");
        DropColumn(c, "NodeRelationDetails", "Notes");

        // Eski sayısal etki sistemine ait kullanılmayan tabloları kaldır (varsa).
        // FK bağımlılıkları için çocuk→ebeveyn sırasıyla düşürülür.
        foreach (var table in SqliteSchema.ObsoleteTables)
            Execute(c, $"DROP TABLE IF EXISTS {table};");

        SetSchemaVersion(c, SqliteSchema.SchemaVersion);
    }

    /// <summary>Eski sütun varsa ve yeni sütun henüz yoksa yeniden adlandırır (idempotent).</summary>
    private static void RenameColumn(SqliteConnection c, string table, string oldColumn, string newColumn)
    {
        if (ColumnExists(c, table, oldColumn) && !ColumnExists(c, table, newColumn))
            // table/column kod içi sabittir (kullanıcı girdisi değil) — SQL injection riski yok.
            Execute(c, $"ALTER TABLE {table} RENAME COLUMN {oldColumn} TO {newColumn};");
    }

    /// <summary>Sütun yoksa ekler (idempotent).</summary>
    private static void EnsureColumn(SqliteConnection c, string table, string column, string type)
    {
        if (!ColumnExists(c, table, column))
            Execute(c, $"ALTER TABLE {table} ADD COLUMN {column} {type};");
    }

    /// <summary>Sütun varsa kaldırır (idempotent).</summary>
    private static void DropColumn(SqliteConnection c, string table, string column)
    {
        if (ColumnExists(c, table, column))
            Execute(c, $"ALTER TABLE {table} DROP COLUMN {column};");
    }

    private static bool ColumnExists(SqliteConnection c, string table, string column)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table});";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            // 1. sütun (index 1) = sütun adı.
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static void SetSchemaVersion(SqliteConnection c, string version) =>
        Execute(c, null,
            "INSERT INTO AppSettings (SettingKey, SettingValue) VALUES ('SchemaVersion', $v) " +
            "ON CONFLICT(SettingKey) DO UPDATE SET SettingValue = $v;",
            ("$v", version));

    // ---- Parametreli komut yürütme yardımcıları ----

    private static void Execute(SqliteConnection c, string sql) =>
        Execute(c, null, sql);

    private static void Execute(SqliteConnection c, SqliteTransaction? tx, string sql, params (string Name, object? Value)[] parameters)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        if (tx is not null)
            cmd.Transaction = tx;

        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);

        cmd.ExecuteNonQuery();
    }
}
