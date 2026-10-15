using BankProductImpact.Domain.Common;
using BankProductImpact.Domain.Nodes;
using BankProductImpact.Presentation.Mvvm;

namespace BankProductImpact.Presentation.ViewModels;

/// <summary>
/// Child Havuzu'ndaki ortak katalog öğesi (teknik doküman 7). Seçim kutusu ve aktif
/// parent'a bağlı olup olmadığı bilgisini taşır.
/// </summary>
public sealed class ChildPoolItemViewModel : ObservableObject
{
    private bool _isSelected;

    public ChildPoolItemViewModel(Node node, bool isBound)
    {
        NodeId = node.Id;
        Name = node.Name;
        IsActive = node.IsActive;
        IsBound = isBound;
    }

    public NodeId NodeId { get; }
    public string Name { get; }
    public bool IsActive { get; }

    /// <summary>Bu node, aktif parent'a zaten bağlı mı? (Ağaçta gösterilir.)</summary>
    public bool IsBound { get; }

    /// <summary>"Seçilenleri Ekle" için işaretli mi?</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
