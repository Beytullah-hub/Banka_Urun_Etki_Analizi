using System.Globalization;
using BankProductImpact.Application.Abstractions;
using BankProductImpact.Domain.Common;
using BankProductImpact.Domain.Nodes;
using Microsoft.Data.Sqlite;

namespace BankProductImpact.Infrastructure.Sqlite;

/// <summary>
/// <see cref="INodeRepository"/>'nin SQLite uygulaması. Ortak node kataloğu ve parent–child
/// ağaç bağlantılarını parametreli sorgularla okur. Bağlantılar kısa ömürlüdür.
/// </summary>
public sealed class SqliteNodeRepository : INodeRepository
{
    private readonly SqliteConnectionFactory _factory;

    public SqliteNodeRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public IReadOnlyList<Node> GetAllNodes() =>
        Query("SELECT * FROM Nodes ORDER BY DisplayOrder, Name;", SqliteMapping.ReadNode);

    public IReadOnlyList<Node> GetRootNodes() =>
        Query(
            "SELECT * FROM Nodes WHERE IsActive = 1 AND CanBeRoot = 1 ORDER BY DisplayOrder, Name;",
            SqliteMapping.ReadNode);

    public Node? GetNode(NodeId id) =>
        Query("SELECT * FROM Nodes WHERE NodeId = $id;", SqliteMapping.ReadNode, ("$id", id.Value))
            .FirstOrDefault();

    public IReadOnlyList<NodeRelation> GetChildRelations(NodeId parentId) =>
        Query(
            "SELECT * FROM NodeRelations WHERE ParentNodeId = $p AND IsActive = 1 ORDER BY DisplayOrder;",
            SqliteMapping.ReadNodeRelation,
            ("$p", parentId.Value));

    public IReadOnlyList<Node> GetChildren(NodeId parentId) =>
        Query(
            "SELECT n.* FROM Nodes n " +
            "JOIN NodeRelations r ON r.ChildNodeId = n.NodeId " +
            "WHERE r.ParentNodeId = $p AND r.IsActive = 1 AND n.IsActive = 1 " +
            "ORDER BY r.DisplayOrder, n.Name;",
            SqliteMapping.ReadNode,
            ("$p", parentId.Value));

    // ---- Yazma ----

    public bool NameExists(string name)
    {
        var normalized = Normalize(name);
        // Türkçe büyük/küçük harf farkları için karşılaştırma C# tarafında yapılır.
        return GetAllNodes().Any(n => Normalize(n.Name) == normalized);
    }

    public Node AddNode(NodeDraft draft, string actor)
    {
        var id = new NodeId("N-" + Guid.NewGuid().ToString("N"));
        var now = DateTime.UtcNow;
        var order = NextDisplayOrder();

        WithTransaction((c, tx) =>
        {
            NonQuery(c, tx,
                "INSERT INTO Nodes (NodeId, Name, Description, IsActive, CanBeRoot, DisplayOrder, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy) " +
                "VALUES ($id, $name, $desc, $active, $root, $order, $now, $now, $actor, $actor);",
                ("$id", id.Value), ("$name", draft.Name.Trim()), ("$desc", (object?)draft.Description),
                ("$active", draft.IsActive ? 1 : 0), ("$root", draft.CanBeRoot ? 1 : 0), ("$order", order),
                ("$now", Iso(now)), ("$actor", actor));

            WriteAudit(c, tx, "CreateNode", "Node", id.Value, parentNodeId: null, actor);
        });

        return new Node(id, draft.Name.Trim(), draft.Description, draft.IsActive, draft.CanBeRoot, order, now, now, actor, actor);
    }

    public void DeleteNode(NodeId nodeId, string actor)
    {
        WithTransaction((c, tx) =>
        {
            WriteAudit(c, tx, "DeleteNode", "Node", nodeId.Value, parentNodeId: null, actor);
            NonQuery(c, tx,
                "DELETE FROM NodeRelationDetails WHERE NodeRelationId IN " +
                "(SELECT NodeRelationId FROM NodeRelations WHERE ParentNodeId = $id OR ChildNodeId = $id);",
                ("$id", nodeId.Value));
            NonQuery(c, tx,
                "DELETE FROM NodeRelations WHERE ParentNodeId = $id OR ChildNodeId = $id;",
                ("$id", nodeId.Value));
            NonQuery(c, tx, "DELETE FROM Nodes WHERE NodeId = $id;", ("$id", nodeId.Value));
        });
    }

