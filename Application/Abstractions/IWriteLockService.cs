using BankProductImpact.Application.Locking;

namespace BankProductImpact.Application.Abstractions;

/// <summary>
/// Uygulama seviyesi tek yazıcı (düzenleme) kilidi (teknik doküman 3). Aynı anda birden
/// fazla kullanıcı okuyabilir; ancak yalnızca bir kullanıcı düzenleme yapabilir.
/// Bu bir YETKİLENDİRME (rol) sistemi DEĞİLDİR; yalnızca eşzamanlı yazma çakışmasını önler.
///
/// Salt SQLite'ın dosya kilidine güvenilmez; anlaşılır, uygulama düzeyinde bir kilit tutulur.
/// </summary>
public interface IWriteLockService
{
    /// <summary>Kilidi atomik olarak almayı dener. Kilit boşsa veya süresi dolmuşsa alınır.</summary>
    WriteLockResult TryAcquire();

    /// <summary>Düzenleme sürerken kilit ömrünü uzatır (yalnızca kilit bizdeyse etkilidir).</summary>
    void Heartbeat();

    /// <summary>Kilidi bırakır (yalnızca bizim tuttuğumuz kilit bırakılır).</summary>
    void Release();

    /// <summary>Kilidin güncel durumunu, süresi dolmuş kilitleri de dikkate alarak döndürür.</summary>
    WriteLockStatus GetStatus();
}
