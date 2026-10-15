using BankProductImpact.Application.Abstractions;
using BankProductImpact.Application.Locking;
using BankProductImpact.Application.Nodes;
using BankProductImpact.Domain.Common;
using BankProductImpact.Infrastructure.Nodes;
using BankProductImpact.Presentation.ViewModels;
using Xunit;

namespace BankProductImpact.Tests.Unit;

/// <summary>
/// Bilgi paneli davranışı: hover önizleme, tıklama sabitleme, kapatma, boş durum mesajı,
/// hover cache. Bilgi tek katmandır (bağlantı bağlamına özel).
/// </summary>
public sealed class MainViewModelInfoTests
{
    private readonly InMemoryNodeRepository _nodes = new();
    private readonly InMemoryNodeDetailsRepository _details = new();

    private MainViewModel CreateViewModel(IChildInformationService service) =>
        new("test", _nodes, writeLock: null, childInfoService: service);

    private ChildInformationService RealService() =>
        new(_nodes, _details, new AlwaysAcquire(), new NullLogger(), actor: "tester");

    private NodeId ChildId(string name) => _nodes.GetAllNodes().First(n => n.Name == name).Id;

    private NodeRelationId Bind(NodeId child)
    {
        _nodes.AddOrActivateChild(NodeCatalogSeed.RepoNodeId, child, null, null, "t");
        return _nodes.GetChildRelations(NodeCatalogSeed.RepoNodeId).First(r => r.ChildNodeId.Equals(child)).Id;
    }

    [Fact] // Hover: panel açılır, içerik yoksa boş mesaj gösterilir
    public void Hover_PanelAcilir_BosMesaj()
    {
        var vm = CreateViewModel(RealService());

        vm.PreviewChildInfo(ChildId("Raporlama"));

        Assert.True(vm.IsChildInfoVisible);
        Assert.False(vm.SelectedChildInfo!.HasContent);
        Assert.Equal(ChildInfoViewModel.EmptyMessage, vm.SelectedChildInfo.EmptyStateMessage);
        Assert.Equal("Raporlama", vm.SelectedChildInfo.NodeName);
    }

    [Fact] // Kaydedilen bilgi hover'da görünür
    public void KaydedilenBilgi_Gorunur()
    {
        var svc = RealService();
        var vm = CreateViewModel(svc);
        var child = ChildId("Karlılık");
        var relId = Bind(child);

        svc.Save(new ChildInformationInput(child, relId, NodeCatalogSeed.RepoNodeId, "MKIP101", null, "kısa açıklama"));
        vm.InvalidateChildInfo(child);
        vm.PreviewChildInfo(child);

        Assert.True(vm.SelectedChildInfo!.HasContent);
        Assert.Equal("MKIP101", vm.SelectedChildInfo.ScreenName);
        Assert.Equal("kısa açıklama", vm.SelectedChildInfo.ShortDescription);
    }

    [Fact] // Tıklama paneli sabitler; sabitken hover başka öğeye geçmez
    public void Tiklama_Sabitler()
    {
        var vm = CreateViewModel(RealService());

        vm.PinChildInfo(ChildId("Raporlama"));
        vm.PreviewChildInfo(ChildId("Karlılık"));

        Assert.Equal("Raporlama", vm.SelectedChildInfo!.NodeName);
    }

    [Fact] // Kapatma panelini gizler
    public void Kapatma_Gizler()
    {
        var vm = CreateViewModel(RealService());
        vm.PinChildInfo(ChildId("Raporlama"));

        vm.CloseChildInfo();

        Assert.False(vm.IsChildInfoVisible);
        Assert.Null(vm.SelectedChildInfo);
    }

    [Fact] // Aynı öğe için tekrar hover veritabanına tekrar sorgu atmaz (cache)
    public void Hover_AyniOge_TekSorgu()
    {
        var spy = new CountingService(EmptyInfo("Raporlama"));
        var vm = CreateViewModel(spy);
        var child = ChildId("Raporlama");

        vm.PreviewChildInfo(child);
        vm.PreviewChildInfo(child);

        Assert.Equal(1, spy.LoadCount);
    }

    private static ChildInformation EmptyInfo(string name) =>
        new(new NodeId("x"), null, null, name, null, null, null, null, null);

    // ---- Test ikizleri ----

    private sealed class CountingService : IChildInformationService
    {
        private readonly ChildInformation _canned;
        public int LoadCount { get; private set; }
        public CountingService(ChildInformation canned) => _canned = canned;
        public ChildInformation Load(NodeId childId, NodeId? parentId) { LoadCount++; return _canned; }
        public ChildInformationSaveResult Save(ChildInformationInput input) => ChildInformationSaveResult.Ok(_canned);
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
