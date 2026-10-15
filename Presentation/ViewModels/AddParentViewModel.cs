using System.Windows.Input;
using BankProductImpact.Application.Abstractions;
using BankProductImpact.Domain.Nodes;
using BankProductImpact.Presentation.Mvvm;

namespace BankProductImpact.Presentation.ViewModels;

/// <summary>
/// Sol paneldeki "+ Yeni Parent Ekle" formu. Yeni bir kök (CanBeRoot=true) node
/// oluşturur; bu, yapısal "Ağacı Düzenle" oturumundan bağımsız tek seferlik bir
/// yazma işlemidir ama aynı global yazma kilidini kullanır (teknik doküman 8, 12).
/// </summary>
public sealed class AddParentViewModel : ObservableObject
{
    private readonly INodeRepository _repository;
    private readonly IWriteLockService? _writeLock;
    private readonly Func<bool> _isEditModeActive;
    private readonly Action<Node> _onCreated;
    private readonly string _actor;

    private bool _isOpen;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private bool _isActive = true;
    private string? _feedback;

    public AddParentViewModel(
        INodeRepository repository,
        IWriteLockService? writeLock,
        Func<bool> isEditModeActive,
        Action<Node> onCreated,
        string actor)
    {
        _repository = repository;
        _writeLock = writeLock;
        _isEditModeActive = isEditModeActive;
        _onCreated = onCreated;
        _actor = actor;

        OpenCommand = new RelayCommand(Open);
        CancelCommand = new RelayCommand(Close);
        SaveCommand = new RelayCommand(Save);
    }

    public ICommand OpenCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand SaveCommand { get; }

    public bool IsOpen { get => _isOpen; set => SetProperty(ref _isOpen, value); }
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string Description { get => _description; set => SetProperty(ref _description, value); }
    public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }

    public string? Feedback
    {
        get => _feedback;
        private set
        {
            if (SetProperty(ref _feedback, value))
                OnPropertyChanged(nameof(HasFeedback));
        }
    }

    public bool HasFeedback => !string.IsNullOrEmpty(_feedback);

    private void Open()
    {
        IsOpen = true;
        Feedback = null;
    }

    private void Save()
    {
        var name = Name.Trim();
        if (name.Length == 0)
        {
            Feedback = "Başlık adı zorunludur.";
            return;
        }

        // Büyük/küçük harf ve baştaki/sondaki boşluk farkları dikkate alınmadan mükerrer kontrolü.
        if (_repository.NameExists(name))
        {
            Feedback = "Bu isimde bir başlık zaten bulunuyor.";
            return;
        }

        // "Ağacı Düzenle" oturumu zaten kilidi tutuyorsa yeniden almaya/bırakmaya gerek yok;
        // tutmuyorsa bu tek seferlik yazma için kısaca alınır ve hemen bırakılır.
        var alreadyHeld = _isEditModeActive();
        if (_writeLock is not null && !alreadyHeld)
        {
            var result = _writeLock.TryAcquire();
            if (!result.Acquired)
            {
                var holder = result.Status.Holder;
                var who = holder is null ? string.Empty : $" ({holder.UserName} — {holder.MachineName})";
                Feedback = $"Düzenleme kilidi başka bir kullanıcıda; şu an başlık eklenemez.{who}";
                return;
            }
        }

        try
        {
            var description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
            var node = _repository.AddNode(new NodeDraft(name, description, IsActive, CanBeRoot: true), _actor);
            Close();
            _onCreated(node);
        }
        finally
        {
            if (_writeLock is not null && !alreadyHeld)
                _writeLock.Release();
        }
    }

    private void Close()
    {
        IsOpen = false;
        Name = string.Empty;
        Description = string.Empty;
        IsActive = true;
        Feedback = null;
    }
}
