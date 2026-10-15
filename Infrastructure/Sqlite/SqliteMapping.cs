using System.Globalization;
using BankProductImpact.Domain.Common;
using BankProductImpact.Domain.Nodes;
using Microsoft.Data.Sqlite;

namespace BankProductImpact.Infrastructure.Sqlite;

/// <summary>
/// SQLite satırları ile node/ağaç domain nesneleri arasındaki eşleme. Tarih değerleri
/// kültürden bağımsız (ISO/Invariant) işlenir.
/// </summary>
internal static class SqliteMapping
{
    public static Node ReadNode(SqliteDataReader r) => new(
        new NodeId(r.GetStringValue("NodeId")),
        r.GetStringValue("Name"),
        r.GetStringOrNull("Description"),
        r.GetBoolValue("IsActive"),
        r.GetBoolValue("CanBeRoot"),
        r.GetIntValue("DisplayOrder"),
        r.GetDateTimeValue("CreatedAt"),
        r.GetDateTimeValue("UpdatedAt"),
        r.GetStringOrNull("CreatedBy"),
        r.GetStringOrNull("UpdatedBy"));

    public static NodeRelationDetails ReadNodeRelationDetails(SqliteDataReader r) => new(
        new NodeRelationId(r.GetStringValue("NodeRelationId")),
        r.GetStringOrNull("ScreenName"),
        r.GetStringOrNull("BatchName"),
        r.GetStringOrNull("ShortDescription"),
        ParseNullableIso(r.GetStringOrNull("CreatedAt")),
        ParseNullableIso(r.GetStringOrNull("LastUpdatedAt")),
        r.GetStringOrNull("UpdatedBy"));

    private static DateTime? ParseNullableIso(string? value) =>
        value is null ? null : DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public static NodeRelation ReadNodeRelation(SqliteDataReader r) => new(
        new NodeRelationId(r.GetStringValue("NodeRelationId")),
        new NodeId(r.GetStringValue("ParentNodeId")),
        new NodeId(r.GetStringValue("ChildNodeId")),
        r.GetIntValue("DisplayOrder"),
        r.GetDoubleOrNull("PositionX"),
        r.GetDoubleOrNull("PositionY"),
        r.GetBoolValue("IsActive"),
        r.GetDateTimeValue("CreatedAt"),
        r.GetDateTimeValue("UpdatedAt"));
}
