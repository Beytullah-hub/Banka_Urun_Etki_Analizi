using BankProductImpact.Application.Nodes;
using BankProductImpact.Domain.Common;

namespace BankProductImpact.Presentation.ViewModels;

/// <summary>
/// Sağ bilgi panelinin görüntüleme modeli. Seçili öğenin (bağlı olduğu başlık
/// bağlamındaki) ekran adı, batch adı ve kısa açıklamasını sunar; bilgi yoksa anlamlı
/// bir boş durum mesajı gösterir.
/// </summary>
public sealed class ChildInfoViewModel
{
    public const string EmptyMessage = "Bu bağlantı için henüz bilgi girilmemiştir.";

    public ChildInfoViewModel(ChildInformation info)
    {
        Model = info;
        NodeId = info.NodeId;
        NodeName = info.NodeName;

        ScreenName = info.ScreenName;
        BatchName = info.BatchName;
        ShortDescription = info.ShortDescription;
        HasContent = info.HasContent;

        LastUpdate = info.UpdatedAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
        UpdatedBy = info.UpdatedBy;
    }

    /// <summary>Düzenleme formunu mevcut değerlerle açmak için altta yatan model.</summary>
    public ChildInformation Model { get; }

    public NodeId NodeId { get; }
    public string NodeName { get; }

    public string? ScreenName { get; }
    public string? BatchName { get; }
    public string? ShortDescription { get; }
    public bool HasContent { get; }
    public string EmptyStateMessage => EmptyMessage;

    // --- Üst bilgi ---
    public string? LastUpdate { get; }
    public string? UpdatedBy { get; }
    public bool HasMeta => LastUpdate is not null;
}
