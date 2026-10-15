using System.Globalization;
using BankProductImpact.Application.Abstractions;

namespace BankProductImpact.Infrastructure.Logging;

/// <summary>
/// Basit dosya tabanlı logger (teknik doküman 7.3, 8.3). Loglar kontrollü bir
/// klasörde tutulur; müşteri adı, hesap numarası veya gizli anahtar yazılmaz.
/// Loglama başarısız olursa uygulama akışı bozulmaz (hata sessizce yutulmaz ama
/// yükseltilmez; loglama ikincil bir işlevdir).
/// </summary>
public sealed class FileAppLogger : IAppLogger
{
    private readonly string _logFilePath;
    private readonly object _gate = new();

    public FileAppLogger(string? logDirectory = null)
    {
        var directory = logDirectory ?? Path.Combine(AppContext.BaseDirectory, "Logs");
        Directory.CreateDirectory(directory);
        _logFilePath = Path.Combine(directory, $"app-{DateTime.Now:yyyyMMdd}.log");
    }

    public void Info(string message) => Write("INFO", message);
    public void Warning(string message) => Write("WARN", message);

    public void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message} | {exception.GetType().Name}: {exception.Message}");

    private void Write(string level, string message)
    {
        var line = string.Format(
            CultureInfo.InvariantCulture,
            "{0:yyyy-MM-dd HH:mm:ss} [{1}] {2}",
            DateTime.Now, level, message);

        try
        {
            lock (_gate)
            {
                File.AppendAllText(_logFilePath, line + Environment.NewLine);
            }
        }
        catch (IOException)
        {
            // Loglama diske yazamazsa ana işlem devam eder; log ikincil işlevdir.
        }
        System.Diagnostics.Debug.WriteLine(line);
    }
}
