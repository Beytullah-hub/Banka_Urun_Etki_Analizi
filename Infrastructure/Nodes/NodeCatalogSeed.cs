using BankProductImpact.Domain.Common;
using BankProductImpact.Domain.Nodes;

namespace BankProductImpact.Infrastructure.Nodes;

/// <summary>
/// Ortak node kataloğunun varsayılan verisi (teknik doküman 11). Parent ve child
/// isimleri KULLANICININ VERDİĞİ HÂLİYLE korunur: düzeltme, sadeleştirme, birleştirme
/// veya yeniden adlandırma yapılmaz.
///
/// İlk açılışta bu katalog Repo'ya OTOMATİK bağlanmaz; node'lar havuzda hazır bekler,
/// kullanıcı manuel bağlar (FAZ 4). Repo, hem Product (etki dünyası) hem de bir root
/// node olarak kataloğa "repo" kimliğiyle eklenir.
/// </summary>
public static class NodeCatalogSeed
{
    /// <summary>Repo node kimliği — mevcut ProductId ("repo") ile bilinçli olarak eşleşir.</summary>
    public static readonly NodeId RepoNodeId = new("repo");

    // Parent listesi (teknik doküman 11). Repo bu listededir ve "repo" kimliğini alır.
    private static readonly string[] ParentNames =
    {
        "Yatırım İşlemleri",
        "SGMK İşlemleri",
        "Tefas ve Bankamız Fon İşlemleri",
        "BİST ve Müşteri Repo İşlemleri",
        "Yatırım Hesap İşlemleri Yönetimi",
        "Hazine İhale İşlemleri",
        "Halk Arz İşlemleri",
        "TÜRİB Elüs İşlemleri",
        "Banka/Müşteri Stok Yönetimi",
        "Saklama Bankacılığı",
        "Hissedar İşlemleri ve EGKS",
        "Vadesiz Altın İşlemleri",
        "Alış/Satış",
        "Fiyat İşlemleri",
        "Biga",
        "Talimatlı İşlemler (hesap/kredi kartı)",
        "Halk Yatırım İşlemleri",
        "Hesap Açılış Eşleştirme",
        "Belge Transfer",
        "Para Transferleri",
        "Menkul Kıymet Piyasa İşlemleri",
        "Piyasa Alış/Satış",
        "Repo",
        "Teminatlı Kredi",
        "TL/YP Repo",
        "Türev ve TRS İşlemleri",
        "Halk Emir",
        "Döviz ve Kıymetli Maden Alış/Satış Talimatları",
        "Hazcom Raporlama Hizmeti",
        "Yatırım Sabit Getirili İşlemleri",
        "Yatırım Fonu İşlemleri (Bankamız Fon İşlemleri)",
    };

    // Child havuzu (teknik doküman 11). Bu node'lar daha sonra parent olarak da kullanılabilir.
    private static readonly string[] ChildNames =
    {
        "İşlem girişi",
        "İşlem izleme",
        "Raporlama",
        "Yasal bildirimler",
        "Reeskont",
        "İptal işlemi",
        "Vadeden önce geri alış",
        "Müşteri 360 ekranı etkisi",
        "İnternet/dialog şubede kullanım",
        "Mobil uygulama kullanımı",
        "ATM den etkisi",
        "Yetkilendir",
        "İtfa/vades sonu dönüş",
        "Fiyat / oran Tanımlama",
        "Karlılık",
        "Mizan/muhasebe",
        "Müşteri banka işlemi",
    };

    /// <summary>Katalogdaki tüm node'ları üretir. Parent'lar root olabilir; child'lar havuzda bekler.</summary>
    public static IReadOnlyList<Node> BuildNodes()
    {
        var nodes = new List<Node>();
        var order = 0;

        foreach (var name in ParentNames)
        {
            // "Repo" bilinçli olarak "repo" kimliğini alır; diğer parent'lar P## kimliği.
            var id = name == "Repo" ? RepoNodeId : new NodeId($"P{order:00}");
            nodes.Add(new Node(id, name, canBeRoot: true, displayOrder: order, createdBy: "seed"));
            order++;
        }

        var childIndex = 0;
        foreach (var name in ChildNames)
        {
            nodes.Add(new Node(new NodeId($"C{childIndex:00}"), name, canBeRoot: false, displayOrder: order, createdBy: "seed"));
            order++;
            childIndex++;
        }

        return nodes;
    }
}
