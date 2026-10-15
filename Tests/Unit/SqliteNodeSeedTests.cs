using BankProductImpact.Domain.Common;
using BankProductImpact.Infrastructure.Nodes;
using BankProductImpact.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BankProductImpact.Tests.Unit;

/// <summary>
/// SQLite node kataloğu seed'i ve şema kısıtları (teknik doküman 4, 11):
/// isimlerin birebir korunması, FK, UNIQUE, CHECK ve çok-parent/çok-rol davranışı.
/// </summary>
public sealed class SqliteNodeSeedTests : IDisposable
{
    private readonly string _dir;
    private readonly SqliteConnectionFactory _factory;
    private readonly SqliteNodeRepository _nodes;

    public SqliteNodeSeedTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "bpi_node_" + Guid.NewGuid().ToString("N"));
        _factory = new SqliteConnectionFactory(Path.Combine(_dir, "ProductImpact.db"));
        SqliteDatabaseInitializer.EnsureCreated(_factory);
        _nodes = new SqliteNodeRepository(_factory);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    private void InsertRelation(string id, string parent, string child)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText =
            "INSERT INTO NodeRelations (NodeRelationId, ParentNodeId, ChildNodeId, CreatedAt, UpdatedAt) " +
            "VALUES ($id, $p, $c, $now, $now);";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$p", parent);
        cmd.Parameters.AddWithValue("$c", child);
        cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    private string IdOf(string name) => _nodes.GetAllNodes().Single(n => n.Name == name).Id.Value;

    [Fact] // Seed: parent isimleri BİREBİR korunur (root olarak seçilebilir)
    public void Seed_ParentIsimleriBirebirKorunur()
    {
        var roots = _nodes.GetRootNodes().Select(n => n.Name).ToHashSet();
        Assert.Contains("Yatırım İşlemleri", roots);
        Assert.Contains("Repo", roots);
        Assert.Contains("Yatırım Fonu İşlemleri (Bankamız Fon İşlemleri)", roots);
        Assert.Contains("Talimatlı İşlemler (hesap/kredi kartı)", roots);
        Assert.Contains("Döviz ve Kıymetli Maden Alış/Satış Talimatları", roots);
    }

    [Fact] // Seed: child isimleri BİREBİR korunur (havuzda, root değil)
    public void Seed_ChildIsimleriBirebirKorunur()
    {
        var all = _nodes.GetAllNodes().Select(n => n.Name).ToHashSet();
        Assert.Contains("İşlem girişi", all);
        Assert.Contains("Müşteri 360 ekranı etkisi", all);
        Assert.Contains("Fiyat / oran Tanımlama", all);
        Assert.Contains("ATM den etkisi", all);
    }

    [Fact] // Repo, "repo" kimliğiyle root olarak seed edilir
    public void Seed_RepoRootNodeVardir()
    {
        var repo = _nodes.GetNode(NodeCatalogSeed.RepoNodeId);
        Assert.NotNull(repo);
        Assert.Equal("Repo", repo!.Name);
        Assert.True(repo.CanBeRoot);
    }

    [Fact] // İlk açılışta katalog Repo'ya otomatik bağlanmaz (havuz boş bekler)
    public void Seed_BaslangictaBaglantiYok()
    {
        Assert.Empty(_nodes.GetChildRelations(NodeCatalogSeed.RepoNodeId));
    }

    [Fact] // Kullanıcı tüm kataloğu silerse sonraki açılışta seed geri yüklenmez
    public void SilinenKatalog_YenidenAcilistaGeriGelmez()
    {
        foreach (var node in _nodes.GetAllNodes())
            _nodes.DeleteNode(node.Id, "tester");

        Assert.Empty(_nodes.GetAllNodes());
        SqliteDatabaseInitializer.EnsureCreated(_factory);
        Assert.Empty(_nodes.GetAllNodes());
    }

    [Fact] // Bir node hem parent hem child olabilir; aynı child birden fazla parent'a bağlanabilir
    public void NodeRelations_CokParentVeCiftRolMumkun()
    {
        var repo = NodeCatalogSeed.RepoNodeId.Value;
        var yatirim = IdOf("Yatırım İşlemleri");
        var raporlama = IdOf("Raporlama");

        // Raporlama hem Repo'ya hem Yatırım İşlemleri'ne child; Yatırım İşlemleri de Repo'ya child.
        InsertRelation("r1", repo, raporlama);
        InsertRelation("r2", yatirim, raporlama);
        InsertRelation("r3", repo, yatirim);

        Assert.Contains(_nodes.GetChildren(new NodeId(repo)), n => n.Name == "Raporlama");
        Assert.Contains(_nodes.GetChildren(new NodeId(yatirim)), n => n.Name == "Raporlama");
        Assert.Contains(_nodes.GetChildren(new NodeId(repo)), n => n.Name == "Yatırım İşlemleri");
    }

    [Fact] // CHECK: parent = child veri katmanında da engellenir
    public void NodeRelations_ParentEsitChildEngellenir()
    {
        var repo = NodeCatalogSeed.RepoNodeId.Value;
        Assert.ThrowsAny<SqliteException>(() => InsertRelation("bad", repo, repo));
    }

    [Fact] // UNIQUE: aynı parent–child ikilisi mükerrer eklenemez
    public void NodeRelations_MukerrerBaglantiEngellenir()
    {
        var repo = NodeCatalogSeed.RepoNodeId.Value;
        var raporlama = IdOf("Raporlama");
        InsertRelation("u1", repo, raporlama);
        Assert.ThrowsAny<SqliteException>(() => InsertRelation("u2", repo, raporlama));
    }

    [Fact] // FK: tanımsız node'a bağlantı eklenemez
    public void NodeRelations_TanimsizNodeEngellenir()
    {
        var repo = NodeCatalogSeed.RepoNodeId.Value;
        Assert.ThrowsAny<SqliteException>(() => InsertRelation("fk", repo, "YOK-999"));
    }
}
