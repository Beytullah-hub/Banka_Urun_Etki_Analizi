using System.Windows;
using BankProductImpact.Application.Abstractions;
using BankProductImpact.Application.Nodes;
using BankProductImpact.Infrastructure.Locking;
using BankProductImpact.Infrastructure.Logging;
using BankProductImpact.Infrastructure.Nodes;
using BankProductImpact.Infrastructure.Sqlite;
using BankProductImpact.Presentation.Configuration;
using BankProductImpact.Presentation.ViewModels;

namespace BankProductImpact.Presentation;

/// <summary>
/// Kompozisyon kökü: bağımlılıklar burada bağlanır. Veri kaynağı ayardan seçilir;
/// SQLite açılamazsa uygulama düşmeden bellek içi (Yerleşik) veriye geçer.
/// </summary>
public partial class App
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var config = AppConfiguration.Load();
        var logger = new FileAppLogger();

        var (nodeRepository, nodeDetails, writeLock, dataSourceLabel) = CreateRepository(config, logger);
        var childInfoService = new ChildInformationService(nodeRepository, nodeDetails, writeLock, logger);

        var viewModel = new MainViewModel(dataSourceLabel, nodeRepository, writeLock, childInfoService);

        var window = new MainWindow { DataContext = viewModel };
        // Normal kapanışta düzenleme kilidi güvenli biçimde bırakılır.
        window.Closed += (_, _) => viewModel.OnShutdown();
        window.Show();
    }

    private static (INodeRepository Nodes, INodeDetailsRepository Details, IWriteLockService WriteLock, string Label) CreateRepository(AppConfiguration config, IAppLogger logger)
    {
        if (!config.UsesSqlite)
            return (new InMemoryNodeRepository(), new InMemoryNodeDetailsRepository(), new InMemoryWriteLockService(), "Veri: Yerleşik");

        try
        {
            var factory = new SqliteConnectionFactory(config.ResolveDatabasePath());
            // İlk çalıştırmada şema oluşturulur, node kataloğu seed edilir.
            SqliteDatabaseInitializer.EnsureCreated(factory);
            return (new SqliteNodeRepository(factory), new SqliteNodeDetailsRepository(factory),
                new SqliteWriteLockService(factory), "Veri: SQLite");
        }
        catch (Exception ex)
        {
            logger.Error("SQLite veri kaynağı açılamadı; yerleşik veriye geçiliyor.", ex);
            MessageBox.Show(
                $"SQLite veri kaynağı açılamadı:\n{ex.Message}\n\nUygulama geçici olarak yerleşik veriyle devam edecek.",
                "Veri Kaynağı Uyarısı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return (new InMemoryNodeRepository(), new InMemoryNodeDetailsRepository(), new InMemoryWriteLockService(), "Veri: Yerleşik (SQLite hatası)");
        }
    }
}
