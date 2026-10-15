using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using BankProductImpact.Domain.Common;
using BankProductImpact.Presentation.ViewModels;

namespace BankProductImpact.Presentation;

/// <summary>
/// Ana pencere kod-arkası. MVVM gereği iş mantığı ViewModel'dedir; burada yalnızca
/// doğrudan görünüm etkileşimi (sürükle-bırak, çıkarma onayı) ele alınır (teknik doküman 7, 9).
/// </summary>
public partial class MainWindow : Window
{
    private GenericChildNodeViewModel? _draggedChild;
    private Point _dragOffset;
    private Point _dragStart;
    private bool _isDraggingChild;
    private const double ClickThreshold = 4;

    // Hover debounce: her hover'da hemen sorgu atmak yerine kısa bir gecikme sonrası yükle
    // (teknik doküman 8). Süre dolmadan başka karta geçilirse önceki istek iptal olur.
    private readonly DispatcherTimer _hoverTimer;
    private NodeId? _pendingHoverNode;

    public MainWindow()
    {
        InitializeComponent();
        _hoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _hoverTimer.Tick += HoverTimer_Tick;
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    // ---- Generik child kartı: hover (önizleme), sürükleme (taşıma), tıklama (sabitle) ----

    private void Child_MouseEnter(object sender, MouseEventArgs e)
    {
        // Hover: debounce sonrası önizleme yükle (sabit değilse).
        if (sender is not FrameworkElement { DataContext: GenericChildNodeViewModel child })
            return;
        _pendingHoverNode = child.NodeId;
        _hoverTimer.Stop();
        _hoverTimer.Start();
    }

    private void Child_MouseLeave(object sender, MouseEventArgs e)
    {
        // Kart terk edilince bekleyen hover isteği iptal edilir (gereksiz sorgu olmasın).
        _hoverTimer.Stop();
    }

    private void HoverTimer_Tick(object? sender, EventArgs e)
    {
        _hoverTimer.Stop();
        if (_pendingHoverNode is { } node)
            ViewModel?.PreviewChildInfo(node);
    }

    private void Child_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // × düğmesine tıklanıyorsa sürüklemeyi başlatma.
        if (IsWithinButton(e.OriginalSource as DependencyObject))
            return;
        // Sürükleme yalnızca düzenleme modunda; görüntüleme modunda tıklama bilgi panelini sabitler.
        if (ViewModel is not { IsEditMode: true })
            return;
        if (sender is not FrameworkElement { DataContext: GenericChildNodeViewModel child })
            return;

        _draggedChild = child;
        var pos = e.GetPosition(GenericDropSurface);
        _dragStart = pos;
        _dragOffset = new Point(pos.X - child.Left, pos.Y - child.Top);
        _isDraggingChild = true;
        ((UIElement)sender).CaptureMouse();
    }

