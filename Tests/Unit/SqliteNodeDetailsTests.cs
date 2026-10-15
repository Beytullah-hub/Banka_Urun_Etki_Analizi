using BankProductImpact.Application.Abstractions;
using BankProductImpact.Domain.Common;
using BankProductImpact.Domain.Nodes;
using BankProductImpact.Infrastructure.Nodes;
using BankProductImpact.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BankProductImpact.Tests.Unit;

/// <summary>
/// Bağlantı bilgisi (NodeRelationDetails) SQLite kalıcılığı: upsert (tek satır),
/// tek-transaction + audit, rollback, migration (eski sütunları yeniden adlandırma,
/// kullanılmayan NodeDetails tablosunu kaldırma) ve idempotent yükseltme.
/// </summary>
public sealed class SqliteNodeDetailsTests : IDisposable
{
    private readonly string _dir;
    private readonly SqliteConnectionFactory _factory;
    private readonly SqliteNodeDetailsRepository _details;
    private readonly SqliteNodeRepository _nodes;

    public SqliteNodeDetailsTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "bpi_det_" + Guid.NewGuid().ToString("N"));
        _factory = new SqliteConnectionFactory(Path.Combine(_dir, "ProductImpact.db"));
        SqliteDatabaseInitializer.EnsureCreated(_factory);
        _details = new SqliteNodeDetailsRepository(_factory);
        _nodes = new SqliteNodeRepository(_factory);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private NodeId ChildId(string name) => _nodes.GetAllNodes().Single(n => n.Name == name).Id;

    private NodeRelationId AddRelationAndGetId(NodeId parent, NodeId child)
    {
        _nodes.AddOrActivateChild(parent, child, null, null, "t");
        return _nodes.GetChildRelations(parent).Single(r => r.ChildNodeId.Equals(child)).Id;
    }

    private ChildInformationSave Save(NodeRelationId relId, string? shortDesc, string? screen = null) =>
        new(new NodeRelationDetails(relId, screen, null, shortDesc, null, null, "tester"),
            "tester", ChildId("Raporlama"), NodeCatalogSeed.RepoNodeId, null, null);

    [Fact] // Insert
    public void Insert()
    {
        var child = ChildId("Raporlama");
        var relId = AddRelationAndGetId(NodeCatalogSeed.RepoNodeId, child);
        _details.SaveChildInformation(Save(relId, "kısa açıklama"));

        var loaded = _details.GetNodeRelationDetails(relId);
        Assert.NotNull(loaded);
        Assert.Equal("kısa açıklama", loaded!.ShortDescription);
        Assert.NotNull(loaded.CreatedAt);
    }

    [Fact] // Update + mükerrer kayıt oluşmaz (tek satır)
    public void Update_TekSatir()
    {
        var child = ChildId("Karlılık");
        var relId = AddRelationAndGetId(NodeCatalogSeed.RepoNodeId, child);
        _details.SaveChildInformation(Save(relId, "ilk"));
        _details.SaveChildInformation(Save(relId, "güncel", "MKIP101"));

        var loaded = _details.GetNodeRelationDetails(relId);
        Assert.Equal("güncel", loaded!.ShortDescription);
        Assert.Equal("MKIP101", loaded.ScreenName);
        Assert.Equal(1, CountRows("NodeRelationDetails", $"NodeRelationId='{relId.Value}'"));
    }

    [Fact] // Rollback: geçersiz bağlantı (FK ihlali) → hiçbir şey yazılmaz
    public void Rollback_HicbiriYazilmaz()
    {
        var badRel = new NodeRelationId("YOK-REL");
        var save = new ChildInformationSave(
            new NodeRelationDetails(badRel, "ctx", null, null, null, null, "t"),
            "t", ChildId("Mizan/muhasebe"), null, null, null);

        Assert.ThrowsAny<SqliteException>(() => _details.SaveChildInformation(save));
        Assert.Null(_details.GetNodeRelationDetails(badRel));
    }

    [Fact] // Yazma audit'e kaydedilir (Created/Updated)
    public void Yazma_AuditeKaydedilir()
    {
        var child = ChildId("Yetkilendir");
        var relId = AddRelationAndGetId(NodeCatalogSeed.RepoNodeId, child);
        var before = CountRows("AuditLog", "ActionType LIKE 'NodeRelationDetails%'");
        _details.SaveChildInformation(Save(relId, "x"));
        Assert.True(CountRows("AuditLog", "ActionType LIKE 'NodeRelationDetails%'") > before);
    }

    [Fact] // Fresh db: yeni sütunlar mevcut, eski sütunlar/tablo yok
    public void FreshDb_YeniSutunlarVar()
    {
        Assert.True(ColumnExists("NodeRelationDetails", "ScreenName"));
        Assert.True(ColumnExists("NodeRelationDetails", "BatchName"));
        Assert.True(ColumnExists("NodeRelationDetails", "ShortDescription"));
        Assert.False(ColumnExists("NodeRelationDetails", "AffectedArea"));
        Assert.False(ColumnExists("NodeRelationDetails", "Notes"));
        Assert.False(TableExists("NodeDetails"));
    }

    [Fact] // Migration idempotent: EnsureCreated ikinci kez veriyi korur ve hata vermez
    public void Migration_Idempotent_VeriKorunur()
    {
        var child = ChildId("Raporlama");
        var relId = AddRelationAndGetId(NodeCatalogSeed.RepoNodeId, child);
        _details.SaveChildInformation(Save(relId, "kalıcı"));
        var nodeCountBefore = _nodes.GetAllNodes().Count;

        SqliteDatabaseInitializer.EnsureCreated(_factory); // tekrar çalıştır

        Assert.Equal("kalıcı", new SqliteNodeDetailsRepository(_factory).GetNodeRelationDetails(relId)!.ShortDescription);
        Assert.Equal(nodeCountBefore, _nodes.GetAllNodes().Count);
    }

    [Fact] // Eski (1.4) şema → güncel adlara zincirleme taşınır, veri korunur
    public void Migration_EskiSemadanYukseltir_VeriKorunur()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bpi_old_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var factory = new SqliteConnectionFactory(Path.Combine(dir, "old.db"));
        try
        {
            // "Eski" (≤1.4) veritabanı: NodeRelationDetails eski sütun adlarıyla + kullanılmayan NodeDetails.
            using (var c = factory.Open())
            {
                Exec(c, "CREATE TABLE Nodes (NodeId TEXT PRIMARY KEY, Name TEXT, Description TEXT, IsActive INTEGER, CanBeRoot INTEGER, DisplayOrder INTEGER, CreatedAt TEXT, UpdatedAt TEXT, CreatedBy TEXT, UpdatedBy TEXT);");
                Exec(c, "CREATE TABLE NodeRelations (NodeRelationId TEXT PRIMARY KEY, ParentNodeId TEXT, ChildNodeId TEXT, DisplayOrder INTEGER, PositionX REAL, PositionY REAL, IsActive INTEGER, CreatedAt TEXT, UpdatedAt TEXT);");
                Exec(c, "CREATE TABLE AppSettings (SettingKey TEXT PRIMARY KEY, SettingValue TEXT);");
                Exec(c, "CREATE TABLE NodeDetails (Id INTEGER PRIMARY KEY AUTOINCREMENT, NodeId TEXT NOT NULL UNIQUE, Summary TEXT);");
                Exec(c, "CREATE TABLE NodeRelationDetails (Id INTEGER PRIMARY KEY AUTOINCREMENT, NodeRelationId TEXT NOT NULL UNIQUE, ContextDescription TEXT, ImpactedSystems TEXT, BusinessNotes TEXT, CreatedAt TEXT, LastUpdatedAt TEXT, UpdatedBy TEXT);");
                Exec(c, "INSERT INTO NodeRelations (NodeRelationId, ParentNodeId, ChildNodeId, DisplayOrder, IsActive, CreatedAt, UpdatedAt) VALUES ('OLD-REL','P','C',0,1,'2020-01-01T00:00:00Z','2020-01-01T00:00:00Z');");
                Exec(c, "INSERT INTO NodeRelationDetails (NodeRelationId, ContextDescription, ImpactedSystems) VALUES ('OLD-REL','eski açıklama','eski alan');");
                Exec(c, "INSERT INTO AppSettings (SettingKey, SettingValue) VALUES ('SchemaVersion','1.4');");
            }
            SqliteConnection.ClearAllPools();

            SqliteDatabaseInitializer.EnsureCreated(factory); // yükselt (idempotent migration)

            var det = new SqliteNodeDetailsRepository(factory).GetNodeRelationDetails(new NodeRelationId("OLD-REL"));
            Assert.NotNull(det);
            Assert.Equal("eski açıklama", det!.ShortDescription); // ContextDescription → ShortDescription
            Assert.Equal("eski alan", det.ScreenName);            // ImpactedSystems → AffectedArea → ScreenName
            Assert.False(TableExistsIn(factory, "NodeDetails"));   // kullanılmayan tablo kaldırıldı
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }

    private static void Exec(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    // ---- Ham SQL yardımcıları ----

    private long CountRows(string table, string where)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {table} WHERE {where};";
        return (long)(cmd.ExecuteScalar() ?? 0L);
    }

    private bool ColumnExists(string table, string column)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table});";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            if (string.Equals(r.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private bool TableExists(string table) => TableExistsIn(_factory, table);

    private static bool TableExistsIn(SqliteConnectionFactory factory, string table)
    {
        using var c = factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$n;";
        cmd.Parameters.AddWithValue("$n", table);
        return cmd.ExecuteScalar() is not null;
    }
}
