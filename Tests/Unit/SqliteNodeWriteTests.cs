using BankProductImpact.Domain.Common;
using BankProductImpact.Domain.Nodes;
using BankProductImpact.Infrastructure.Nodes;
using BankProductImpact.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BankProductImpact.Tests.Unit;

/// <summary>
/// FAZ 4 node yazma metotları (teknik doküman 7-9, 13): child ekleme/çıkarma, konum,
/// mükerrer engelleme, yeni node + ad kontrolü, ağaçtan çıkarmanın node'u silmemesi, audit.
/// </summary>
public sealed class SqliteNodeWriteTests : IDisposable
{
    private readonly string _dir;
    private readonly SqliteConnectionFactory _factory;
    private readonly SqliteNodeRepository _nodes;

    public SqliteNodeWriteTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "bpi_nw_" + Guid.NewGuid().ToString("N"));
        _factory = new SqliteConnectionFactory(Path.Combine(_dir, "ProductImpact.db"));
        SqliteDatabaseInitializer.EnsureCreated(_factory);
        _nodes = new SqliteNodeRepository(_factory);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private NodeId Repo => NodeCatalogSeed.RepoNodeId;
    private NodeId ChildId(string name) => _nodes.GetAllNodes().Single(n => n.Name == name).Id;

    private long AuditCount()
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM AuditLog;";
        return (long)(cmd.ExecuteScalar() ?? 0L);
    }

    [Fact] // Seçerek child ekleme: bağlantı oluşur ve GetChildren'da görünür
    public void AddChild_BaglantiOlusturur()
    {
        var raporlama = ChildId("Raporlama");
        _nodes.AddOrActivateChild(Repo, raporlama, null, null, "tester");

        Assert.Contains(_nodes.GetChildren(Repo), n => n.Id.Equals(raporlama));
    }

    [Fact] // Aynı child ikinci kez eklenince mükerrer bağlantı oluşmaz
    public void AddChild_MukerrerEklenmez()
    {
        var c = ChildId("İşlem girişi");
        _nodes.AddOrActivateChild(Repo, c, null, null, "t");
        _nodes.AddOrActivateChild(Repo, c, null, null, "t");

        Assert.Single(_nodes.GetChildRelations(Repo), r => r.ChildNodeId.Equals(c));
    }

    [Fact] // Ağaçtan çıkarma bağlantıyı pasife alır ama node'u SİLMEZ
    public void RemoveChild_NoduSilmez()
    {
        var c = ChildId("Karlılık");
        _nodes.AddOrActivateChild(Repo, c, null, null, "t");
        _nodes.RemoveChild(Repo, c, "t");

        Assert.DoesNotContain(_nodes.GetChildren(Repo), n => n.Id.Equals(c));
        Assert.NotNull(_nodes.GetNode(c)); // node katalogda duruyor
    }

    [Fact] // Bir parent'tan çıkarılan child diğer parent'ta kalır
    public void RemoveChild_DigerParenttaKalir()
    {
        var yatirim = ChildId("Yatırım İşlemleri");
        var raporlama = ChildId("Raporlama");
        _nodes.AddOrActivateChild(Repo, raporlama, null, null, "t");
        _nodes.AddOrActivateChild(yatirim, raporlama, null, null, "t");

        _nodes.RemoveChild(Repo, raporlama, "t");

        Assert.DoesNotContain(_nodes.GetChildren(Repo), n => n.Id.Equals(raporlama));
        Assert.Contains(_nodes.GetChildren(yatirim), n => n.Id.Equals(raporlama));
    }

    [Fact] // Konum kaydedilir
    public void UpdatePosition_Kaydeder()
    {
        var c = ChildId("Reeskont");
        _nodes.AddOrActivateChild(Repo, c, 10, 20, "t");
        _nodes.UpdateChildPosition(Repo, c, 123.5, 456.5, "t");

        var rel = _nodes.GetChildRelations(Repo).Single(r => r.ChildNodeId.Equals(c));
        Assert.Equal(123.5, rel.PositionX);
        Assert.Equal(456.5, rel.PositionY);
    }

    [Fact] // Yeni node katalog: eklenir ve ad kontrolü (kırpma + büyük/küçük harf) yakalar
    public void AddNode_YeniKayitVeAdKontrolu()
    {
        var created = _nodes.AddNode(new NodeDraft("Yeni Ekran", "açıklama", true, false), "t");

        Assert.NotNull(_nodes.GetNode(created.Id));
        Assert.True(_nodes.NameExists("yeni ekran"));       // büyük/küçük harf
        Assert.True(_nodes.NameExists("  Yeni Ekran  "));   // gereksiz boşluk
        Assert.False(_nodes.NameExists("Yeni Ekran 2"));
    }

    [Fact] // Yazma işlemleri audit'e kaydedilir
    public void Yazmalar_AuditeKaydedilir()
    {
        var before = AuditCount();
        _nodes.AddNode(new NodeDraft("Denetim Testi", null, true, false), "t");
        var c = ChildId("Mizan/muhasebe");
        _nodes.AddOrActivateChild(Repo, c, null, null, "t");
        _nodes.RemoveChild(Repo, c, "t");

        Assert.True(AuditCount() >= before + 3);
    }

    [Fact] // Parent = child eklenemez
    public void AddChild_ParentEsitChildEngellenir()
    {
        Assert.Throws<ArgumentException>(() => _nodes.AddOrActivateChild(Repo, Repo, null, null, "t"));
    }

    [Fact]
    public void DeleteNode_BaglantilariylaKaliciSilerVeAuditYazar()
    {
        var child = _nodes.AddNode(new NodeDraft("Silinecek", null, true, false), "t");
        _nodes.AddOrActivateChild(Repo, child.Id, null, null, "t");
        var before = AuditCount();

        _nodes.DeleteNode(child.Id, "t");

        Assert.Null(_nodes.GetNode(child.Id));
        Assert.DoesNotContain(_nodes.GetChildren(Repo), n => n.Id.Equals(child.Id));
        Assert.Equal(before + 1, AuditCount());
    }
}
