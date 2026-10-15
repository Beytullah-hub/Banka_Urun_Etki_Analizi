using BankProductImpact.Domain.Common;
using BankProductImpact.Domain.Nodes;

namespace BankProductImpact.Application.Abstractions;

/// <summary>
/// Bağlantıya özel bilgi kayıtlarının veri erişim sözleşmesi. INodeRepository'den ayrı
/// tutulur (sorumluluklar net kalsın). UI doğrudan SQLite bilmez; yalnızca bu arayüzü tanır.
/// </summary>
public interface INodeDetailsRepository
{
    /// <summary>Bir bağlantının bilgi kaydı (yoksa null).</summary>
    NodeRelationDetails? GetNodeRelationDetails(NodeRelationId relationId);

    /// <summary>Bilgi kaydını TEK transaction içinde atomik kaydeder (upsert) ve audit yazar.</summary>
    void SaveChildInformation(ChildInformationSave save);

    /// <summary>Başarısız bir güncelleme denemesini audit'e (IsSuccessful=0) yazar.</summary>
    void LogFailedChildInformationUpdate(NodeId nodeId, NodeRelationId? relationId, NodeId? parentNodeId, string actor, string errorMessage);
}

/// <summary>
/// Bilgi kaydetme girdisi. Bilgi yalnızca bir bağlantı seçiliyse yazılır. Audit için
/// eski/yeni JSON ve bağlam kimlikleri taşınır.
/// </summary>
public sealed record ChildInformationSave(
    NodeRelationDetails RelationDetails,
    string Actor,
    NodeId NodeId,
    NodeId? ParentNodeId,
    string? OldValueJson,
    string? NewValueJson);
