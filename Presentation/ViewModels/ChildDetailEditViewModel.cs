using System.Windows.Input;
using BankProductImpact.Application.Abstractions;
using BankProductImpact.Application.Nodes;
using BankProductImpact.Presentation.Mvvm;

namespace BankProductImpact.Presentation.ViewModels;

/// <summary>
/// Sağ panel içi bilgi düzenleme formu. Ekran adı, batch adı ve kısa açıklama
/// alanlarını düzenler; Kaydet doğrulama + kilit kontrolüyle servise gider, Vazgeç
/// hiçbir şey yazmadan çıkar. Kaydedilmemiş değişiklik takibi için <see cref="IsDirty"/> sağlar.
/// </summary>
public sealed class ChildDetailEditViewModel : ObservableObject
{
    private readonly IChildInformationService _service;
    private readonly Action<ChildInformation> _onSaved;
    private readonly Action _onCancelled;
    private readonly ChildInformation _original;

    private string _screenName;
    private string _batchName;
    private string _shortDescription;
    private string? _feedback;

    public ChildDetailEditViewModel(
        ChildInformation info,
        IChildInformationService service,
        Action<ChildInformation> onSaved,
        Action onCancelled)
    {
        _original = info;
        _service = service;
        _onSaved = onSaved;
        _onCancelled = onCancelled;

        NodeName = info.NodeName;

        _screenName = info.ScreenName ?? string.Empty;
        _batchName = info.BatchName ?? string.Empty;
        _shortDescription = info.ShortDescription ?? string.Empty;

        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(_onCancelled);
    }

    public string NodeName { get; }

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    // Karakter sınırları (TextBox MaxLength).
    public int ScreenNameMax => ChildInformationLimits.ScreenName;
    public int BatchNameMax => ChildInformationLimits.BatchName;
    public int ShortDescriptionMax => ChildInformationLimits.ShortDescription;

    public string ScreenName { get => _screenName; set => SetField(ref _screenName, value); }
    public string BatchName { get => _batchName; set => SetField(ref _batchName, value); }
    public string ShortDescription { get => _shortDescription; set => SetField(ref _shortDescription, value); }

    public string? Feedback { get => _feedback; private set => SetProperty(ref _feedback, value); }

    /// <summary>Formda kaydedilmemiş değişiklik var mı? (başka öğeye geçerken uyarı için)</summary>
    public bool IsDirty =>
        _screenName != (_original.ScreenName ?? string.Empty) ||
        _batchName != (_original.BatchName ?? string.Empty) ||
        _shortDescription != (_original.ShortDescription ?? string.Empty);

    private void SetField(ref string field, string value)
    {
        if (SetProperty(ref field, value))
            OnPropertyChanged(nameof(IsDirty));
    }

    private void Save()
    {
        var input = new ChildInformationInput(
            _original.NodeId, _original.NodeRelationId, _original.ParentNodeId,
            _screenName, _batchName, _shortDescription);

        var result = _service.Save(input);
        if (result.Success)
        {
            _onSaved(result.Reloaded!);
        }
        else
        {
            // Hata: kullanıcı girdisi korunur, mesaj gösterilir.
            Feedback = result.Message;
        }
    }
}