    private void Child_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingChild || _draggedChild is null || e.LeftButton != MouseButtonState.Pressed)
            return;

        var pos = e.GetPosition(GenericDropSurface);
        _draggedChild.Left = Math.Max(0, pos.X - _dragOffset.X);
        _draggedChild.Top = Math.Max(0, pos.Y - _dragOffset.Y);
    }

    private void Child_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingChild && _draggedChild is not null)
        {
            ((UIElement)sender).ReleaseMouseCapture();
            var moved = (e.GetPosition(GenericDropSurface) - _dragStart).Length;

            // Konum yalnızca sürükleme bitince tek seferde kaydedilir (teknik doküman 7).
            ViewModel?.MoveGenericChild(_draggedChild.NodeId, _draggedChild.Left, _draggedChild.Top);

            // Hareket ihmal edilebilirse tıklama sayılır → bilgi panelini sabitle.
            if (moved < ClickThreshold)
                RequestPin(_draggedChild.NodeId);

            _isDraggingChild = false;
            _draggedChild = null;
            return;
        }

        // Görüntüleme modunda tıklama: bilgi panelini sabitle.
        if (sender is FrameworkElement { DataContext: GenericChildNodeViewModel child })
            RequestPin(child.NodeId);
    }

    /// <summary>
    /// Bilgi panelini sabitler; ancak detay düzenleme sürüyor ve kaydedilmemiş değişiklik
    /// varsa başka child'a geçmeden önce kullanıcıyı uyarır (teknik doküman 8).
    /// </summary>
    private void RequestPin(NodeId childId)
    {
        if (ViewModel is not { } vm)
            return;

        if (vm.IsEditingChildDetails)
        {
            // Aynı child'a tıklama düzenlemeyi bozmaz.
            if (vm.SelectedChildInfo?.NodeId.Equals(childId) == true)
                return;

            if (vm.HasUnsavedChildDetails)
            {
                var confirm = MessageBox.Show(
                    "Kaydedilmemiş değişiklikler var. Devam etmek istiyor musunuz?",
                    "Kaydedilmemiş Değişiklik", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes)
                    return;
            }
            vm.CancelChildDetailEdit();
        }

        vm.PinChildInfo(childId);
    }

    // ---- Havuzdan sürükleyip grafiğe bırakma ----

    private void PoolItem_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            return;
        if (sender is FrameworkElement { DataContext: ChildPoolItemViewModel item })
            DragDrop.DoDragDrop((DependencyObject)sender, item.NodeId.Value, DragDropEffects.Copy);
    }

    private void GenericCanvas_Drop(object sender, DragEventArgs e)
    {
        if (ViewModel?.ChildPool is not { } pool)
            return;
        if (!e.Data.GetDataPresent(DataFormats.StringFormat))
            return;

        var nodeId = (string)e.Data.GetData(DataFormats.StringFormat);
        var pos = e.GetPosition(GenericDropSurface);
        // Kartı imlecin merkezine hizala (kart 172x56).
        pool.AddChildAtPosition(new NodeId(nodeId),
            Math.Max(0, pos.X - GenericChildNodeViewModel.CardWidth / 2),
            Math.Max(0, pos.Y - GenericChildNodeViewModel.CardHeight / 2));
    }

    // ---- Ağaçtan çıkarma (onaylı) ----

    private void RemoveChild_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.ChildPool is not { } pool)
            return;
        if (sender is not FrameworkElement { DataContext: GenericChildNodeViewModel child })
            return;

        var confirm = MessageBox.Show(
            "Bu öğe yalnızca mevcut ağaçtan çıkarılacaktır. Katalogdan silinmeyecektir.",
            "Ağaçtan Çıkar", MessageBoxButton.OKCancel, MessageBoxImage.Question);

        if (confirm == MessageBoxResult.OK)
            pool.RemoveChild(child.NodeId);
    }

    private void DeleteParent_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm || sender is not FrameworkElement { DataContext: ParentItemViewModel parent })
            return;

        e.Handled = true;
        var answer = MessageBox.Show(
            $"\"{parent.Name}\" kalıcı olarak silinecek.\n\nBu kaydın diğer kayıtlarla olan tüm bağlantıları ve bağlantı detayları da silinir. Devam edilsin mi?",
            "Kaydı Kalıcı Sil", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer == MessageBoxResult.Yes)
            vm.DeleteParent(parent.NodeId);
    }

    private void DeletePoolNode_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.ChildPool is not { } pool || sender is not FrameworkElement { DataContext: ChildPoolItemViewModel item })
            return;

        e.Handled = true;
        var answer = MessageBox.Show(
            $"\"{item.Name}\" kalıcı olarak silinecek.\n\nBu kaydın diğer kayıtlarla olan tüm bağlantıları ve bağlantı detayları da silinir. Devam edilsin mi?",
            "Kaydı Kalıcı Sil", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer == MessageBoxResult.Yes)
            pool.DeleteNode(item.NodeId);
    }

    private void CloseChildInfo_Click(object sender, RoutedEventArgs e) => ViewModel?.CloseChildInfo();

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        var settings = new SettingsWindow { Owner = this };
        settings.ShowDialog();
    }

    private static bool IsWithinButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is Button)
                return true;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }
}
