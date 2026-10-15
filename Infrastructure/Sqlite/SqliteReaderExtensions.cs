using System.Globalization;
using Microsoft.Data.Sqlite;

namespace BankProductImpact.Infrastructure.Sqlite;

/// <summary>
/// <see cref="SqliteDataReader"/> için sütun adına göre güvenli, tipli okuma yardımcıları.
/// Kültürden bağımsız (ISO/Invariant) ayrıştırma yapar.
/// </summary>
internal static class SqliteReaderExtensions
{
    public static string GetStringValue(this SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
    }

    public static string? GetStringOrNull(this SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    public static double GetDoubleValue(this SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.GetDouble(ordinal);
    }

    public static double? GetDoubleOrNull(this SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetDouble(ordinal);
    }

    public static int? GetIntOrNull(this SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    public static int GetIntValue(this SqliteDataReader reader, string column) =>
        reader.GetInt32(reader.GetOrdinal(column));

    /// <summary>SQLite'ta boolean INTEGER (0/1) olarak saklanır.</summary>
    public static bool GetBoolValue(this SqliteDataReader reader, string column) =>
        reader.GetInt64(reader.GetOrdinal(column)) != 0;

    public static DateOnly? GetDateOnlyOrNull(this SqliteDataReader reader, string column)
    {
        var text = reader.GetStringOrNull(column);
        return text is not null && DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;
    }

    public static DateTime GetDateTimeValue(this SqliteDataReader reader, string column)
    {
        var text = reader.GetStringOrNull(column);
        return text is not null && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)
            ? dt
            : DateTime.MinValue;
    }

    public static TEnum GetEnum<TEnum>(this SqliteDataReader reader, string column) where TEnum : struct, Enum
    {
        var text = reader.GetStringValue(column);
        return Enum.TryParse<TEnum>(text, ignoreCase: true, out var value)
            ? value
            : throw new InvalidDataException($"Geçersiz '{typeof(TEnum).Name}' değeri: '{text}' (sütun {column}).");
    }
}
