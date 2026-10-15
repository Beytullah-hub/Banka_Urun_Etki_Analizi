using BankProductImpact.Application.Abstractions;
using BankProductImpact.Domain.Common;
using BankProductImpact.Domain.Nodes;

namespace BankProductImpact.Infrastructure.Nodes;

/// <summary>
/// SQLite dışı veri kaynaklarında (Mock/Excel) node detaylarını bellekte tutan uygulama.
/// Aynı sözleşmeyi uygular; kalıcı depolama yoktur (tek süreç).
/// </summary>
public sealed class InMemoryNodeDetailsRepository : INodeDetailsRepository
{
    private readonly Dictionary<string, NodeRelationDetails> _context = new();

    public NodeRelationDetails? GetNodeRelationDetails(NodeRelationId relationId) =>
        _context.TryGetValue(relationId.Value, out var d) ? d : null;

    public void SaveChildInformation(ChildInformationSave save)
    {
        var now = DateTime.UtcNow;
        var relation = save.RelationDetails;
        var existing = GetNodeRelationDetails(relation.NodeRelationId);
        _context[relation.NodeRelationId.Value] = relation with
        {
            CreatedAt = existing?.CreatedAt ?? relation.CreatedAt ?? now,
            LastUpdatedAt = relation.LastUpdatedAt ?? now
        };
    }

    public void LogFailedChildInformationUpdate(NodeId nodeId, NodeRelationId? relationId, NodeId? parentNodeId, string actor, string errorMessage)
    {
        // Bellek içi uygulamada kalıcı audit yok; no-op.
    }
}
