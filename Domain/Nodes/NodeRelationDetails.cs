using BankProductImpact.Domain.Common;

namespace BankProductImpact.Domain.Nodes;

/// <summary>
/// Bir bağlantıya (NodeRelation) özel bilgi kaydı. Aynı öğe farklı başlıklar altında
/// farklı ekran adı, batch adı ve kısa açıklamaya sahip olabilir; bilgi her zaman
/// bağlantı bağlamında saklanır (her bağlantı için en fazla bir kayıt).
///
/// Öğe ağaçtan çıkarılsa (bağlantı pasife alınsa) bile bu kayıt korunur; öğe aynı
/// başlığa tekrar bağlandığında önceki bilgiler yeniden görünür.
/// </summary>
public sealed record NodeRelationDetails(
    NodeRelationId NodeRelationId,
    string? ScreenName,
    string? BatchName,
    string? ShortDescription,
    DateTime? CreatedAt,
    DateTime? LastUpdatedAt,
    string? UpdatedBy)
{
    /// <summary>Herhangi bir bilgi alanı dolu mu?</summary>
    public bool HasContent =>
        !string.IsNullOrWhiteSpace(ScreenName) ||
        !string.IsNullOrWhiteSpace(BatchName) ||
        !string.IsNullOrWhiteSpace(ShortDescription);

    public static NodeRelationDetails Empty(NodeRelationId relationId) =>
        new(relationId, null, null, null, null, null, null);
}
