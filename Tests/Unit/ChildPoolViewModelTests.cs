using BankProductImpact.Infrastructure.Nodes;
using BankProductImpact.Presentation.ViewModels;
using Xunit;

namespace BankProductImpact.Tests.Unit;

/// <summary>
/// Child Havuzu ViewModel davranışı (teknik doküman 7-9): seçerek ekleme, ağaçtan
/// çıkarma, yeni seçenek (ad zorunlu + mükerrer), düzenleme modu kapısı.
/// </summary>
public sealed class ChildPoolViewModelTests
{
    private readonly InMemoryNodeRepository _repo = new();
    private int _changeCount;
    private bool _canEdit = true;

    private ChildPoolViewModel CreatePool()
    {
        var pool = new ChildPoolViewModel(_repo, () => _canEdit, () => _changeCount++, "tester");
        pool.SetParent(NodeCatalogSeed.RepoNodeId);
        return pool;
    }

    private void Select(ChildPoolViewModel pool, string name) =>
        pool.Items.Single(i => i.Name == name).IsSelected = true;

    [Fact] // Katalog yüklenir
    public void SetParent_KatalogYukler()
    {
        var pool = CreatePool();
        Assert.NotEmpty(pool.Items);
        Assert.Contains(pool.Items, i => i.Name == "Raporlama");
    }

    [Fact] // Katalog alfabetik (Türkçe) sıralı
    public void Katalog_AlfabetikSirali()
    {
        var pool = CreatePool();
        var comparer = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), ignoreCase: true);
        var names = pool.Items.Select(i => i.Name).ToList();

        Assert.Equal(names.OrderBy(n => n, comparer).ToList(), names);
    }

    [Fact] // Seçerek ekleme: bağlantı oluşur, ağaç değişikliği bildirilir
    public void SecerekEkleme_BaglantiOlusturur()
    {
        var pool = CreatePool();
        Select(pool, "Raporlama");

        pool.AddSelectedCommand.Execute(null);

        Assert.Contains(_repo.GetChildren(NodeCatalogSeed.RepoNodeId), n => n.Name == "Raporlama");
        Assert.True(_changeCount >= 1);
    }

    [Fact] // Düzenleme modu kapalıyken ekleme yapılmaz
    public void DuzenlemeKapali_EklemeYapilmaz()
    {
        _canEdit = false;
        var pool = CreatePool();
        Select(pool, "Raporlama");

        pool.AddSelectedCommand.Execute(null);

        Assert.Empty(_repo.GetChildren(NodeCatalogSeed.RepoNodeId));
    }

    [Fact] // Ağaçtan çıkarma bağlantıyı kaldırır ama node katalogda kalır
    public void AgactanCikarma_KatalogdanSilmez()
    {
        var pool = CreatePool();
        var raporlama = _repo.GetAllNodes().Single(n => n.Name == "Raporlama");
        pool.AddChildAtPosition(raporlama.Id, 10, 10);

        pool.RemoveChild(raporlama.Id);

        Assert.DoesNotContain(_repo.GetChildren(NodeCatalogSeed.RepoNodeId), n => n.Id.Equals(raporlama.Id));
        Assert.NotNull(_repo.GetNode(raporlama.Id));
    }

    [Fact] // Yeni seçenek: ad zorunlu
    public void YeniSecenek_AdZorunlu()
    {
        var pool = CreatePool();
        pool.NewName = "   ";

        pool.SaveNewNodeCommand.Execute(null);

        Assert.Contains("zorunlu", pool.Feedback);
    }

    [Fact] // Yeni seçenek: mükerrer ad engellenir (büyük/küçük harf + boşluk)
    public void YeniSecenek_MukerrerAdEngellenir()
    {
        var pool = CreatePool();
        pool.NewName = "  raporlama  ";

        pool.SaveNewNodeCommand.Execute(null);

        Assert.Contains("zaten var", pool.Feedback);
    }

    [Fact] // Yeni seçenek: geçerli ad katalog eklenir ve mevcut parent'a bağlanır
    public void YeniSecenek_EklenirVeBaglanir()
    {
        var pool = CreatePool();
        pool.NewName = "Özel Ekran";
        pool.NewAddToCurrentParent = true;

        pool.SaveNewNodeCommand.Execute(null);

        Assert.True(_repo.NameExists("Özel Ekran"));
        Assert.Contains(_repo.GetChildren(NodeCatalogSeed.RepoNodeId), n => n.Name == "Özel Ekran");
    }

    [Fact]
    public void KatalogdanSilme_NodeVeTumBaglantilariKaldirir()
    {
        var pool = CreatePool();
        var node = _repo.AddNode(new BankProductImpact.Domain.Nodes.NodeDraft("Silinecek Öğe", null, true, false), "t");
        pool.SetParent(NodeCatalogSeed.RepoNodeId);
        pool.AddChildAtPosition(node.Id, 10, 10);

        pool.DeleteNode(node.Id);

        Assert.Null(_repo.GetNode(node.Id));
        Assert.DoesNotContain(_repo.GetChildren(NodeCatalogSeed.RepoNodeId), n => n.Id.Equals(node.Id));
        Assert.True(_changeCount >= 2);
    }
}
