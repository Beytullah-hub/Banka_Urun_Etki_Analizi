namespace BankProductImpact.Presentation.ViewModels;

/// <summary>
/// Merkez parent kutusundan bir child'a giden dal çizgisi. Uç noktası child'ın merkezine
/// bağlıdır; child sürüklenince çizgi otomatik takip eder (Child.CenterX/CenterY bağlaması).
/// </summary>
public sealed class GenericBranchLineViewModel
{
    public GenericBranchLineViewModel(double centerX, double centerY, GenericChildNodeViewModel child)
    {
        X1 = centerX;
        Y1 = centerY;
        Child = child;
    }

    /// <summary>Merkez parent kutusunun konumu (sabit).</summary>
    public double X1 { get; }
    public double Y1 { get; }

    /// <summary>Dalın bağlı olduğu child (X2/Y2 bunun merkezine bağlanır).</summary>
    public GenericChildNodeViewModel Child { get; }
}
