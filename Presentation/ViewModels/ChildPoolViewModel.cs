using System.Collections.ObjectModel;
using System.Windows.Input;
using BankProductImpact.Application.Abstractions;
using BankProductImpact.Domain.Common;
using BankProductImpact.Domain.Nodes;
using BankProductImpact.Presentation.Mvvm;

namespace BankProductImpact.Presentation.ViewModels;

/// <summary>
/// Child Havuzu / Ağaç Düzenleme paneli (teknik doküman 7-8). Ortak katalogdan seçerek
/// child ekleme, "Yeni Seçenek Ekle" ve ağaçtan çıkarma işlemlerini yönetir. Tüm yazma
/// işlemleri düzenleme modu (yazma kilidi) gerektirir; değişiklikler audit'e yazılır.
/// </summary>
public sealed class ChildPoolViewModel : ObservableObject
{
    private readonly INodeRepository _repository;
    private readonly Func<bool> _canEdit;
    private readonly Action _onTreeChanged;
    private readonly string _actor;
    private readonly List<ChildPoolItemViewModel> _allItems = new();

    private NodeId _parentId;
    private bool _isOpen;
    private string _searchText = string.Empty;
    private bool _showOnlyActive;
    private string _feedback = string.Empty;

    // Yeni node formu
    private bool _isNewNodeFormOpen;
    private string _newName = string.Empty;
    private string _newDescription = string.Empty;
    private bool _newIsActive = true;
    private bool _newCanBeRoot;
    private bool _newAddToCurrentParent = true;

    public ChildPoolViewModel(INodeRepository repository, Func<bool> canEdit, Action onTreeChanged, string actor)
    {
        _repository = repository;
        _canEdit = canEdit;
        _onTreeChanged = onTreeChanged;
        _actor = actor;

        ToggleOpenCommand = new RelayCommand(() => IsOpen = !IsOpen);
        AddSelectedCommand = new RelayCommand(AddSelected, () => _canEdit());
        OpenNewNodeFormCommand = new RelayCommand(() => IsNewNodeFormOpen = true, () => _canEdit());
        CancelNewNodeFormCommand = new RelayCommand(CloseNewNodeForm);
        SaveNewNodeCommand = new RelayCommand(SaveNewNode, () => _canEdit());
        RemoveChildCommand = new RelayCommand<GenericChildNodeViewModel>(child => { if (child is not null) RemoveChild(child.NodeId); });
    }

    public ObservableCollection<ChildPoolItemViewModel> Items { get; } = new();

    public ICommand ToggleOpenCommand { get; }
    public ICommand AddSelectedCommand { get; }
    public ICommand OpenNewNodeFormCommand { get; }
    public ICommand CancelNewNodeFormCommand { get; }
    public ICommand SaveNewNodeCommand { get; }
    public ICommand RemoveChildCommand { get; }

