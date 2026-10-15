using System.Text.Json;
using BankProductImpact.Application.Abstractions;
using BankProductImpact.Domain.Common;
using BankProductImpact.Domain.Nodes;

namespace BankProductImpact.Application.Nodes;

/// <summary>
/// Bir öğenin (seçili başlık bağlamındaki) bilgisini yükleyen ve kaydeden uygulama
/// servisi. Sorumlulukları: bilgiyi <see cref="ChildInformation"/> olarak döndürmek,
/// girdileri doğrulamak/normalize etmek, tek yazıcı kilidini doğrulamak, transaction
/// içinde kaydettirmek ve audit oluşturmak. SQLite'a doğrudan bağımlılık YOKTUR.
/// </summary>
public sealed class ChildInformationService : IChildInformationService
{
    private readonly INodeRepository _nodes;
    private readonly INodeDetailsRepository _details;
    private readonly IWriteLockService _writeLock;
    private readonly IAppLogger _logger;
    private readonly string _actor;

    public ChildInformationService(
        INodeRepository nodes,
        INodeDetailsRepository details,
        IWriteLockService writeLock,
        IAppLogger logger,
        string? actor = null)
    {
        _nodes = nodes;
        _details = details;
        _writeLock = writeLock;
        _logger = logger;
        _actor = string.IsNullOrWhiteSpace(actor) ? Environment.UserName : actor!;
    }

    /// <summary>
    /// Bir öğenin (seçili başlık bağlamında) bilgisini döndürür. Aynı öğe farklı başlıklar
    /// altında farklı bilgiye sahip olabildiğinden kayıt (başlık, öğe) ikilisiyle bulunur.
    /// Kayıt yoksa alanlar boş döner; bu hata değildir.
    /// </summary>
    public ChildInformation Load(NodeId childId, NodeId? parentId)
    {
        var node = _nodes.GetNode(childId);
        var nodeName = node?.Name ?? childId.Value;

        NodeRelationId? relationId = null;
        if (parentId is { } pid)
        {
            var relation = _nodes.GetChildRelations(pid).FirstOrDefault(r => r.ChildNodeId.Equals(childId));
            relationId = relation?.Id;
        }

        var details = relationId is { } rid ? _details.GetNodeRelationDetails(rid) : null;

        return new ChildInformation(
            childId, relationId, parentId, nodeName,
            details?.ScreenName, details?.BatchName, details?.ShortDescription,
            details?.LastUpdatedAt, details?.UpdatedBy);
    }

    /// <summary>
    /// Bilgiyi kaydeder. Önce yazma kilidi doğrulanır (kilit başka kullanıcıdaysa hiçbir şey
    /// yazılmaz), sonra girdiler doğrulanır/normalize edilir, ardından tek transaction içinde
    /// upsert + audit yapılır. Hata durumunda kullanıcı girdisi kaybedilmez.
    /// </summary>
    public ChildInformationSaveResult Save(ChildInformationInput input)
    {
        // Bilgi her zaman bir bağlantı bağlamında saklanır; bağlantı yoksa kaydedilemez.
        if (input.NodeRelationId is not { } relationId)
            return ChildInformationSaveResult.Fail("Bilgi kaydetmek için önce öğe bir başlığa bağlı olmalıdır.");

        // 1) Yazma kilidi: aynı kullanıcı/süreç kilidi yeniden kullanır (idempotent);
        //    başka kullanıcıdaysa alınamaz ve kaydetme yapılmaz.
        var lockResult = _writeLock.TryAcquire();
        if (!lockResult.Acquired)
        {
            var holder = lockResult.Status.Holder;
            var who = holder is null ? string.Empty : $" ({holder.UserName} — {holder.MachineName})";
            return ChildInformationSaveResult.Fail($"Düzenleme kilidi başka bir kullanıcıda; kaydedilemedi. Salt okunur moddasınız.{who}");
        }

        // 2) Doğrulama + normalize (trim, boş→null, kontrol karakteri temizliği, uzunluk sınırı).
        var (ok, message, norm) = ValidateAndNormalize(input);
        if (!ok)
            return ChildInformationSaveResult.Fail(message!);

        // 3) Kaydet (tek transaction + audit repository katmanında).
        var now = DateTime.UtcNow;
        var details = new NodeRelationDetails(relationId, norm.ScreenName, norm.BatchName, norm.ShortDescription,
            CreatedAt: null, LastUpdatedAt: now, UpdatedBy: _actor);

        var oldInfo = Load(input.NodeId, input.ParentNodeId);
        var save = new ChildInformationSave(details, _actor, input.NodeId, input.ParentNodeId,
            OldValueJson: ToJson(oldInfo), NewValueJson: ToJson(norm));

        try
        {
            _details.SaveChildInformation(save);
            return ChildInformationSaveResult.Ok(Load(input.NodeId, input.ParentNodeId));
        }
        catch (Exception ex)
        {
            _logger.Error($"Bilgi kaydı başarısız: node={input.NodeId}", ex);
            TryLogFailure(input, ex);
            return ChildInformationSaveResult.Fail("Kayıt sırasında bir hata oluştu. Girdiğiniz bilgiler korundu; lütfen tekrar deneyin.");
        }
    }

    private void TryLogFailure(ChildInformationInput input, Exception ex)
    {
        try { _details.LogFailedChildInformationUpdate(input.NodeId, input.NodeRelationId, input.ParentNodeId, _actor, ex.Message); }
        catch (Exception logEx) { _logger.Warning($"Başarısız kayıt audit'i yazılamadı: {logEx.Message}"); }
    }

    // ---- Doğrulama / normalizasyon ----

    private static (bool Ok, string? Message, ChildInformationInput Normalized) ValidateAndNormalize(ChildInformationInput input)
    {
        var norm = input with
        {
            ScreenName = Normalize(input.ScreenName),
            BatchName = Normalize(input.BatchName),
            ShortDescription = Normalize(input.ShortDescription),
        };

        var error =
            TooLong("Ekran adı", norm.ScreenName, ChildInformationLimits.ScreenName) ??
            TooLong("Batch adı", norm.BatchName, ChildInformationLimits.BatchName) ??
            TooLong("Kısa açıklama", norm.ShortDescription, ChildInformationLimits.ShortDescription);

        return error is null ? (true, null, norm) : (false, error, norm);
    }

    /// <summary>Trim eder, kontrol karakterlerini (satır sonu/tab hariç) temizler, boşu null'a çevirir.</summary>
    private static string? Normalize(string? value)
    {
        if (value is null)
            return null;

        var cleaned = new string(value.Where(c => !char.IsControl(c) || c is '\n' or '\r' or '\t').ToArray()).Trim();
        return cleaned.Length == 0 ? null : cleaned;
    }

    private static string? TooLong(string label, string? value, int max) =>
        value is not null && value.Length > max ? $"{label} en fazla {max} karakter olabilir." : null;

    private static string ToJson(ChildInformation info) =>
        JsonSerializer.Serialize(new { info.ScreenName, info.BatchName, info.ShortDescription });

    private static string ToJson(ChildInformationInput input) =>
        JsonSerializer.Serialize(new { input.ScreenName, input.BatchName, input.ShortDescription });
}
