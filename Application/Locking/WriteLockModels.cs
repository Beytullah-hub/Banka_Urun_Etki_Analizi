namespace BankProductImpact.Application.Locking;

/// <summary>Düzenleme kilidinin bu istemci açısından durumu.</summary>
public enum WriteLockState
{
    /// <summary>Kilit boş; alınabilir.</summary>
    Free,
    /// <summary>Kilit bu istemcide (düzenleme modundayız).</summary>
    HeldByCurrentUser,
    /// <summary>Kilit başka bir kullanıcıda; salt okunur moddayız.</summary>
    HeldByOther
}

/// <summary>Kilidi tutan istemcinin bilgileri (kullanıcıya "kim düzenliyor" göstermek için).</summary>
public sealed record WriteLockInfo(
    string UserName,
    string MachineName,
    int ProcessId,
    DateTime AcquiredAt,
    DateTime LastHeartbeatAt,
    DateTime ExpiresAt);

/// <summary>Kilit durumu ve (varsa) kilidi tutan.</summary>
public sealed record WriteLockStatus(WriteLockState State, WriteLockInfo? Holder)
{
    public static readonly WriteLockStatus Free = new(WriteLockState.Free, null);

    public bool IsHeldByOther => State == WriteLockState.HeldByOther;
    public bool IsHeldByCurrentUser => State == WriteLockState.HeldByCurrentUser;
}

/// <summary>Kilit alma denemesinin sonucu.</summary>
public sealed record WriteLockResult(bool Acquired, WriteLockStatus Status);
