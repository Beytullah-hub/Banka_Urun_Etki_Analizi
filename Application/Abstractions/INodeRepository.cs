using BankProductImpact.Domain.Common;
using BankProductImpact.Domain.Nodes;

namespace BankProductImpact.Application.Abstractions;

/// <summary>
/// Ortak node kataloğu ve parent–child ağaç bağlantıları için veri erişim sözleşmesi.
/// Etki verisinden (IProductDataRepository) ayrıdır; ağaç aitliği ile etki ilişkisi
/// birbirine karışmaz. UI doğrudan SQLite bilmez; yalnızca bu arayüzü tanır.
/// </summary>
public interface INodeRepository
{
    /// <summary>Katalogdaki tüm node'lar (DisplayOrder, ardından ada göre sıralı).</summary>
    IReadOnlyList<Node> GetAllNodes();

    /// <summary>Kök (parent) olarak seçilebilir aktif node'lar — parent seçim listesi.</summary>
    IReadOnlyList<Node> GetRootNodes();

    Node? GetNode(NodeId id);

    /// <summary>Bir parent'ın aktif child bağlantıları (konum/sıra bilgisiyle).</summary>
    IReadOnlyList<NodeRelation> GetChildRelations(NodeId parentId);

    /// <summary>Bir parent'a aktif olarak bağlı child node'lar.</summary>
    IReadOnlyList<Node> GetChildren(NodeId parentId);

    // ---- Yazma (yalnızca düzenleme modunda çağrılmalı; audit'e yazılır) ----

    /// <summary>Verilen ad (kırpma + büyük/küçük harf duyarsız) katalogda zaten var mı?</summary>
    bool NameExists(string name);

    /// <summary>Ortak kataloğa yeni bir node ekler ve eklenen node'u döndürür.</summary>
    Node AddNode(NodeDraft draft, string actor);

    /// <summary>Node'u bağlantıları ve bağlantı detaylarıyla katalogdan kalıcı olarak siler.</summary>
    void DeleteNode(NodeId nodeId, string actor);

    /// <summary>
    /// Bir child'ı parent'a bağlar. Bağlantı pasif olarak varsa yeniden aktifleştirilir;
    /// zaten aktifse tekrar eklenmez (mükerrer engellenir). Oluşan/etkilenen ilişkiyi döndürür.
    /// </summary>
    NodeRelation AddOrActivateChild(NodeId parentId, NodeId childId, double? positionX, double? positionY, string actor);

    /// <summary>Child'ı yalnızca bu parent'ın ağacından çıkarır (bağlantıyı pasife alır); node silinmez.</summary>
    void RemoveChild(NodeId parentId, NodeId childId, string actor);

    /// <summary>Bir child'ın bu parent ağacındaki konumunu günceller (sürükleme bitince tek kayıt).</summary>
    void UpdateChildPosition(NodeId parentId, NodeId childId, double positionX, double positionY, string actor);
}
