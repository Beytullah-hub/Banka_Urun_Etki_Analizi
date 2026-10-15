namespace BankProductImpact.Application.Nodes;

/// <summary>
/// Bilgi metin alanlarının karakter sınırları. Magic number olarak ViewModel'e
/// dağılmaması için tek yerde tutulur; hem doğrulama hem UI kullanır.
/// </summary>
public static class ChildInformationLimits
{
    public const int ScreenName = 250;
    public const int BatchName = 250;
    public const int ShortDescription = 1000;
}
