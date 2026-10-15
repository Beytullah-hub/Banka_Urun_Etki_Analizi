using BankProductImpact.Application.Nodes;
using BankProductImpact.Domain.Common;

namespace BankProductImpact.Application.Abstractions;

/// <summary>
/// Child detay bilgilerini yükleyen/kaydeden uygulama servisi sözleşmesi. UI bu arayüzü
/// kullanır; doğrudan repository/SQLite çağırmaz (teknik doküman 5, 19).
/// </summary>
public interface IChildInformationService
{
    /// <summary>Bir child'ın (seçili parent bağlamında) genel + bağlamsal birleşik bilgisi.</summary>
    ChildInformation Load(NodeId childId, NodeId? parentId);

    /// <summary>Detayları doğrulayıp kilit kontrolüyle kaydeder; sonucu (ve güncel modeli) döndürür.</summary>
    ChildInformationSaveResult Save(ChildInformationInput input);
}
