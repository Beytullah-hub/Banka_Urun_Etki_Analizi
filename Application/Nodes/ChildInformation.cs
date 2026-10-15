using BankProductImpact.Domain.Common;

namespace BankProductImpact.Application.Nodes;

/// <summary>
/// Bir öğenin, seçili başlık bağlamındaki bilgisi (UI'ya döndürülen model). Bilgi her
/// zaman bağlantı bağlamında saklanır; alanların boş olması hata değildir.
/// </summary>
public sealed record ChildInformation(
    NodeId NodeId,
    NodeRelationId? NodeRelationId,
    NodeId? ParentNodeId,
    string NodeName,
    string? ScreenName,
    string? BatchName,
    string? ShortDescription,
    DateTime? UpdatedAt,
    string? UpdatedBy)
{
    public bool HasContent =>
        !string.IsNullOrWhiteSpace(ScreenName) ||
        !string.IsNullOrWhiteSpace(BatchName) ||
        !string.IsNullOrWhiteSpace(ShortDescription);
}

/// <summary>Düzenleme formundan kaydetmeye gelen ham girdi (henüz normalize/doğrulanmamış).</summary>
public sealed record ChildInformationInput(
    NodeId NodeId,
    NodeRelationId? NodeRelationId,
    NodeId? ParentNodeId,
    string? ScreenName,
    string? BatchName,
    string? ShortDescription);

/// <summary>Kaydetme sonucu: başarı/başarısızlık, kullanıcıya gösterilecek mesaj ve güncel model.</summary>
public sealed record ChildInformationSaveResult(bool Success, string? Message, ChildInformation? Reloaded)
{
    public static ChildInformationSaveResult Ok(ChildInformation reloaded) => new(true, null, reloaded);
    public static ChildInformationSaveResult Fail(string message) => new(false, message, null);
}
