using BankProductImpact.Application.Abstractions;
using BankProductImpact.Application.Locking;
using BankProductImpact.Infrastructure.Nodes;
using BankProductImpact.Infrastructure.Sqlite;
using BankProductImpact.Presentation.ViewModels;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BankProductImpact.Tests.Unit;

/// <summary>
/// "+ Yeni Parent Ekle" davranışı: yeni kayıt CanBeRoot=true olarak eklenir, sol
/// parent listesinde anında görünür ve otomatik seçilir; mükerrer/boş isim reddedilir;
/// yazma kilidi gerektirir; kilit başka kullanıcıdaysa eklenmez; işlem audit'e yazılır.
/// </summary>
public sealed class MainViewModelAddParentTests
{
    private static MainViewModel CreateViewModel(IWriteLockService? writeLock = null) =>
        new("test", new InMemoryNodeRepository(), writeLock);

    [Fact] // Yeni parent eklenir, CanBeRoot=true olur, listede görünür ve otomatik seçilir
    public void YeniParent_EklenirVeOtomatikSecilir()
    {
        var vm = CreateViewModel(new AlwaysAcquireLock());
        vm.AddParent!.OpenCommand.Execute(null);
        vm.AddParent.Name = "Yeni Test Parenti";

        vm.AddParent.SaveCommand.Execute(null);

        Assert.False(vm.AddParent.IsOpen);
        Assert.Contains(vm.Parents, p => p.Name == "Yeni Test Parenti");
        Assert.Equal("Yeni Test Parenti", vm.SelectedParent!.Name);
        Assert.Equal("Yeni Test Parenti", vm.HeaderTitle);
    }

    [Fact] // Boş isimle parent eklenemez
    public void BosIsimle_ParentEklenemez()
    {
        var vm = CreateViewModel(new AlwaysAcquireLock());
        vm.AddParent!.OpenCommand.Execute(null);
        vm.AddParent.Name = "   ";

        vm.AddParent.SaveCommand.Execute(null);

        Assert.True(vm.AddParent.IsOpen); // form kapanmadı
        Assert.True(vm.AddParent.HasFeedback);
    }

    [Fact] // Aynı isimde (büyük/küçük harf ve boşluk farkı gözetmeksizin) parent eklenemez
    public void AyniIsimle_ParentEklenemez()
    {
        var vm = CreateViewModel(new AlwaysAcquireLock());
        vm.AddParent!.OpenCommand.Execute(null);
        vm.AddParent.Name = "  repo  "; // mevcut "Repo" ile eşleşir

        vm.AddParent.SaveCommand.Execute(null);

        Assert.True(vm.AddParent.IsOpen);
        Assert.Equal("Bu isimde bir başlık zaten bulunuyor.", vm.AddParent.Feedback);
    }

    [Fact] // Kilit başka kullanıcıdaysa parent eklenmez
    public void KilitBaskasindaysa_ParentEklenmez()
    {
        var vm = CreateViewModel(new HeldByOtherLock());
        vm.AddParent!.OpenCommand.Execute(null);
        vm.AddParent.Name = "Kilitli Deneme";

        vm.AddParent.SaveCommand.Execute(null);

        Assert.True(vm.AddParent.IsOpen);
        Assert.True(vm.AddParent.HasFeedback);
        Assert.DoesNotContain(vm.Parents, p => p.Name == "Kilitli Deneme");
    }

    [Fact] // Vazgeç: hiçbir şey eklenmez, form kapanır
    public void Vazgec_HicbirSeyEklenmez()
    {
        var vm = CreateViewModel(new AlwaysAcquireLock());
        vm.AddParent!.OpenCommand.Execute(null);
        vm.AddParent.Name = "Vazgecilecek";

        vm.AddParent.CancelCommand.Execute(null);

        Assert.False(vm.AddParent.IsOpen);
        Assert.DoesNotContain(vm.Parents, p => p.Name == "Vazgecilecek");
    }

    [Fact] // Parent ekleme SQLite'ta AuditLog'a yazılır
    public void ParentEkleme_AuditeKaydedilir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bpi_addparent_" + Guid.NewGuid().ToString("N"));
        try
        {
            var factory = new SqliteConnectionFactory(Path.Combine(dir, "ProductImpact.db"));
            SqliteDatabaseInitializer.EnsureCreated(factory);
            var nodes = new SqliteNodeRepository(factory);
            var vm = new MainViewModel("test", nodes, new AlwaysAcquireLock());
            var before = AuditCount(factory);

            vm.AddParent!.OpenCommand.Execute(null);
            vm.AddParent.Name = "Audit Test Parenti";
            vm.AddParent.SaveCommand.Execute(null);

            Assert.True(AuditCount(factory) >= before + 1);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }

    private static long AuditCount(SqliteConnectionFactory factory)
    {
        using var c = factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM AuditLog WHERE ActionType = 'CreateNode';";
        return (long)(cmd.ExecuteScalar() ?? 0L);
    }

    private sealed class AlwaysAcquireLock : IWriteLockService
    {
        public WriteLockResult TryAcquire() => new(true, new WriteLockStatus(WriteLockState.HeldByCurrentUser, null));
        public void Heartbeat() { }
        public void Release() { }
        public WriteLockStatus GetStatus() => WriteLockStatus.Free;
    }

    private sealed class HeldByOtherLock : IWriteLockService
    {
        private readonly WriteLockInfo _other = new("baska", "PC-2", 111, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(1));
        public WriteLockResult TryAcquire() => new(false, new WriteLockStatus(WriteLockState.HeldByOther, _other));
        public void Heartbeat() { }
        public void Release() { }
        public WriteLockStatus GetStatus() => new(WriteLockState.HeldByOther, _other);
    }
}
