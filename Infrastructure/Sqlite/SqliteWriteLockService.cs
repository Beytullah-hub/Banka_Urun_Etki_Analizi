using System.Globalization;
using BankProductImpact.Application.Abstractions;
using BankProductImpact.Application.Locking;
using Microsoft.Data.Sqlite;

namespace BankProductImpact.Infrastructure.Sqlite;

/// <summary>
/// <see cref="IWriteLockService"/>'nin SQLite uygulaması (teknik doküman 3). Kilit alımı
/// BEGIN IMMEDIATE transaction'ı ve <c>UX_WriteLock_Active</c> kısmi UNIQUE index'i ile
/// atomiktir: paralel denemelerde yalnızca bir istemci başarılı olur.
///
/// Her uygulama örneği kendi <see cref="_ownerToken"/>'ıyla tanınır; böylece aynı kullanıcı/
/// makinede iki örnek bile birbirinden ayırt edilir ve bir istemcinin kilidi yanlışlıkla
/// başkası tarafından kaldırılmaz.
/// </summary>
public sealed class SqliteWriteLockService : IWriteLockService
{
    private const string DefaultLockKey = "TreeEdit";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly SqliteConnectionFactory _factory;
    private readonly string _lockKey;
    private readonly TimeSpan _ttl;

    // Bu uygulama örneğinin kimliği.
    private readonly string _ownerToken = Guid.NewGuid().ToString("N");
    private readonly string _userName = Environment.UserName;
    private readonly string _machineName = Environment.MachineName;
    private readonly int _processId = Environment.ProcessId;

    public SqliteWriteLockService(SqliteConnectionFactory factory, TimeSpan? ttl = null, string? lockKey = null)
    {
        _factory = factory;
        _lockKey = lockKey ?? DefaultLockKey;
        _ttl = ttl ?? TimeSpan.FromSeconds(30);
    }

    public WriteLockResult TryAcquire()
    {
        using var connection = _factory.Open();
        // IMMEDIATE: yazma kilidini hemen al; kilit alıcıları temiz biçimde sıralanır.
        Exec(connection, "BEGIN IMMEDIATE;");
        try
        {
            var now = DateTime.UtcNow;
            var existing = ReadActiveLock(connection);

            if (existing is not null)
            {
                var ownedByMe = existing.OwnerToken == _ownerToken;
                var expired = existing.ExpiresAt <= now;

                if (ownedByMe)
                {
                    RefreshMyLock(connection, now);
                    Exec(connection, "COMMIT;");
                    return new WriteLockResult(true, new WriteLockStatus(WriteLockState.HeldByCurrentUser, ToInfo(existing, now)));
                }

                if (!expired)
                {
                    // Başka kullanıcıda ve süresi dolmamış → alınamaz.
                    Exec(connection, "COMMIT;");
                    return new WriteLockResult(false, new WriteLockStatus(WriteLockState.HeldByOther, ToInfo(existing)));
                }

                // Süresi dolmuş kilit (beklenmedik kapanış) yeniden alınabilir: eskisini pasife al.
                Exec(connection, "UPDATE ApplicationWriteLock SET IsActive = 0 WHERE Id = $id;", ("$id", existing.Id));
            }

            InsertMyLock(connection, now);
            Exec(connection, "COMMIT;");
            return new WriteLockResult(true, new WriteLockStatus(WriteLockState.HeldByCurrentUser, MyInfo(now)));
        }
        catch (SqliteException)
        {
            // Paralel bir istemci önce aldı (UNIQUE ihlali) → geri al ve güncel durumu döndür.
            SafeRollback(connection);
            return new WriteLockResult(false, GetStatus());
        }
    }

    public void Heartbeat()
    {
        using var connection = _factory.Open();
        var expires = DateTime.UtcNow + _ttl;
        Exec(connection,
            "UPDATE ApplicationWriteLock SET LastHeartbeatAt = $hb, ExpiresAt = $exp " +
            "WHERE LockKey = $k AND OwnerToken = $owner AND IsActive = 1;",
            ("$hb", Iso(DateTime.UtcNow)), ("$exp", Iso(expires)), ("$k", _lockKey), ("$owner", _ownerToken));
    }

