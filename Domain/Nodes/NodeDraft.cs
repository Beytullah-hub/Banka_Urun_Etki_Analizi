namespace BankProductImpact.Domain.Nodes;

/// <summary>
/// Yeni bir katalog node'u oluşturmak için girdi (teknik doküman 8). Ad zorunludur;
/// mükerrer ad kontrolü repository tarafından yapılır.
/// </summary>
public sealed record NodeDraft(
    string Name,
    string? Description,
    bool IsActive,
    bool CanBeRoot);
