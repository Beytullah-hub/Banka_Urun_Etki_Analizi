using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Windows.Input;
using BankProductImpact.Application.Abstractions;
using BankProductImpact.Application.Locking;
using BankProductImpact.Application.Nodes;
using BankProductImpact.Domain.Common;
using BankProductImpact.Domain.Nodes;
using BankProductImpact.Presentation.Mvvm;

namespace BankProductImpact.Presentation.ViewModels;

/// <summary>
/// Ana ekranın ViewModel'i: parent ağacını kurar, seçili parent'a bağlı child'ları
/// gösterir, "Ağacı Düzenle" modunu ve tek yazıcı kilidini yönetir, child bilgi
/// panelini (hover/pin/düzenle) sürer. İş mantığı Application/Domain katmanındadır;
/// burada yalnızca ekran durumu yönetilir.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly INodeRepository? _nodeRepository;
    private readonly IChildInformationService? _childInfoService;
    private readonly Dictionary<string, ChildInformation> _childInfoCache = new();
    private readonly IWriteLockService? _writeLock;
    private readonly List<ParentItemViewModel> _allParents = new();
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(10);
    private Timer? _heartbeatTimer;

    private ParentItemViewModel? _selectedParent;
    private string _parentSearchText = string.Empty;
    private bool _isEditMode;
    private string _editModeStatus = "Görüntüleme modu (salt okunur).";

    public MainViewModel(
        string dataSourceLabel,
        INodeRepository? nodeRepository = null,
        IWriteLockService? writeLock = null,
        IChildInformationService? childInfoService = null)
    {
        DataSourceLabel = dataSourceLabel;
        _nodeRepository = nodeRepository;
        _writeLock = writeLock;
        _childInfoService = childInfoService;

        if (nodeRepository is not null)
        {
            ChildPool = new ChildPoolViewModel(nodeRepository, () => IsEditMode, RefreshAfterTreeEdit, Environment.UserName);
            AddParent = new AddParentViewModel(nodeRepository, writeLock, () => IsEditMode, OnParentCreated, Environment.UserName);
        }

        BuildParents();

        ToggleEditModeCommand = new RelayCommand(ToggleEditMode, () => IsEditMode || CanEditSelectedTree);
        EditChildDetailsCommand = new RelayCommand(BeginEditChildDetails, () => SelectedChildInfo is not null && _childInfoService is not null);

        RefreshEditModeStatus();
    }

    /// <summary>Kök olabilen node'ları parent listesine yükler; varsayılan seçim kesinlikle Repo'dur.</summary>
    private void BuildParents()
    {
        if (_nodeRepository is null)
            return;

        // Parent'lar alfabetik (Türkçe) sıralanır.
        foreach (var node in _nodeRepository.GetRootNodes().OrderBy(n => n.Name, TurkishComparer))
            _allParents.Add(new ParentItemViewModel(node));

        ApplyParentFilter();

        // Uygulama açılışında varsayılan parent kesinlikle Repo olmalıdır.
        var repo = _allParents.FirstOrDefault(p => p.IsRepo);
        _selectedParent = repo; // OnParentSelected'i tetiklemeden ata
        OnPropertyChanged(nameof(SelectedParent));
        LoadGenericChildren(_selectedParent);
        if (_selectedParent is not null)
            ChildPool?.SetParent(_selectedParent.NodeId);
    }

    private void ApplyParentFilter()
    {
        var query = _parentSearchText?.Trim() ?? string.Empty;
        var filtered = query.Length == 0
            ? _allParents
            : _allParents.Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        Parents.Clear();
        foreach (var parent in filtered)
            Parents.Add(parent);
    }

    private void OnParentSelected(ParentItemViewModel parent)
    {
        // Başka parent'a geçerken düzenleme modu güvenle sonlandırılır (kilit bırakılır).
        if (IsEditMode)
            ExitEditMode();

        CloseChildInfo();

        LoadGenericChildren(parent);
        ChildPool?.SetParent(parent.NodeId);

        OnPropertyChanged(nameof(HeaderTitle));
        OnPropertyChanged(nameof(GenericTreeMessage));
        OnPropertyChanged(nameof(CanEditSelectedTree));
        RefreshEditModeStatus();
        // "Ağacı Düzenle" düğmesinin aktif/pasif durumunu anında güncelle.
        CommandManager.InvalidateRequerySuggested();
    }

    // Ağaç tasarım alanı: merkez parent kutusu ortada, radyal dallar çevresinde.
    private const double CanvasWidth = 960;
    private const double CanvasHeight = 680;
    private const double CenterX = CanvasWidth / 2;
    private const double CenterY = CanvasHeight / 2;
    private const double RadiusX = 330;
    private const double RadiusY = 205;

    private void LoadGenericChildren(ParentItemViewModel? parent)
    {
        GenericChildNodes.Clear();
        GenericLines.Clear();
        if (parent is null || _nodeRepository is null)
        {
            OnPropertyChanged(nameof(GenericTreeMessage));
            return;
        }

        var relations = _nodeRepository.GetChildRelations(parent.NodeId).ToDictionary(r => r.ChildNodeId);
        // Child'lar alfabetik (Türkçe) sıralanır.
        var children = _nodeRepository.GetChildren(parent.NodeId)
            .OrderBy(c => c.Name, TurkishComparer)
            .ToList();

        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            double left, top;
            if (relations.TryGetValue(child.Id, out var rel) && rel.PositionX is { } px && rel.PositionY is { } py)
            {
                // Sürüklenerek kaydedilmiş konum.
                left = px;
                top = py;
            }
            else
            {
                // Konum yoksa merkez kutunun çevresine radyal (dal) yerleşim.
                var angle = (-Math.PI / 2) + (i * 2 * Math.PI / children.Count);
                var cx = CenterX + RadiusX * Math.Cos(angle);
                var cy = CenterY + RadiusY * Math.Sin(angle);
                left = cx - GenericChildNodeViewModel.CardWidth / 2;
                top = cy - GenericChildNodeViewModel.CardHeight / 2;
            }

            var node = new GenericChildNodeViewModel(child, left, top);
            GenericChildNodes.Add(node);
            // Merkez parent kutusundan child'a dal çizgisi (child sürüklenince takip eder).
            GenericLines.Add(new GenericBranchLineViewModel(CenterX, CenterY, node));
        }

        OnPropertyChanged(nameof(GenericTreeMessage));
    }

    private static readonly StringComparer TurkishComparer =
        StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), ignoreCase: true);

    /// <summary>Child Havuzu bir değişiklik yaptığında: ağacı ve parent listesini tazeler.</summary>
    private void RefreshAfterTreeEdit()
    {
        LoadGenericChildren(SelectedParent);
        RefreshParentList();
    }

    private void RefreshParentList()
    {
        if (_nodeRepository is null)
            return;

        var selectedId = SelectedParent?.NodeId;
        _allParents.Clear();
        foreach (var node in _nodeRepository.GetRootNodes().OrderBy(n => n.Name, TurkishComparer))
            _allParents.Add(new ParentItemViewModel(node));

        ApplyParentFilter();
        // Seçimi koru (yeni root eklenmiş olabilir).
        if (selectedId is { } id)
            _selectedParent = _allParents.FirstOrDefault(p => p.NodeId.Equals(id)) ?? _selectedParent;
    }

    /// <summary>"+ Yeni Parent Ekle" ile bir kayıt oluşturulunca listeyi tazeler ve yeni parent'ı seçer.</summary>
    private void OnParentCreated(Node node)
    {
        ParentSearchText = string.Empty; // yeni parent arama filtresine takılıp gizlenmesin
        RefreshParentList();

        var created = _allParents.FirstOrDefault(p => p.NodeId.Equals(node.Id));
        if (created is not null)
            SelectedParent = created;
    }

    /// <summary>Bir parent'ı katalogdan kalıcı olarak siler ve güvenli bir sonraki seçime geçer.</summary>
    public void DeleteParent(NodeId nodeId)
    {
        if (_nodeRepository is null || !IsEditMode)
            return;

        var wasSelected = SelectedParent?.NodeId.Equals(nodeId) == true;
        _nodeRepository.DeleteNode(nodeId, Environment.UserName);
        CloseChildInfo();

        if (wasSelected)
            _selectedParent = null;

        RefreshParentList();
        if (_selectedParent is null)
            _selectedParent = _allParents.FirstOrDefault(p => p.IsRepo) ?? _allParents.FirstOrDefault();

        OnPropertyChanged(nameof(SelectedParent));
        LoadGenericChildren(_selectedParent);
        if (_selectedParent is not null)
            ChildPool?.SetParent(_selectedParent.NodeId);
        OnPropertyChanged(nameof(HeaderTitle));
        OnPropertyChanged(nameof(CanEditSelectedTree));
    }

    // --- Child bilgi paneli ---

    private ChildInfoViewModel? _selectedChildInfo;
    private bool _isChildInfoPinned;
    private ChildDetailEditViewModel? _childDetailEdit;
    private bool _isEditingChildDetails;
    private bool _detailEditHoldsLock;
    private string? _childDetailNotice;

    /// <summary>Hover/tıklama ile seçilen child'ın bilgi paneli (yoksa null).</summary>
    public ChildInfoViewModel? SelectedChildInfo
    {
        get => _selectedChildInfo;
        private set
        {
            if (SetProperty(ref _selectedChildInfo, value))
                OnPropertyChanged(nameof(IsChildInfoVisible));
        }
    }

    public bool IsChildInfoVisible => SelectedChildInfo is not null;

    /// <summary>Hover: panel sabitlenmemişse önizleme gösterir.</summary>
    public void PreviewChildInfo(NodeId childId)
    {
        // Düzenleme sırasında hover ile başka child'a geçilmez.
        if (_isChildInfoPinned || _isEditingChildDetails)
            return;
        LoadChildInfo(childId, pin: false);
    }

    /// <summary>Tıklama: paneli sabitler; başka child seçilene veya kapatılana kadar açık kalır.</summary>
    public void PinChildInfo(NodeId childId) => LoadChildInfo(childId, pin: true);

    public void CloseChildInfo()
    {
        if (_isEditingChildDetails)
            EndDetailEdit();
        _isChildInfoPinned = false;
        SelectedChildInfo = null;
    }

    // --- Child detay düzenleme ---

    public bool IsEditingChildDetails
    {
        get => _isEditingChildDetails;
        private set => SetProperty(ref _isEditingChildDetails, value);
    }

    public ChildDetailEditViewModel? ChildDetailEdit
    {
        get => _childDetailEdit;
        private set => SetProperty(ref _childDetailEdit, value);
    }

    /// <summary>Düzenleme başlatılamadığında (kilit başkasında) panelde gösterilen uyarı.</summary>
    public string? ChildDetailNotice
    {
        get => _childDetailNotice;
        private set
        {
            if (SetProperty(ref _childDetailNotice, value))
                OnPropertyChanged(nameof(HasChildDetailNotice));
        }
    }

    public bool HasChildDetailNotice => !string.IsNullOrEmpty(_childDetailNotice);

    /// <summary>Kaydedilmemiş değişiklik var mı? (başka child'a geçerken uyarı için)</summary>
    public bool HasUnsavedChildDetails => _isEditingChildDetails && (_childDetailEdit?.IsDirty ?? false);

    /// <summary>
    /// "Detay Bilgilerini Düzenle": yapısal "Ağacı Düzenle" moduna bağlı DEĞİLDİR; yalnızca
    /// yazma kilidini gerektirir. Kilit alınamazsa form açılmaz.
    /// </summary>
    public void BeginEditChildDetails()
    {
        if (_childInfoService is null || SelectedChildInfo is null)
            return;

        // Kilit doğrula / al (aynı kullanıcı/süreç kilidi yeniden kullanır).
        if (_writeLock is not null)
        {
            var result = _writeLock.TryAcquire();
            if (!result.Acquired)
            {
                var holder = result.Status.Holder;
                var who = holder is null ? string.Empty : $" ({holder.UserName} — {holder.MachineName})";
                ChildDetailNotice = $"Düzenleme kilidi başka bir kullanıcıda; şu an düzenleyemezsiniz. Salt okunur.{who}";
                return;
            }
            // Kilidi yalnızca detay düzenleme için aldıysak (yapısal mod kilidi tutmuyorsa) yaşat.
            if (!IsEditMode && !_detailEditHoldsLock)
            {
                _detailEditHoldsLock = true;
                StartHeartbeat();
            }
        }

        ChildDetailNotice = null;
        ChildDetailEdit = new ChildDetailEditViewModel(SelectedChildInfo.Model, _childInfoService, OnChildDetailsSaved, OnChildDetailsCancelled);
        IsEditingChildDetails = true;
    }

    /// <summary>Kaydedilmemiş değişiklikleri atarak düzenlemeyi iptal eder (başka child'a geçişte).</summary>
    public void CancelChildDetailEdit() => OnChildDetailsCancelled();

    private void OnChildDetailsSaved(ChildInformation reloaded)
    {
        InvalidateChildInfo(reloaded.NodeId);
        EndDetailEdit();
        // Yeni bilgiler anında panelde görünür ve sabit kalır.
        SelectedChildInfo = new ChildInfoViewModel(reloaded);
        _isChildInfoPinned = true;
    }

    private void OnChildDetailsCancelled() => EndDetailEdit();

    private void EndDetailEdit()
    {
        IsEditingChildDetails = false;
        ChildDetailEdit = null;

        // Kilidi yalnızca detay düzenleme için almışsak bırak (yapısal mod kullanıyorsa dokunma).
        if (_detailEditHoldsLock)
        {
            StopHeartbeat();
            _writeLock?.Release();
            _detailEditHoldsLock = false;
        }
    }

    /// <summary>Detay kaydından sonra ilgili child'ın önbelleği temizlenir.</summary>
    public void InvalidateChildInfo(NodeId childId)
    {
        var parentKey = SelectedParent?.NodeId.Value ?? string.Empty;
        _childInfoCache.Remove($"{childId.Value}|{parentKey}");
    }

    private void LoadChildInfo(NodeId childId, bool pin)
    {
        if (_childInfoService is null)
            return;

        // Aynı child (aynı parent bağlamında) için tekrar sorgu atmamak üzere küçük bir önbellek.
        var parentId = SelectedParent?.NodeId;
        var key = $"{childId.Value}|{parentId?.Value ?? string.Empty}";

        if (!_childInfoCache.TryGetValue(key, out var info))
        {
            info = _childInfoService.Load(childId, parentId);
            _childInfoCache[key] = info;
        }

        SelectedChildInfo = new ChildInfoViewModel(info);
        if (pin)
            _isChildInfoPinned = true;
    }

    /// <summary>Sürükleme bitince child'ın yeni konumunu kaydeder (tek kayıt).</summary>
    public void MoveGenericChild(NodeId childId, double left, double top)
    {
        if (!IsEditMode || _nodeRepository is null || SelectedParent is null)
            return;

        var node = GenericChildNodes.FirstOrDefault(n => n.NodeId.Equals(childId));
        if (node is not null)
        {
            node.Left = left;
            node.Top = top;
        }
        _nodeRepository.UpdateChildPosition(SelectedParent.NodeId, childId, left, top, Environment.UserName);
    }

    // --- Başlık ---

    /// <summary>Aktif veri kaynağı (SQLite / Excel / yerleşik veri) — başlıkta gösterilir.</summary>
    public string DataSourceLabel { get; }

    /// <summary>Pencere başlığı ve merkez kutu; tek kaynaktan (SelectedParent.Name) gelir.</summary>
    public string HeaderTitle => SelectedParent?.Name ?? "Banka Ürün ve Süreç İlişki Haritası";

    // --- Parent seçimi (dinamik ağaç) ---
    public ObservableCollection<ParentItemViewModel> Parents { get; } = new();

    /// <summary>Seçili parent ağacındaki konumlu child düğümleri (canvas).</summary>
    public ObservableCollection<GenericChildNodeViewModel> GenericChildNodes { get; } = new();

    /// <summary>Merkez parent kutusundan child'lara giden dal çizgileri.</summary>
    public ObservableCollection<GenericBranchLineViewModel> GenericLines { get; } = new();

    /// <summary>Child Havuzu / Ağaç Düzenleme paneli (node repository yoksa null).</summary>
    public ChildPoolViewModel? ChildPool { get; }

    /// <summary>"+ Yeni Parent Ekle" formu (node repository yoksa null).</summary>
    public AddParentViewModel? AddParent { get; }

    /// <summary>Sol panel parent arama kutusu; liste anlık filtrelenir.</summary>
    public string ParentSearchText
    {
        get => _parentSearchText;
        set
        {
            if (SetProperty(ref _parentSearchText, value))
                ApplyParentFilter();
        }
    }

    public ParentItemViewModel? SelectedParent
    {
        get => _selectedParent;
        set
        {
            if (SetProperty(ref _selectedParent, value) && value is not null)
                OnParentSelected(value);
        }
    }

    public string GenericTreeMessage =>
        GenericChildNodes.Count == 0
            ? "Bu ağaçta henüz bağlı öğe yok. \"Ağacı Düzenle\" ile katalogdan öğe ekleyin veya sürükleyin."
            : $"{GenericChildNodes.Count} bağlı öğe. Düzenleme modunda sürükleyerek taşıyabilir, × ile çıkarabilirsiniz.";

    public ICommand ToggleEditModeCommand { get; }
    public ICommand EditChildDetailsCommand { get; }

    // --- Düzenleme modu / tek yazıcı kilidi ---

    /// <summary>Düzenleme modunda mıyız (yazma kilidi bizde)?</summary>
    public bool IsEditMode
    {
        get => _isEditMode;
        private set
        {
            if (SetProperty(ref _isEditMode, value))
            {
                OnPropertyChanged(nameof(IsReadOnly));
                OnPropertyChanged(nameof(EditModeButtonText));
            }
        }
    }

    /// <summary>Düzenleme modunda değilsek salt okunurdur (ekleme/çıkarma gizli/pasif).</summary>
    public bool IsReadOnly => !IsEditMode;

    /// <summary>"Ağacı Düzenle" bir parent seçiliyken aktiftir.</summary>
    public bool CanEditSelectedTree => _writeLock is not null && SelectedParent is not null;

    public string EditModeButtonText => IsEditMode ? "Düzenlemeyi Bitir" : "Ağacı Düzenle";

    public string EditModeStatus
    {
        get => _editModeStatus;
        private set => SetProperty(ref _editModeStatus, value);
    }

    private void ToggleEditMode()
    {
        if (IsEditMode)
            ExitEditMode();
        else
            EnterEditMode();
    }

    private void EnterEditMode()
    {
        if (_writeLock is null)
            return;

        var result = _writeLock.TryAcquire();
        if (result.Acquired)
        {
            IsEditMode = true;
            StartHeartbeat();
            EditModeStatus = "Düzenleme modundasınız (kilit sizde). Değişiklikler kaydedilebilir.";
            if (ChildPool is not null)
                ChildPool.IsOpen = true;
        }
        else
        {
            IsEditMode = false;
            EditModeStatus = DescribeOtherHolder(result.Status);
        }
    }

    private void ExitEditMode()
    {
        StopHeartbeat();
        _writeLock?.Release();
        IsEditMode = false;
        if (ChildPool is not null)
            ChildPool.IsOpen = false;
        RefreshEditModeStatus();
    }

    /// <summary>Pencere kapanışında çağrılır: kilit güvenli biçimde bırakılır.</summary>
    public void OnShutdown()
    {
        StopHeartbeat();
        if (IsEditMode)
            _writeLock?.Release();
    }

    private void RefreshEditModeStatus()
    {
        if (_writeLock is null)
        {
            EditModeStatus = "Görüntüleme modu.";
            return;
        }

        var status = _writeLock.GetStatus();
        if (status.IsHeldByOther)
        {
            EditModeStatus = DescribeOtherHolder(status);
        }
        else if (SelectedParent is null)
        {
            EditModeStatus = "Düzenlemek için soldan bir başlık seçin.";
        }
        else
        {
            EditModeStatus = "Görüntüleme modu (salt okunur). Bu ağacı düzenlemek için \"Ağacı Düzenle\".";
        }
    }

    private static string DescribeOtherHolder(WriteLockStatus status)
    {
        var holder = status.Holder;
        var who = holder is null ? string.Empty : $" ({holder.UserName} — {holder.MachineName})";
        return $"Düzenleme kilidi başka bir kullanıcı tarafından kullanılıyor. Salt okunur moddasınız.{who}";
    }

    private void StartHeartbeat()
    {
        StopHeartbeat();
        // Heartbeat DB yazması; UI thread'ini bloklamamak için zamanlayıcı callback'inde (havuz thread'i) çalışır.
        _heartbeatTimer = new Timer(_ => SafeHeartbeat(), null, HeartbeatInterval, HeartbeatInterval);
    }

    private void StopHeartbeat()
    {
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = null;
    }

    private void SafeHeartbeat()
    {
        try { _writeLock?.Heartbeat(); }
        catch (Exception) { /* geçici hata düzenlemeyi bozmamalı; sonraki tik tekrar dener */ }
    }
}
