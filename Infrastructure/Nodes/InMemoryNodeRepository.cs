using BankProductImpact.Application.Abstractions;
using BankProductImpact.Domain.Common;
using BankProductImpact.Domain.Nodes;

namespace BankProductImpact.Infrastructure.Nodes;

/// <summary>
/// Bellek içi node kataloğu (SQLite dışı veri kaynaklarında — Mock/Excel — parent seçiminin
/// çalışması için). Katalog <see cref="NodeCatalogSeed"/>'ten yüklenir; parent–child
/// bağlantısı başlangıçta yoktur (havuz manuel bağlanır).
/// </summary>
public sealed class InMemoryNodeRepository : INodeRepository
{
    private readonly List<Node> _nodes = NodeCatalogSeed.BuildNodes().ToList();
    private readonly List<NodeRelation> _relations = new();

    public IReadOnlyList<Node> GetAllNodes() =>
        _nodes.OrderBy(n => n.DisplayOrder).ThenBy(n => n.Name).ToList();

    public IReadOnlyList<Node> GetRootNodes() =>
        _nodes.Where(n => n.IsActive && n.CanBeRoot).OrderBy(n => n.DisplayOrder).ThenBy(n => n.Name).ToList();

    public Node? GetNode(NodeId id) => _nodes.FirstOrDefault(n => n.Id.Equals(id));

    public IReadOnlyList<NodeRelation> GetChildRelations(NodeId parentId) =>
        _relations.Where(r => r.ParentNodeId.Equals(parentId) && r.IsActive).OrderBy(r => r.DisplayOrder).ToList();

    public IReadOnlyList<Node> GetChildren(NodeId parentId)
    {
        var childIds = GetChildRelations(parentId).Select(r => r.ChildNodeId).ToHashSet();
        return _nodes.Where(n => childIds.Contains(n.Id) && n.IsActive).ToList();
    }

    public bool NameExists(string name)
    {
        var normalized = name.Trim().ToLowerInvariant();
        return _nodes.Any(n => n.Name.Trim().ToLowerInvariant() == normalized);
    }

    public Node AddNode(NodeDraft draft, string actor)
    {
        var node = new Node(new NodeId("N-" + Guid.NewGuid().ToString("N")), draft.Name.Trim(),
            draft.Description, draft.IsActive, draft.CanBeRoot,
            displayOrder: _nodes.Count, createdBy: actor, updatedBy: actor);
        _nodes.Add(node);
        return node;
    }

    public void DeleteNode(NodeId nodeId, string actor)
    {
        _relations.RemoveAll(r => r.ParentNodeId.Equals(nodeId) || r.ChildNodeId.Equals(nodeId));
        _nodes.RemoveAll(n => n.Id.Equals(nodeId));
    }

    public NodeRelation AddOrActivateChild(NodeId parentId, NodeId childId, double? positionX, double? positionY, string actor)
    {
        if (parentId.Equals(childId))
            throw new ArgumentException("Bir node kendisine child olarak eklenemez.", nameof(childId));

        var existing = _relations.FirstOrDefault(r => r.ParentNodeId.Equals(parentId) && r.ChildNodeId.Equals(childId));
        if (existing is not null)
        {
            _relations.Remove(existing);
            var reactivated = new NodeRelation(existing.Id, parentId, childId, existing.DisplayOrder, positionX, positionY, isActive: true);
            _relations.Add(reactivated);
            return reactivated;
        }

        var relation = new NodeRelation(new NodeRelationId("NR-" + Guid.NewGuid().ToString("N")),
            parentId, childId, _relations.Count, positionX, positionY, isActive: true);
        _relations.Add(relation);
        return relation;
    }

    public void RemoveChild(NodeId parentId, NodeId childId, string actor)
    {
        var existing = _relations.FirstOrDefault(r => r.ParentNodeId.Equals(parentId) && r.ChildNodeId.Equals(childId) && r.IsActive);
        if (existing is null)
            return;
        _relations.Remove(existing);
        _relations.Add(new NodeRelation(existing.Id, parentId, childId, existing.DisplayOrder, existing.PositionX, existing.PositionY, isActive: false));
    }

    public void UpdateChildPosition(NodeId parentId, NodeId childId, double positionX, double positionY, string actor)
    {
        var existing = _relations.FirstOrDefault(r => r.ParentNodeId.Equals(parentId) && r.ChildNodeId.Equals(childId) && r.IsActive);
        if (existing is null)
            return;
        _relations.Remove(existing);
        _relations.Add(new NodeRelation(existing.Id, parentId, childId, existing.DisplayOrder, positionX, positionY, isActive: true));
    }
}
