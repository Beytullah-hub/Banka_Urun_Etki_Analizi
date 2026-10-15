using System.Collections.Concurrent;
using BankProductImpact.Application.Locking;
using BankProductImpact.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BankProductImpact.Tests.Unit;

/// <summary>
/// Tek yazıcı kilidi testleri (teknik doküman 3, 15): ilk alır, ikinci alamaz, okuma sürer,
/// heartbeat, süresi dolmuş kilit yeniden alınır, kapanışta bırakılır, paralel alımda tek başarı.
/// Her <see cref="SqliteWriteLockService"/> örneği ayrı bir "kullanıcı" (ayrı OwnerToken) temsil eder.
/// </summary>
public sealed class SqliteWriteLockTests : IDisposable
{
    private readonly string _dir;
    private readonly SqliteConnectionFactory _factory;

    public SqliteWriteLockTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "bpi_lock_" + Guid.NewGuid().ToString("N"));
        _factory = new SqliteConnectionFactory(Path.Combine(_dir, "ProductImpact.db"));
        SqliteDatabaseInitializer.EnsureCreated(_factory);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    private SqliteWriteLockService NewService(TimeSpan? ttl = null) => new(_factory, ttl);

    [Fact] // İlk kullanıcı kilidi alır
    public void IlkKullanici_KilidiAlir()
    {
        var result = NewService().TryAcquire();

        Assert.True(result.Acquired);
        Assert.Equal(WriteLockState.HeldByCurrentUser, result.Status.State);
    }

    [Fact] // İkinci kullanıcı, birinci tutarken kilidi alamaz (salt okunur)
    public void IkinciKullanici_KilidiAlamaz()
    {
        var first = NewService();
        var second = NewService();

        Assert.True(first.TryAcquire().Acquired);
        var secondResult = second.TryAcquire();

        Assert.False(secondResult.Acquired);
        Assert.Equal(WriteLockState.HeldByOther, secondResult.Status.State);
        Assert.NotNull(secondResult.Status.Holder);
    }

    [Fact] // Kilit tutulurken okuma işlemleri devam eder
    public void KilitTutulurken_OkumaDevamEder()
    {
        NewService().TryAcquire();

        var nodes = new SqliteNodeRepository(_factory).GetRootNodes();
        Assert.NotEmpty(nodes);
    }

    [Fact] // Heartbeat kilidin süresini uzatır (ileri bir ExpiresAt yazar)
    public void Heartbeat_SureyiUzatir()
    {
        var service = NewService(TimeSpan.FromSeconds(30));
        service.TryAcquire();

        service.Heartbeat();
        var status = service.GetStatus();

        Assert.Equal(WriteLockState.HeldByCurrentUser, status.State);
        Assert.True(status.Holder!.ExpiresAt > DateTime.UtcNow);
    }

    [Fact] // Süresi dolmuş kilit (beklenmedik kapanış) yeniden alınabilir
    public void SuresiDolmusKilit_YenidenAlinabilir()
    {
        var first = NewService(TimeSpan.FromSeconds(30));
        Assert.True(first.TryAcquire().Acquired);

        // Beklenmedik kapanışı simüle et: kilidi geçmişte süresi dolmuş yap.
        ForceExpire();

        var second = NewService();
        Assert.True(second.TryAcquire().Acquired);

        // Artık kilit ikinci kullanıcıda; ilkinin gözünde "başkası tutuyor".
        Assert.Equal(WriteLockState.HeldByOther, first.GetStatus().State);
    }

    [Fact] // Normal kapanışta kilit bırakılır ve başkası alabilir
    public void NormalKapanista_KilitBirakilir()
    {
        var first = NewService();
        first.TryAcquire();
        first.Release();

        var second = NewService();
        Assert.True(second.TryAcquire().Acquired);
    }

    [Fact] // Paralel kilit alma denemesinde yalnızca bir kullanıcı başarılı olur
    public void ParalelAlim_YalnizcaBiriBasarili()
    {
        var services = Enumerable.Range(0, 8).Select(_ => NewService()).ToList();
        var acquired = new ConcurrentBag<bool>();

        Parallel.ForEach(services, s => acquired.Add(s.TryAcquire().Acquired));

        Assert.Equal(1, acquired.Count(a => a));
    }

    private void ForceExpire()
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "UPDATE ApplicationWriteLock SET ExpiresAt = $past WHERE IsActive = 1;";
        cmd.Parameters.AddWithValue("$past", DateTime.UtcNow.AddMinutes(-5).ToString("o"));
        cmd.ExecuteNonQuery();
    }
}
