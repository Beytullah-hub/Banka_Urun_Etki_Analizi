using BankProductImpact.Domain.Common;

namespace BankProductImpact.Domain.Nodes;

/// <summary>
/// Bir parent node ile bir child node arasındaki ağaç (aitlik) bağlantısı. Bu bağlantı
/// yalnızca "child, bu parent'ın ağacında gösterilir" anlamına gelir; etki ilişkisi
/// DEĞİLDİR (etki ilişkileri ayrı tutulur).
///
/// Kurallar: parent ile child aynı olamaz; aynı parent–child ikilisi mükerrer eklenemez
/// (veri katmanında UNIQUE ile korunur). Ağaçtan çıkarma node'u silmez, yalnızca bu
/// bağlantıyı pasife alır/kaldırır.
/// </summary>
public sealed class NodeRelation
{
    public NodeRelation(
        NodeRelationId id,
        NodeId parentNodeId,
        NodeId childNodeId,
        int displayOrder = 0,
        double? positionX = null,
        double? positionY = null,
        bool isActive = true,
        DateTime? createdAt = null,
        DateTime? updatedAt = null)
    {
        if (parentNodeId.Equals(childNodeId))
            throw new ArgumentException("Bir node kendisine child olarak eklenemez (parent = child).", nameof(childNodeId));

        Id = id;
        ParentNodeId = parentNodeId;
        ChildNodeId = childNodeId;
        DisplayOrder = displayOrder;
        PositionX = positionX;
        PositionY = positionY;
        IsActive = isActive;
        CreatedAt = createdAt ?? DateTime.UtcNow;
        UpdatedAt = updatedAt ?? CreatedAt;
    }

    public NodeRelationId Id { get; }
    public NodeId ParentNodeId { get; }
    public NodeId ChildNodeId { get; }
    public int DisplayOrder { get; }

    /// <summary>Child'ın grafikteki konumu (kullanıcı sürükleyince güncellenir).</summary>
    public double? PositionX { get; }
    public double? PositionY { get; }

    public bool IsActive { get; }
    public DateTime CreatedAt { get; }
    public DateTime UpdatedAt { get; }
}
