using BankProductImpact.Application.Abstractions;
using BankProductImpact.Application.Locking;
using BankProductImpact.Application.Nodes;
using BankProductImpact.Domain.Common;
using BankProductImpact.Infrastructure.Nodes;
using BankProductImpact.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BankProductImpact.Tests.Unit;

/// <summary>
/// Regresyon: bilgi kaydı aitliği ve konumu DEĞİŞTİRMEZ; ağaçtan çıkarma bilgiyi SİLMEZ;
/// tekrar eklemede eski bilgi (aynı bağlantı üzerinden) yeniden görünür.
/// </summary>
public sealed class ChildDetailRegressionTests : IDisposable
{
    private readonly string _dir;
    private readonly SqliteConnectionFactory _factory;
    private readonly SqliteNodeRepository _nodes;
    private readonly SqliteNodeDetailsRepository _detailsRepo;
    private readonly ChildInformationService _service;

    public ChildDetailRegressionTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "bpi_reg_" + Guid.NewGuid().ToString("N"));
        _factory = new SqliteConnectionFactory(Path.Combine(_dir, "ProductImpact.db"));
        SqliteDatabaseInitializer.EnsureCreated(_factory);
        _nodes = new SqliteNodeRepository(_factory);
        _detailsRepo = new SqliteNodeDetailsRepository(_factory);
        _service = new ChildInformationService(_nodes, _detailsRepo, new AlwaysAcquire(), new NullLogger(), "tester");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private NodeId ChildId(string name) => _nodes.GetAllNodes().Single(n => n.Name == name).Id;

    [Fact] // Bilgi kaydı aitliği ve konumu değiştirmez
    public void BilgiKaydi_AitlikVeKonumuDegistirmez()
    {
        var child = ChildId("Karlılık");
        _nodes.AddOrActivateChild(NodeCatalogSeed.RepoNodeId, child, 111, 222, "t");
        var relId = _nodes.GetChildRelations(NodeCatalogSeed.RepoNodeId).Single(r => r.ChildNodeId.Equals(child)).Id;

        _service.Save(new ChildInformationInput(child, relId, NodeCatalogSeed.RepoNodeId, "MKIP101", null, "açıklama"));

        var rel = _nodes.GetChildRelations(NodeCatalogSeed.RepoNodeId).Single(r => r.ChildNodeId.Equals(child));
        Assert.Equal(111, rel.PositionX);
        Assert.Equal(222, rel.PositionY);
        Assert.True(rel.IsActive);
    }

    [Fact] // Ağaçtan çıkarma bilgiyi SİLMEZ
    public void AgactanCikarma_BilgiKorunur()
    {
        var child = ChildId("Reeskont");
        _nodes.AddOrActivateChild(NodeCatalogSeed.RepoNodeId, child, null, null, "t");
        var relId = _nodes.GetChildRelations(NodeCatalogSeed.RepoNodeId).Single(r => r.ChildNodeId.Equals(child)).Id;
        _service.Save(new ChildInformationInput(child, relId, NodeCatalogSeed.RepoNodeId, null, null, "repo açıklaması"));

        _nodes.RemoveChild(NodeCatalogSeed.RepoNodeId, child, "t"); // ağaçtan çıkar

        Assert.NotNull(_detailsRepo.GetNodeRelationDetails(relId));   // bilgi korundu
        Assert.Equal("repo açıklaması", _detailsRepo.GetNodeRelationDetails(relId)!.ShortDescription);
    }

    [Fact] // Öğe tekrar aynı başlığa eklenince eski bilgi yeniden görünür
    public void TekrarEkleme_EskiBilgiGorunur()
    {
        var child = ChildId("Yetkilendir");
        _nodes.AddOrActivateChild(NodeCatalogSeed.RepoNodeId, child, null, null, "t");
        var relId = _nodes.GetChildRelations(NodeCatalogSeed.RepoNodeId).Single(r => r.ChildNodeId.Equals(child)).Id;
        _service.Save(new ChildInformationInput(child, relId, NodeCatalogSeed.RepoNodeId, null, null, "eski açıklama"));

        _nodes.RemoveChild(NodeCatalogSeed.RepoNodeId, child, "t");
        _nodes.AddOrActivateChild(NodeCatalogSeed.RepoNodeId, child, null, null, "t"); // tekrar ekle

        // Aynı bağlantı yeniden aktifleşir; bilgi (aynı NodeRelationId) tekrar görünür.
        var info = _service.Load(child, NodeCatalogSeed.RepoNodeId);
        Assert.Equal(relId, info.NodeRelationId);
        Assert.Equal("eski açıklama", info.ShortDescription);
    }

    private sealed class AlwaysAcquire : IWriteLockService
    {
        public WriteLockResult TryAcquire() => new(true, new WriteLockStatus(WriteLockState.HeldByCurrentUser, null));
        public void Heartbeat() { }
        public void Release() { }
        public WriteLockStatus GetStatus() => new(WriteLockState.HeldByCurrentUser, null);
    }

    private sealed class NullLogger : IAppLogger
    {
        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}
