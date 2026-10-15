namespace BankProductImpact.Application.Abstractions;

/// <summary>
/// Güvenli loglama sözleşmesi (teknik doküman 7.3, 8.3). Loglarda müşteri adı,
/// hesap numarası, kimlik veya hassas finansal detay bulunmaz; yalnızca işlem
/// türü, sonuç ve korelasyon bilgisi tutulur.
/// </summary>
public interface IAppLogger
{
    void Info(string message);
    void Warning(string message);
    void Error(string message, Exception? exception = null);
}
