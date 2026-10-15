using BankProductImpact.Application.Abstractions;
using BankProductImpact.Application.Locking;
using BankProductImpact.Infrastructure.Nodes;
using BankProductImpact.Presentation.ViewModels;
using Xunit;

namespace BankProductImpact.Tests.Unit;

/// <summary>
/// Düzenleme modu / tek yazıcı kilidi ViewModel davranışı: varsayılan salt okunur;
/// kilit alınınca düzenleme; başkası tutarsa salt okunur; kapanışta bırakma. Repo,
/// sayısal etki sisteminin kaldırılmasından sonra artık normal bir parent'tır —
/// diğerleri gibi düzenlenebilir.
/// </summary>
public sealed class MainViewModelEditModeTests
{
    private static MainViewModel CreateViewModel(IWriteLockService writeLock) =>
        new("test", new InMemoryNodeRepository(), writeLock);

    [Fact] // Varsayılan: salt okunur (düzenleme modu kapalı)
    public void Varsayilan_SaltOkunur()
    {
        var vm = CreateViewModel(new AlwaysAcquireLock());

        Assert.False(vm.IsEditMode);
        Assert.True(vm.IsReadOnly);
        Assert.Equal("Ağacı Düzenle", vm.EditModeButtonText);
    }

    [Fact] // Repo seçiliyken "Ağacı Düzenle" da aktiftir (Repo artık normal bir parent'tır)
    public void RepoSecili_DuzenlemeDugmesiAktif()
    {
        var vm = CreateViewModel(new AlwaysAcquireLock());

        Assert.True(vm.SelectedParent!.IsRepo);
        Assert.True(vm.CanEditSelectedTree);
        Assert.True(vm.ToggleEditModeCommand.CanExecute(null));
    }

    [Fact] // Repo dışı bir parent seçilince de "Ağacı Düzenle" aktif kalır
    public void FarkliParentSecilince_DuzenlemeDugmesiAktif()
    {
        var vm = CreateViewModel(new AlwaysAcquireLock());

        vm.SelectedParent = vm.Parents.First(p => !p.IsRepo);

        Assert.True(vm.CanEditSelectedTree);
        Assert.True(vm.ToggleEditModeCommand.CanExecute(null));
    }

    [Fact] // Kilit boşsa düzenleme moduna girilir
    public void KilitBossa_DuzenlemeModunaGirilir()
    {
        var vm = CreateViewModel(new AlwaysAcquireLock());

        vm.ToggleEditModeCommand.Execute(null);

        Assert.True(vm.IsEditMode);
        Assert.False(vm.IsReadOnly);
        Assert.Equal("Düzenlemeyi Bitir", vm.EditModeButtonText);

        vm.OnShutdown();
    }

    [Fact] // Kilit başka kullanıcıdaysa salt okunur kalınır ve uyarı gösterilir
    public void KilitBaskasindaysa_SaltOkunurKalir()
    {
        var vm = CreateViewModel(new HeldByOtherLock());

        vm.ToggleEditModeCommand.Execute(null);

        Assert.False(vm.IsEditMode);
        Assert.True(vm.IsReadOnly);
        Assert.Contains("Salt okunur", vm.EditModeStatus);
    }

    [Fact] // Düzenlemeyi bitirince kilit bırakılır
    public void DuzenlemeyiBitirince_KilitBirakilir()
    {
        var spy = new AlwaysAcquireLock();
        var vm = CreateViewModel(spy);

        vm.ToggleEditModeCommand.Execute(null); // gir
        vm.ToggleEditModeCommand.Execute(null); // çık

        Assert.False(vm.IsEditMode);
        Assert.True(spy.ReleaseCount >= 1);
    }

    [Fact] // Pencere kapanışında (OnShutdown) kilit bırakılır
    public void Kapanista_KilitBirakilir()
    {
        var spy = new AlwaysAcquireLock();
        var vm = CreateViewModel(spy);
        vm.ToggleEditModeCommand.Execute(null);

        vm.OnShutdown();

        Assert.True(spy.ReleaseCount >= 1);
    }

    // --- Test ikizleri ---

    private sealed class AlwaysAcquireLock : IWriteLockService
    {
        public int ReleaseCount { get; private set; }
        public WriteLockResult TryAcquire() =>
            new(true, new WriteLockStatus(WriteLockState.HeldByCurrentUser, null));
        public void Heartbeat() { }
        public void Release() => ReleaseCount++;
        public WriteLockStatus GetStatus() => WriteLockStatus.Free;
    }

    private sealed class HeldByOtherLock : IWriteLockService
    {
        private readonly WriteLockInfo _other = new("baska", "PC-2", 111, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(1));
        public WriteLockResult TryAcquire() =>
            new(false, new WriteLockStatus(WriteLockState.HeldByOther, _other));
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
