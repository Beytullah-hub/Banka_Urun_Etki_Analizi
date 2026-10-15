using System.Globalization;
using BankProductImpact.Application.Abstractions;
using BankProductImpact.Domain.Common;
using BankProductImpact.Domain.Nodes;
using Microsoft.Data.Sqlite;

namespace BankProductImpact.Infrastructure.Sqlite;

/// <summary>
/// <see cref="INodeDetailsRepository"/>'nin SQLite uygulaması. Bağlantıya özel bilgiyi
/// (NodeRelationDetails) parametreli sorgularla okur/yazar; kaydı ve audit'i tek
/// transaction içinde atomik yapar.
/// </summary>
public sealed class SqliteNodeDetailsRepository : INodeDetailsRepository
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly SqliteConnectionFactory _factory;

    public SqliteNodeDetailsRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public NodeRelationDetails? GetNodeRelationDetails(NodeRelationId relationId) =>
        QuerySingle("SELECT * FROM NodeRelationDetails WHERE NodeRelationId = $id;",
            SqliteMapping.ReadNodeRelationDetails, ("$id", relationId.Value));

    public void SaveChildInformation(ChildInformationSave save)
    {
        using var connection = _factory.Open();
        using var tx = connection.BeginTransaction();
        try
        {
            var now = DateTime.UtcNow;
            var relation = save.RelationDetails;

            var existed = Exists(connection, tx, "NodeRelationDetails", "NodeRelationId", relation.NodeRelationId.Value);
            UpsertRelationDetails(connection, tx, relation, now);
            WriteAudit(connection, tx,
                existed ? "NodeRelationDetailsUpdated" : "NodeRelationDetailsCreated",
                "NodeRelationDetails", relation.NodeRelationId.Value, save);

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public void LogFailedChildInformationUpdate(NodeId nodeId, NodeRelationId? relationId, NodeId? parentNodeId, string actor, string errorMessage)
    {
        using var connection = _factory.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO AuditLog (Timestamp, UserName, MachineName, ActionType, EntityType, EntityId, ParentNodeId, IsSuccessful, ErrorMessage) " +
            "VALUES ($ts, $user, $machine, 'ChildInformationUpdateFailed', 'NodeRelationDetails', $id, $parent, 0, $err);";
        foreach (var (name, value) in new (string, object?)[]
                 {
                     ("$ts", Iso(DateTime.Now)), ("$user", actor), ("$machine", Environment.MachineName),
                     ("$id", nodeId.Value), ("$parent", (object?)parentNodeId?.Value), ("$err", errorMessage)
                 })
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    // ---- Yazma ----

    private void UpsertRelationDetails(SqliteConnection c, SqliteTransaction tx, NodeRelationDetails d, DateTime now) =>
        NonQuery(c, tx,
            "INSERT INTO NodeRelationDetails (NodeRelationId, ScreenName, BatchName, ShortDescription, CreatedAt, LastUpdatedAt, UpdatedBy) " +
            "VALUES ($id, $screen, $batch, $desc, $created, $updated, $by) " +
            "ON CONFLICT(NodeRelationId) DO UPDATE SET " +
            "  ScreenName = $screen, BatchName = $batch, ShortDescription = $desc, " +
            "  LastUpdatedAt = $updated, UpdatedBy = $by;",   // CreatedAt korunur (yalnızca insert'te yazılır)
            ("$id", d.NodeRelationId.Value), ("$screen", (object?)d.ScreenName), ("$batch", (object?)d.BatchName),
            ("$desc", (object?)d.ShortDescription),
            ("$created", Iso(d.CreatedAt ?? now)), ("$updated", Iso(d.LastUpdatedAt ?? now)), ("$by", (object?)d.UpdatedBy));

    private static void WriteAudit(SqliteConnection c, SqliteTransaction tx, string action, string entityType, string entityId, ChildInformationSave save) =>
        NonQuery(c, tx,
            "INSERT INTO AuditLog (Timestamp, UserName, MachineName, ActionType, EntityType, EntityId, ParentNodeId, OldValueJson, NewValueJson, IsSuccessful) " +
            "VALUES ($ts, $user, $machine, $action, $entity, $id, $parent, $old, $new, 1);",
            ("$ts", Iso(DateTime.Now)), ("$user", save.Actor), ("$machine", Environment.MachineName),
            ("$action", action), ("$entity", entityType), ("$id", entityId),
            ("$parent", (object?)save.ParentNodeId?.Value), ("$old", (object?)save.OldValueJson), ("$new", (object?)save.NewValueJson));

    // ---- Yardımcılar ----

    private static bool Exists(SqliteConnection c, SqliteTransaction tx, string table, string keyColumn, string keyValue)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT 1 FROM {table} WHERE {keyColumn} = $k LIMIT 1;"; // table/kolon sabit
        cmd.Transaction = tx;
        cmd.Parameters.AddWithValue("$k", keyValue);
        return cmd.ExecuteScalar() is not null;
    }

    private T? QuerySingle<T>(string sql, Func<SqliteDataReader, T> map, params (string Name, object? Value)[] parameters) where T : class
    {
        using var connection = _factory.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);

        using var reader = command.ExecuteReader();
        return reader.Read() ? map(reader) : null;
    }

    private static void NonQuery(SqliteConnection c, SqliteTransaction tx, string sql, params (string Name, object? Value)[] parameters)
    {
        using var command = c.CreateCommand();
        command.CommandText = sql;
        command.Transaction = tx;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    private static string Iso(DateTime value) => value.ToString("o", Inv);
}
