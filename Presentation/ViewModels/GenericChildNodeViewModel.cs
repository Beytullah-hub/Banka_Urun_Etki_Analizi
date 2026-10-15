using System.Windows.Media;
using BankProductImpact.Domain.Common;
using BankProductImpact.Domain.Nodes;
using BankProductImpact.Presentation.Mvvm;

namespace BankProductImpact.Presentation.ViewModels;

/// <summary>
/// Generik parent ağacındaki konumlu bir child kartı (Repo dışı parent'lar için).
/// Konum sürüklenerek değiştirilebilir ve kaydedilir (teknik doküman 7).
/// </summary>
public sealed class GenericChildNodeViewModel : ObservableObject
{
    // Kart ölçüleri (dal çizgisi uçlarını kartın merkezine hizalamak için).
    public const double CardWidth = 264;
    public const double CardHeight = 96;

    private double _left;
    private double _top;

    public GenericChildNodeViewModel(Node node, double left, double top)
    {
        NodeId = node.Id;
        Name = node.Name;
        _left = left;
        _top = top;
        AccentBrush = NodeAccentPalette.ForNode(node.Id);
    }

    public NodeId NodeId { get; }
    public string Name { get; }

    /// <summary>Kimliğe göre otomatik atanan kategori rengi (dal şeridi ve çizgisi).</summary>
    public Brush AccentBrush { get; }

    public double Left
    {
        get => _left;
        set { if (SetProperty(ref _left, value)) OnPropertyChanged(nameof(CenterX)); }
    }

    public double Top
    {
        get => _top;
        set { if (SetProperty(ref _top, value)) OnPropertyChanged(nameof(CenterY)); }
    }

    /// <summary>Kartın merkez X'i (dal çizgisi buraya bağlanır; sürüklenince güncellenir).</summary>
    public double CenterX => _left + CardWidth / 2;
    public double CenterY => _top + CardHeight / 2;
}
