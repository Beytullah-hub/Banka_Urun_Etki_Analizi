using BankProductImpact.Application.Abstractions;
using BankProductImpact.Application.Locking;
using BankProductImpact.Application.Nodes;
using BankProductImpact.Domain.Common;
using BankProductImpact.Infrastructure.Nodes;
using Xunit;

namespace BankProductImpact.Tests.Unit;

/// <summary>
/// ChildInformationService: bilgi yükleme/kaydetme (tek katman — bağlantı bağlamına özel),
/// aynı öğenin farklı başlıklar altında ayrı bilgisi, doğrulama sınırları, normalize,
/// yazma kilidi kontrolü, bağlantı yokken kaydetmenin reddi. Alanlar: ekran adı, batch
/// adı, kısa açıklama.
/// </summary>
public sealed class ChildInformationServiceTests
{
    private readonly InMemoryNodeRepository _nodes = new();
    private readonly InMemoryNodeDetailsRepository _details = new();

    private ChildInformationService CreateService(IWriteLockService writeLock) =>
        new(_nodes, _details, writeLock, new NullLogger(), actor: "tester");

    private NodeId ChildId(string name) => _nodes.GetAllNodes().First(n => n.Name == name).Id;

    private NodeRelationId Bind(NodeId parent, NodeId child)
    {
        _nodes.AddOrActivateChild(parent, child, null, null, "t");
        return _nodes.GetChildRelations(parent).First(r => r.ChildNodeId.Equals(child)).Id;
    }

    [Fact] // Kaydedilen bilgi (bağlantı bağlamında) geri yüklenir
    public void Load_BilgiDoner()
    {
        var svc = CreateService(new AlwaysAcquire());
        var child = ChildId("Raporlama");
        var relId = Bind(NodeCatalogSeed.RepoNodeId, child);

        svc.Save(new ChildInformationInput(child, relId, NodeCatalogSeed.RepoNodeId,
            "MKIP101", "GUNSONU_BATCH", "kısa açıklama"));

        var info = svc.Load(child, NodeCatalogSeed.RepoNodeId);
        Assert.Equal("MKIP101", info.ScreenName);
        Assert.Equal("GUNSONU_BATCH", info.BatchName);
        Assert.Equal("kısa açıklama", info.ShortDescription);
        Assert.Equal("Raporlama", info.NodeName);
    }

    [Fact] // Aynı öğe farklı başlıklar altında ayrı bilgiye sahiptir (karışmaz)
    public void FarkliBasliklar_AyriBilgi()
    {
        var svc = CreateService(new AlwaysAcquire());
        var child = ChildId("Raporlama");
        var repoRel = Bind(NodeCatalogSeed.RepoNodeId, child);
        var otherParent = ChildId("Yatırım Fonu İşlemleri (Bankamız Fon İşlemleri)");
        var otherRel = Bind(otherParent, child);

        svc.Save(new ChildInformationInput(child, repoRel, NodeCatalogSeed.RepoNodeId, "REPO_EKRAN", null, null));
        svc.Save(new ChildInformationInput(child, otherRel, otherParent, "FON_EKRAN", null, null));

        Assert.Equal("REPO_EKRAN", svc.Load(child, NodeCatalogSeed.RepoNodeId).ScreenName);
        Assert.Equal("FON_EKRAN", svc.Load(child, otherParent).ScreenName);
    }

    [Fact] // Uzunluk sınırı aşılırsa reddedilir ve hiçbir şey yazılmaz
    public void Save_UzunlukSiniri_Reddedilir()
    {
        var svc = CreateService(new AlwaysAcquire());
        var child = ChildId("Karlılık");
        var relId = Bind(NodeCatalogSeed.RepoNodeId, child);
        var tooLong = new string('a', ChildInformationLimits.ShortDescription + 1);

        var result = svc.Save(new ChildInformationInput(child, relId, NodeCatalogSeed.RepoNodeId, null, null, tooLong));

        Assert.False(result.Success);
        Assert.Contains("Kısa açıklama", result.Message);
        Assert.Null(_details.GetNodeRelationDetails(relId));
    }

    [Fact] // Kilit başka kullanıcıdaysa kaydetme yapılmaz
    public void Save_KilitBaskasinda_Reddedilir()
    {
        var svc = CreateService(new HeldByOther());
        var child = ChildId("Reeskont");
        var relId = Bind(NodeCatalogSeed.RepoNodeId, child);

        var result = svc.Save(new ChildInformationInput(child, relId, NodeCatalogSeed.RepoNodeId, "x", null, null));

        Assert.False(result.Success);
        Assert.Contains("Salt okunur", result.Message);
        Assert.Null(_details.GetNodeRelationDetails(relId));
    }

    [Fact] // Kilit bizdeyse kaydeder ve güncel modeli döndürür
    public void Save_KilitBizde_KaydederVeYeniler()
    {
        var svc = CreateService(new AlwaysAcquire());
        var child = ChildId("Yetkilendir");
        var relId = Bind(NodeCatalogSeed.RepoNodeId, child);

        var result = svc.Save(new ChildInformationInput(child, relId, NodeCatalogSeed.RepoNodeId, "MKIP200", null, null));

        Assert.True(result.Success);
        Assert.Equal("MKIP200", result.Reloaded!.ScreenName);
        Assert.Equal("MKIP200", _details.GetNodeRelationDetails(relId)!.ScreenName);
    }

    [Fact] // Normalize: yalnızca boşluktan oluşan metin null saklanır, diğerleri trim'lenir
    public void Save_Normalize_BosNullTrim()
    {
        var svc = CreateService(new AlwaysAcquire());
        var child = ChildId("İşlem izleme");
        var relId = Bind(NodeCatalogSeed.RepoNodeId, child);

        svc.Save(new ChildInformationInput(child, relId, NodeCatalogSeed.RepoNodeId, "   ", "  MKIP_BATCH  ", null));

        var d = _details.GetNodeRelationDetails(relId)!;
        Assert.Null(d.ScreenName);              // yalnızca boşluk → null
        Assert.Equal("MKIP_BATCH", d.BatchName); // trim
    }

    [Fact] // Bağlantı yoksa (öğe hiçbir başlığa bağlı değilse) bilgi kaydedilemez
    public void Save_IliskiYoksa_Reddedilir()
    {
        var svc = CreateService(new AlwaysAcquire());
        var child = ChildId("Mizan/muhasebe");

        var result = svc.Save(new ChildInformationInput(child, null, null, "x", null, null));

        Assert.False(result.Success);
        Assert.Contains("başlığa bağlı", result.Message);
    }

    // ---- Test ikizleri ----

    private sealed class AlwaysAcquire : IWriteLockService
    {
        public WriteLockResult TryAcquire() => new(true, new WriteLockStatus(WriteLockState.HeldByCurrentUser, null));
        public void Heartbeat() { }
        public void Release() { }
        public WriteLockStatus GetStatus() => new(WriteLockState.HeldByCurrentUser, null);
    }

    private sealed class HeldByOther : IWriteLockService
    {
        private readonly WriteLockInfo _other = new("baska", "PC-2", 9, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(1));
        public WriteLockResult TryAcquire() => new(false, new WriteLockStatus(WriteLockState.HeldByOther, _other));
        public void Heartbeat() { }
        public void Release() { }
        public WriteLockStatus GetStatus() => new(WriteLockState.HeldByOther, _other);
    }

    private sealed class NullLogger : IAppLogger
    {
        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}
