using BankProductImpact.Domain.Common;
using BankProductImpact.Domain.Nodes;

namespace BankProductImpact.Presentation.ViewModels;

/// <summary>
/// Sol paneldeki parent seçim listesinin bir öğesi (teknik doküman 6). Yalnızca
/// kök olarak seçilebilir (CanBeRoot) node'lar burada listelenir.
/// </summary>
public sealed class ParentItemViewModel
{
    public ParentItemViewModel(Node node)
    {
        NodeId = node.Id;
        Name = node.Name;
        IsRepo = node.Id.Equals(NodeCatalogRepoId);
    }

    /// <summary>Repo node kimliği ("repo") — mevcut etki kokpitini tetikler.</summary>
    public static NodeId NodeCatalogRepoId { get; } = new("repo");

    public NodeId NodeId { get; }
    public string Name { get; }

    /// <summary>Bu parent, mevcut Repo ürün ağacı mı? (Etki kokpiti bu durumda gösterilir.)</summary>
    public bool IsRepo { get; }

    public override string ToString() => Name;
}