    public bool IsOpen { get => _isOpen; set => SetProperty(ref _isOpen, value); }

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) ApplyFilter(); }
    }

    public bool ShowOnlyActive
    {
        get => _showOnlyActive;
        set { if (SetProperty(ref _showOnlyActive, value)) ApplyFilter(); }
    }

    public string Feedback { get => _feedback; private set => SetProperty(ref _feedback, value); }

    // --- Yeni node formu ---
    public bool IsNewNodeFormOpen { get => _isNewNodeFormOpen; set => SetProperty(ref _isNewNodeFormOpen, value); }
    public string NewName { get => _newName; set => SetProperty(ref _newName, value); }
    public string NewDescription { get => _newDescription; set => SetProperty(ref _newDescription, value); }
    public bool NewIsActive { get => _newIsActive; set => SetProperty(ref _newIsActive, value); }
    public bool NewCanBeRoot { get => _newCanBeRoot; set => SetProperty(ref _newCanBeRoot, value); }
    public bool NewAddToCurrentParent { get => _newAddToCurrentParent; set => SetProperty(ref _newAddToCurrentParent, value); }

    /// <summary>Aktif parent değişince havuzu (bağlı işaretleriyle) yeniden yükler.</summary>
    public void SetParent(NodeId parentId)
    {
        _parentId = parentId;
        Feedback = string.Empty;
        Reload();
    }

    private static readonly StringComparer TurkishComparer =
        StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), ignoreCase: true);

    private void Reload()
    {
        _allItems.Clear();
        var boundIds = _repository.GetChildren(_parentId).Select(n => n.Id).ToHashSet();
        // Katalog alfabetik (Türkçe) sıralanır.
        foreach (var node in _repository.GetAllNodes().OrderBy(n => n.Name, TurkishComparer))
            _allItems.Add(new ChildPoolItemViewModel(node, boundIds.Contains(node.Id)));

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query = _searchText.Trim();
        IEnumerable<ChildPoolItemViewModel> filtered = _allItems;
        if (_showOnlyActive)
            filtered = filtered.Where(i => i.IsActive);
        if (query.Length > 0)
            filtered = filtered.Where(i => i.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

        Items.Clear();
        foreach (var item in filtered)
            Items.Add(item);
    }

    private void AddSelected()
    {
        if (!_canEdit())
        {
            Feedback = "Değişiklik için düzenleme modu gerekir.";
            return;
        }

        var toAdd = Items.Where(i => i.IsSelected && !i.IsBound).ToList();
        if (toAdd.Count == 0)
        {
            Feedback = "Eklenecek (işaretli ve henüz bağlı olmayan) node yok.";
            return;
        }

        foreach (var item in toAdd)
            _repository.AddOrActivateChild(_parentId, item.NodeId, null, null, _actor);

        Feedback = $"{toAdd.Count} node eklendi.";
        _onTreeChanged();
        Reload();
    }

    /// <summary>Sürükleyerek bırakılan child'ı verilen konumda ekler (teknik doküman 7).</summary>
    public void AddChildAtPosition(NodeId childId, double x, double y)
    {
        if (!_canEdit() || childId.Equals(_parentId))
            return;

        _repository.AddOrActivateChild(_parentId, childId, x, y, _actor);
        _onTreeChanged();
        Reload();
    }

    /// <summary>Child'ı yalnızca aktif ağaçtan çıkarır (katalogdan silmez).</summary>
    public void RemoveChild(NodeId childId)
    {
        if (!_canEdit())
            return;

        _repository.RemoveChild(_parentId, childId, _actor);
        Feedback = "Öğe ağaçtan çıkarıldı (katalogda korunuyor).";
        _onTreeChanged();
        Reload();
    }

    /// <summary>Node'u bütün bağlantı ve detaylarıyla ortak katalogdan kalıcı olarak siler.</summary>
    public void DeleteNode(NodeId nodeId)
    {
        if (!_canEdit())
        {
            Feedback = "Kalıcı silme için düzenleme modu gerekir.";
            return;
        }

        if (nodeId.Equals(_parentId))
        {
            Feedback = "Seçili başlık öğe havuzundan silinemez; soldaki başlık listesini kullanın.";
            return;
        }

        _repository.DeleteNode(nodeId, _actor);
        Feedback = "Kayıt katalogdan ve bağlı olduğu tüm ağaçlardan silindi.";
        _onTreeChanged();
        Reload();
    }

    private void SaveNewNode()
    {
        if (!_canEdit())
            return;

        var name = NewName.Trim();
        if (name.Length == 0)
        {
            Feedback = "Ad zorunludur.";
            return;
        }
        if (_repository.NameExists(name))
        {
            Feedback = "Bu isimde bir kayıt zaten var (büyük/küçük harf ve boşluk farkları dikkate alınmaz).";
            return;
        }

        var node = _repository.AddNode(new NodeDraft(name, string.IsNullOrWhiteSpace(NewDescription) ? null : NewDescription.Trim(), NewIsActive, NewCanBeRoot), _actor);
        if (NewAddToCurrentParent)
            _repository.AddOrActivateChild(_parentId, node.Id, null, null, _actor);

        Feedback = $"\"{node.Name}\" katalog eklendi.";
        CloseNewNodeForm();
        _onTreeChanged();
        Reload();
    }

    private void CloseNewNodeForm()
    {
        IsNewNodeFormOpen = false;
        NewName = string.Empty;
        NewDescription = string.Empty;
        NewIsActive = true;
        NewCanBeRoot = false;
        NewAddToCurrentParent = true;
    }
}
