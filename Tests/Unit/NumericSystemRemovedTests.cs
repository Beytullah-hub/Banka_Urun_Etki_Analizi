using System.Reflection;
using BankProductImpact.Infrastructure.Nodes;
using BankProductImpact.Presentation.ViewModels;
using Xunit;

namespace BankProductImpact.Tests.Unit;

/// <summary>
/// Temizlik testleri: sayısal etki analizi sisteminin (Excel/Mock/SQLite ürün-metrik
/// repository'leri, ImpactEngine, MetricDetailWindow, senaryo kontrolleri) koddan
/// tamamen kaldırıldığını doğrular — proje artık yalnızca dinamik parent-child node
/// ağacı ve child bilgi yönetimidir.
/// </summary>
public sealed class NumericSystemRemovedTests
{
    [Fact] // MainViewModel artık ProductQueryService/ImpactAnalysisService almıyor; sade imza
    public void MainViewModel_SadeKurucuyaSahip()
    {
        var ctor = typeof(MainViewModel).GetConstructors().Single();
        var parameterTypeNames = ctor.GetParameters().Select(p => p.ParameterType.Name).ToList();

        Assert.Equal(new[] { "String", "INodeRepository", "IWriteLockService", "IChildInformationService" }, parameterTypeNames);
    }

    [Fact] // MainViewModel'de artık sayısal senaryo/etki üyeleri yok
    public void MainViewModel_SayisalUyeleriYok()
    {
        var members = typeof(MainViewModel).GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name).ToHashSet();

        foreach (var removed in new[]
        {
            "RunScenarioCommand", "ClearCommand", "SelectNodeCommand", "OpenDetailCommand",
            "SelectedNode", "NewValueText", "NewValueLabel", "IncludeIndirect", "StatusMessage",
            "Nodes", "Lines", "Filters", "ScenarioNodes", "Results", "Influencers", "Influenced",
            "AnalysisNotes", "ProductName", "ProductType", "ProductStatus", "MetricCountText"
        })
        {
            Assert.DoesNotContain(removed, members);
        }
    }

    [Fact] // MetricDetailWindow tipi projede yok (hiçbir yerden açılmıyor)
    public void MetricDetailWindow_ProjedeYok()
    {
        var presentationAssembly = typeof(MainViewModel).Assembly;
        Assert.DoesNotContain(presentationAssembly.GetTypes(), t => t.Name == "MetricDetailWindow" || t.Name == "MetricDetailViewModel");
    }

    [Fact] // Eski sayısal etki tablolarına erişen aktif bir repository/servis tipi yok
    public void EskiEtkiRepositoryTipleriYok()
    {
        var infrastructureAssembly = typeof(InMemoryNodeRepository).Assembly;
        var removedTypeNames = new[]
        {
            "ExcelProductDataRepository", "MockProductDataRepository", "SqliteProductDataRepository",
            "RuleFactory", "ExcelWorkbookInitializer"
        };

        foreach (var name in removedTypeNames)
            Assert.DoesNotContain(infrastructureAssembly.GetTypes(), t => t.Name == name);
    }

    [Fact] // Domain katmanında artık sayısal etki motoru/kural tipleri yok
    public void DomainKatmaninda_EtkiMotoruYok()
    {
        var domainAssembly = typeof(BankProductImpact.Domain.Nodes.Node).Assembly;
        var removedTypeNames = new[]
        {
            "ImpactEngine", "ImpactResult", "ImpactAnalysis", "MetricRelationship",
            "Metric", "Product", "Scenario", "CoefficientRule", "PercentChangeCalculator"
        };

        foreach (var name in removedTypeNames)
            Assert.DoesNotContain(domainAssembly.GetTypes(), t => t.Name == name);
    }

    [Fact] // Ana ekran XAML'inde artık sayısal senaryo kontrolleri yok
    public void AnaEkranda_SayisalSenaryoKontroluYok()
    {
        var xamlPath = FindMainWindowXaml();
        var xaml = File.ReadAllText(xamlPath);

        foreach (var text in new[] { "Senaryoyu Çalıştır", "ETKİ SONUÇLARI", "GRUP FİLTRELERİ", "SENARYO ANALİZİ" })
            Assert.DoesNotContain(text, xaml);
    }

    private static string FindMainWindowXaml()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "Presentation", "MainWindow.xaml");
            if (File.Exists(candidate))
                return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        throw new FileNotFoundException("MainWindow.xaml bulunamadı (test ortamı proje kökünün üstünde mi çalışıyor?).");
    }
}