    public void Release()
    {
        using var connection = _factory.Open();
        Exec(connection,
            "UPDATE ApplicationWriteLock SET IsActive = 0 " +
            "WHERE LockKey = $k AND OwnerToken = $owner AND IsActive = 1;",
            ("$k", _lockKey), ("$owner", _ownerToken));
    }

    public WriteLockStatus GetStatus()
    {
        using var connection = _factory.Open();
        var existing = ReadActiveLock(connection);
        if (existing is null)
            return WriteLockStatus.Free;

        var now = DateTime.UtcNow;
        if (existing.ExpiresAt <= now)
            return WriteLockStatus.Free; // süresi dolmuş kilit boş sayılır

        return existing.OwnerToken == _ownerToken
            ? new WriteLockStatus(WriteLockState.HeldByCurrentUser, ToInfo(existing))
            : new WriteLockStatus(WriteLockState.HeldByOther, ToInfo(existing));
    }

    // ---- İç işleyiş ----

    private void RefreshMyLock(SqliteConnection c, DateTime now) =>
        Exec(c,
            "UPDATE ApplicationWriteLock SET LastHeartbeatAt = $hb, ExpiresAt = $exp " +
            "WHERE LockKey = $k AND OwnerToken = $owner AND IsActive = 1;",
            ("$hb", Iso(now)), ("$exp", Iso(now + _ttl)), ("$k", _lockKey), ("$owner", _ownerToken));

    private void InsertMyLock(SqliteConnection c, DateTime now) =>
        Exec(c,
            "INSERT INTO ApplicationWriteLock (LockKey, UserName, MachineName, ProcessId, OwnerToken, AcquiredAt, LastHeartbeatAt, ExpiresAt, IsActive) " +
            "VALUES ($k, $user, $machine, $pid, $owner, $acq, $hb, $exp, 1);",
            ("$k", _lockKey), ("$user", _userName), ("$machine", _machineName), ("$pid", _processId),
            ("$owner", _ownerToken), ("$acq", Iso(now)), ("$hb", Iso(now)), ("$exp", Iso(now + _ttl)));

    private LockRow? ReadActiveLock(SqliteConnection c)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText =
            "SELECT Id, UserName, MachineName, ProcessId, OwnerToken, AcquiredAt, LastHeartbeatAt, ExpiresAt " +
            "FROM ApplicationWriteLock WHERE LockKey = $k AND IsActive = 1 LIMIT 1;";
        cmd.Parameters.AddWithValue("$k", _lockKey);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return null;

        return new LockRow(
            reader.GetInt64(0),
            reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
            reader.GetString(4),
            ParseIso(reader.GetString(5)),
            ParseIso(reader.GetString(6)),
            ParseIso(reader.GetString(7)));
    }

    private WriteLockInfo MyInfo(DateTime now) =>
        new(_userName, _machineName, _processId, now, now, now + _ttl);

    private static WriteLockInfo ToInfo(LockRow row, DateTime? acquiredOverride = null) =>
        new(row.UserName, row.MachineName, row.ProcessId,
            acquiredOverride ?? row.AcquiredAt, row.LastHeartbeatAt, row.ExpiresAt);

    private static void Exec(SqliteConnection c, string sql, params (string Name, object? Value)[] parameters)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    private static void SafeRollback(SqliteConnection c)
    {
        try { Exec(c, "ROLLBACK;"); } catch (SqliteException) { /* zaten kapanmış olabilir */ }
    }

    private static string Iso(DateTime value) => value.ToUniversalTime().ToString("o", Inv);

    private static DateTime ParseIso(string value) =>
        DateTime.Parse(value, Inv, DateTimeStyles.RoundtripKind).ToUniversalTime();

    private sealed record LockRow(
        long Id, string UserName, string MachineName, int ProcessId, string OwnerToken,
        DateTime AcquiredAt, DateTime LastHeartbeatAt, DateTime ExpiresAt);
}
