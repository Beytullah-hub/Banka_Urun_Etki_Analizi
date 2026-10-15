using BankProductImpact.Infrastructure.Nodes;
using BankProductImpact.Presentation.ViewModels;
using Xunit;

namespace BankProductImpact.Tests.Unit;

/// <summary>
/// Parent seçimi ViewModel davranışı: varsayılan parent Repo; başka parent seçilince
/// başlık ve ağaç güncellenir. Repo, sayısal etki sisteminin kaldırılmasından sonra
/// açılışta varsayılan seçili olma dışında özel bir muameleye tabi değildir.
/// </summary>
public sealed class MainViewModelParentTests
{
    private static MainViewModel CreateViewModel() =>
        new("test", new InMemoryNodeRepository());

    [Fact] // Uygulama açılışında varsayılan parent kesinlikle Repo'dur
    public void Acilis_VarsayilanParentRepodur()
    {
        var vm = CreateViewModel();

        Assert.NotNull(vm.SelectedParent);
        Assert.True(vm.SelectedParent!.IsRepo);
        Assert.Equal("Repo", vm.HeaderTitle);
    }

    [Fact] // Parent listesi alfabetik (Türkçe) sıralı
    public void ParentListesi_AlfabetikSirali()
    {
        var vm = CreateViewModel();
        var comparer = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), ignoreCase: true);
        var names = vm.Parents.Select(p => p.Name).ToList();

        Assert.Equal(names.OrderBy(n => n, comparer).ToList(), names);
    }

    [Fact] // Parent listesi Repo dahil kök node'ları içerir
    public void ParentListesi_RepoVeDigerleriniIcerir()
    {
        var vm = CreateViewModel();

        Assert.Contains(vm.Parents, p => p.IsRepo);
        Assert.Contains(vm.Parents, p => p.Name == "Yatırım İşlemleri");
    }

    [Fact] // Repo dışı parent seçilince başlık ve ağaç o parent'a göre güncellenir
    public void FarkliParentSecilince_BaslikVeAgacGuncellenir()
    {
        var vm = CreateViewModel();
        var other = vm.Parents.First(p => !p.IsRepo);

        vm.SelectedParent = other;

        Assert.Equal(other.Name, vm.HeaderTitle);
    }

    [Fact] // Repo'ya geri dönülünce başlık tekrar Repo olur
    public void RepoyaDonulunce_BaslikRepoyaDoner()
    {
        var vm = CreateViewModel();

        vm.SelectedParent = vm.Parents.First(p => !p.IsRepo);
        vm.SelectedParent = vm.Parents.First(p => p.IsRepo);

        Assert.Equal("Repo", vm.HeaderTitle);
    }

    [Fact] // Parent arama listeyi filtreler
    public void ParentArama_ListeyiFiltreler()
    {
        var vm = CreateViewModel();

        vm.ParentSearchText = "Repo";

        Assert.Contains(vm.Parents, p => p.Name == "Repo");
        Assert.DoesNotContain(vm.Parents, p => p.Name == "Yatırım İşlemleri");
    }
}
