using BankProductImpact.Domain.Common;

namespace BankProductImpact.Domain.Nodes;

/// <summary>
/// Ortak node kataloğundaki bir kayıt. Bir node bir ağacın merkez parent'ı olabilirken
/// başka bir ağaçta child olarak da bağlanabilir; bu nedenle parent ve child için ayrı
/// katalog tutulmaz (tek ortak <see cref="Node"/> modeli kullanılır).
///
/// Parent–child ağaç ilişkisi (<see cref="NodeRelation"/>) ile metrikler arası etki
/// ilişkisi (Domain.Relationships) birbirinden bağımsızdır.
/// </summary>
public sealed class Node
{
    public Node(
        NodeId id,
        string name,
        string? description = null,
        bool isActive = true,
        bool canBeRoot = true,
        int displayOrder = 0,
        DateTime? createdAt = null,
        DateTime? updatedAt = null,
        string? createdBy = null,
        string? updatedBy = null)
    {
        Id = id;
        Name = name;
        Description = description;
        IsActive = isActive;
        CanBeRoot = canBeRoot;
        DisplayOrder = displayOrder;
        CreatedAt = createdAt ?? DateTime.UtcNow;
        UpdatedAt = updatedAt ?? CreatedAt;
        CreatedBy = createdBy;
        UpdatedBy = updatedBy;
    }

    public NodeId Id { get; }
    public string Name { get; }
    public string? Description { get; }
    public bool IsActive { get; }

    /// <summary>Bu node bir ağacın kökü (merkez parent) olarak seçilebilir mi?</summary>
    public bool CanBeRoot { get; }

    public int DisplayOrder { get; }
    public DateTime CreatedAt { get; }
    public DateTime UpdatedAt { get; }
    public string? CreatedBy { get; }
    public string? UpdatedBy { get; }
}
