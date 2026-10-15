using BankProductImpact.Application.Abstractions;
using BankProductImpact.Application.Locking;

namespace BankProductImpact.Infrastructure.Locking;

/// <summary>
/// SQLite dışı veri kaynakları (Mock/Excel) için tek süreçlik kilit. Paylaşılan bir
/// veritabanı olmadığından kilit her zaman alınabilir; yalnızca yerel düzenleme durumunu
/// tutar. Çoklu kullanıcı senaryosu yalnızca SQLite ile geçerlidir.
/// </summary>
public sealed class InMemoryWriteLockService : IWriteLockService
{
    private bool _held;

    public WriteLockResult TryAcquire()
    {
        _held = true;
        return new WriteLockResult(true, new WriteLockStatus(WriteLockState.HeldByCurrentUser, LocalInfo()));
    }

    public void Heartbeat() { /* tek süreç: gerek yok */ }

    public void Release() => _held = false;

    public WriteLockStatus GetStatus() =>
        _held
            ? new WriteLockStatus(WriteLockState.HeldByCurrentUser, LocalInfo())
            : WriteLockStatus.Free;

    private static WriteLockInfo LocalInfo()
    {
        var now = DateTime.UtcNow;
        return new WriteLockInfo(Environment.UserName, Environment.MachineName, Environment.ProcessId, now, now, now.AddMinutes(30));
    }
}