    public NodeRelation AddOrActivateChild(NodeId parentId, NodeId childId, double? positionX, double? positionY, string actor)
    {
        if (parentId.Equals(childId))
            throw new ArgumentException("Bir node kendisine child olarak eklenemez.", nameof(childId));

        var now = DateTime.UtcNow;
        var existing = FindRelation(parentId, childId);

        if (existing is { IsActive: true })
            return existing.ToRelation(); // zaten aktif → mükerrer eklenmez

        var relationId = existing?.Id ?? "NR-" + Guid.NewGuid().ToString("N");

        WithTransaction((c, tx) =>
        {
            if (existing is null)
            {
                NonQuery(c, tx,
                    "INSERT INTO NodeRelations (NodeRelationId, ParentNodeId, ChildNodeId, DisplayOrder, PositionX, PositionY, IsActive, CreatedAt, UpdatedAt) " +
                    "VALUES ($id, $p, $c, $order, $x, $y, 1, $now, $now);",
                    ("$id", relationId), ("$p", parentId.Value), ("$c", childId.Value),
                    ("$order", NextChildOrder(parentId)), ("$x", (object?)positionX), ("$y", (object?)positionY), ("$now", Iso(now)));
            }
            else
            {
                // Pasif bağlantıyı yeniden aktifleştir (ağaçtan çıkarılmış olabilir).
                NonQuery(c, tx,
                    "UPDATE NodeRelations SET IsActive = 1, PositionX = $x, PositionY = $y, UpdatedAt = $now WHERE NodeRelationId = $id;",
                    ("$x", (object?)positionX), ("$y", (object?)positionY), ("$now", Iso(now)), ("$id", relationId));
            }

            WriteAudit(c, tx, "AddChild", "NodeRelation", childId.Value, parentId.Value, actor);
        });

        return new NodeRelation(new NodeRelationId(relationId), parentId, childId, 0, positionX, positionY, true, now, now);
    }

    public void RemoveChild(NodeId parentId, NodeId childId, string actor)
    {
        WithTransaction((c, tx) =>
        {
            NonQuery(c, tx,
                "UPDATE NodeRelations SET IsActive = 0, UpdatedAt = $now WHERE ParentNodeId = $p AND ChildNodeId = $c AND IsActive = 1;",
                ("$now", Iso(DateTime.UtcNow)), ("$p", parentId.Value), ("$c", childId.Value));

            WriteAudit(c, tx, "RemoveChild", "NodeRelation", childId.Value, parentId.Value, actor);
        });
    }

    public void UpdateChildPosition(NodeId parentId, NodeId childId, double positionX, double positionY, string actor)
    {
        WithTransaction((c, tx) =>
        {
            NonQuery(c, tx,
                "UPDATE NodeRelations SET PositionX = $x, PositionY = $y, UpdatedAt = $now WHERE ParentNodeId = $p AND ChildNodeId = $c AND IsActive = 1;",
                ("$x", positionX), ("$y", positionY), ("$now", Iso(DateTime.UtcNow)), ("$p", parentId.Value), ("$c", childId.Value));

            WriteAudit(c, tx, "MoveChild", "NodeRelation", childId.Value, parentId.Value, actor);
        });
    }

    // ---- Yazma yardımcıları ----

    private static string Normalize(string name) => name.Trim().ToLowerInvariant();

    private int NextDisplayOrder() =>
        Query("SELECT COALESCE(MAX(DisplayOrder), -1) + 1 AS n FROM Nodes;", r => r.GetInt32(0)).FirstOrDefault();

    private int NextChildOrder(NodeId parentId) =>
        Query("SELECT COALESCE(MAX(DisplayOrder), -1) + 1 AS n FROM NodeRelations WHERE ParentNodeId = $p;",
            r => r.GetInt32(0), ("$p", parentId.Value)).FirstOrDefault();

    private RelationRow? FindRelation(NodeId parentId, NodeId childId) =>
        Query(
            "SELECT NodeRelationId, IsActive, PositionX, PositionY FROM NodeRelations WHERE ParentNodeId = $p AND ChildNodeId = $c;",
            r => new RelationRow(
                r.GetString(0), r.GetInt64(1) != 0,
                r.IsDBNull(2) ? null : r.GetDouble(2), r.IsDBNull(3) ? null : r.GetDouble(3),
                parentId, childId),
            ("$p", parentId.Value), ("$c", childId.Value))
        .FirstOrDefault();

    private void WithTransaction(Action<SqliteConnection, SqliteTransaction> work)
    {
        using var connection = _factory.Open();
        using var tx = connection.BeginTransaction();
        try
        {
            work(connection, tx);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static void WriteAudit(SqliteConnection c, SqliteTransaction tx, string action, string entityType, string entityId, string? parentNodeId, string actor) =>
        NonQuery(c, tx,
            "INSERT INTO AuditLog (Timestamp, UserName, MachineName, ActionType, EntityType, EntityId, ParentNodeId, IsSuccessful) " +
            "VALUES ($ts, $user, $machine, $action, $entity, $id, $parent, 1);",
            ("$ts", Iso(DateTime.Now)), ("$user", actor), ("$machine", Environment.MachineName),
            ("$action", action), ("$entity", entityType), ("$id", entityId), ("$parent", (object?)parentNodeId));

    private static void NonQuery(SqliteConnection c, SqliteTransaction tx, string sql, params (string Name, object? Value)[] parameters)
    {
        using var command = c.CreateCommand();
        command.CommandText = sql;
        command.Transaction = tx;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    private static string Iso(DateTime value) => value.ToString("o", CultureInfo.InvariantCulture);

    private sealed record RelationRow(string Id, bool IsActive, double? X, double? Y, NodeId Parent, NodeId Child)
    {
        public NodeRelation ToRelation() => new(new NodeRelationId(Id), Parent, Child, 0, X, Y, IsActive);
    }

    private IReadOnlyList<T> Query<T>(string sql, Func<SqliteDataReader, T> map, params (string Name, object? Value)[] parameters)
    {
        using var connection = _factory.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);

        var results = new List<T>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            results.Add(map(reader));

        return results;
    }
}
