using BankProductImpact.Application.Abstractions;
using BankProductImpact.Application.Locking;
using BankProductImpact.Application.Nodes;
using BankProductImpact.Domain.Common;
using BankProductImpact.Infrastructure.Nodes;
using BankProductImpact.Presentation.ViewModels;
using Xunit;

namespace BankProductImpact.Tests.Unit;

/// <summary>
/// Bilgi düzenleme formu: form aç/kaydet/vazgeç, kaydedilmemiş değişiklik takibi,
/// yazma kilidi (başkasında açılmaz; alınan kilit bırakılır), yapısal "Ağacı Düzenle"
/// moduna bağlı olmama.
/// </summary>
public sealed class MainViewModelChildDetailEditTests
{
    private (MainViewModel Vm, InMemoryNodeDetailsRepository Details, NodeRelationId RelId) Setup(IWriteLockService writeLock)
    {
        var nodes = new InMemoryNodeRepository();
        var details = new InMemoryNodeDetailsRepository();
        var svc = new ChildInformationService(nodes, details, writeLock, new NullLogger(), "tester");
        var vm = new MainViewModel("test", nodes, writeLock, svc);

        var parent = vm.Parents.First(p => !p.IsRepo);
        vm.SelectedParent = parent;
        var child = nodes.GetAllNodes().First(n => n.Name == "Raporlama").Id;
        nodes.AddOrActivateChild(parent.NodeId, child, null, null, "t");
        var relId = nodes.GetChildRelations(parent.NodeId).First(r => r.ChildNodeId.Equals(child)).Id;
        vm.PinChildInfo(child);
        return (vm, details, relId);
    }

    [Fact] // "Bilgileri Düzenle" formu açar, mevcut değerleri doldurur
    public void BeginEdit_FormAcar()
    {
        var (vm, _, _) = Setup(new AlwaysAcquire());

        vm.BeginEditChildDetails();

        Assert.True(vm.IsEditingChildDetails);
        Assert.NotNull(vm.ChildDetailEdit);
        Assert.Equal("Raporlama", vm.ChildDetailEdit!.NodeName);
    }

    [Fact] // Kaydet: servise yazar, paneli günceller, düzenlemeden çıkar
    public void Save_YazarVePaneliGunceller()
    {
        var (vm, details, relId) = Setup(new AlwaysAcquire());
        vm.BeginEditChildDetails();
        vm.ChildDetailEdit!.ShortDescription = "yeni açıklama";

        vm.ChildDetailEdit.SaveCommand.Execute(null);

        Assert.False(vm.IsEditingChildDetails);
        Assert.Equal("yeni açıklama", vm.SelectedChildInfo!.ShortDescription);
        Assert.Equal("yeni açıklama", details.GetNodeRelationDetails(relId)!.ShortDescription);
    }

    [Fact] // Vazgeç: hiçbir şey yazmaz ve düzenlemeden çıkar
    public void Vazgec_YazmazVeCikar()
    {
        var (vm, details, relId) = Setup(new AlwaysAcquire());
        vm.BeginEditChildDetails();
        vm.ChildDetailEdit!.ShortDescription = "kaydedilmeyecek";

        vm.ChildDetailEdit.CancelCommand.Execute(null);

        Assert.False(vm.IsEditingChildDetails);
        Assert.Null(details.GetNodeRelationDetails(relId)); // hiçbir şey yazılmadı
    }

    [Fact] // Kilit başka kullanıcıdaysa form açılmaz, uyarı gösterilir
    public void KilitBaskasinda_FormAcilmaz()
    {
        var (vm, _, _) = Setup(new HeldByOther());

        vm.BeginEditChildDetails();

        Assert.False(vm.IsEditingChildDetails);
        Assert.True(vm.HasChildDetailNotice);
    }

    [Fact] // Alan değişince kaydedilmemiş değişiklik algılanır
    public void AlanDegisince_Dirty()
    {
        var (vm, _, _) = Setup(new AlwaysAcquire());
        vm.BeginEditChildDetails();

        vm.ChildDetailEdit!.ShortDescription = "değişti";

        Assert.True(vm.HasUnsavedChildDetails);
    }

    [Fact] // Bilgi düzenleme yapısal "Ağacı Düzenle" modunu gerektirmez
    public void BilgiDuzenleme_YapisalModaBagliDegil()
    {
        var (vm, _, _) = Setup(new AlwaysAcquire());
        Assert.False(vm.IsEditMode); // yapısal mod kapalı

        vm.BeginEditChildDetails();

        Assert.True(vm.IsEditingChildDetails);
    }

    [Fact] // Bilgi düzenleme için alınan kilit, kayıt sonrası bırakılır
    public void Save_AlinanKilidiBirakir()
    {
        var lockSvc = new CountingLock();
        var (vm, _, _) = Setup(lockSvc);
        vm.BeginEditChildDetails();      // kilidi alır (yapısal mod kapalı)
        vm.ChildDetailEdit!.ShortDescription = "x";

        vm.ChildDetailEdit.SaveCommand.Execute(null);

        Assert.True(lockSvc.ReleaseCount >= 1);
    }

    // ---- Test ikizleri ----

    private sealed class AlwaysAcquire : IWriteLockService
    {
        public WriteLockResult TryAcquire() => new(true, new WriteLockStatus(WriteLockState.HeldByCurrentUser, null));
        public void Heartbeat() { }
        public void Release() { }
        public WriteLockStatus GetStatus() => new(WriteLockState.HeldByCurrentUser, null);
    }

    private sealed class CountingLock : IWriteLockService
    {
        public int ReleaseCount { get; private set; }
        public WriteLockResult TryAcquire() => new(true, new WriteLockStatus(WriteLockState.HeldByCurrentUser, null));
        public void Heartbeat() { }
        public void Release() => ReleaseCount++;
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
