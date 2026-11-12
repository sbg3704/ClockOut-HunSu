// 기능: 가장자리 도크 창의 동작. 모듈 칸을 order 순으로 쌓고, 넘치면 order가 큰 것부터 손잡이로 접는다(Core/DockLayout). 알약 메뉴 이벤트를 밖으로 알린다.
// 사용법: var d = new DockWindow(Places.EdgeRight); d.Show(); d.SetModules(list); d.Relayout();  d.ZoneRect() → 알약 자리(측정용)
// 의존: Core/Layout(DockLayout, Places), Shell/ModuleView, Native/User32. 창은 활성화되지 않는다(엑셀 포커스를 뺏지 않음).
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Native;
using ClockOut.Hunsu.Shell;

namespace ClockOut.Hunsu.Overlay;

public partial class DockWindow : Window
{
    private readonly List<ModuleView> _modules = [];

    internal DockWindow(string side)
    {
        InitializeComponent();
        Side = side;
        bool left = side == Places.EdgeLeft;
        Column.HorizontalAlignment = left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        Pill.Visibility = left ? Visibility.Collapsed : Visibility.Visible; // 알약은 오른쪽 도크에만
        SourceInitialized += (_, _) =>
        {
            Handle = new WindowInteropHelper(this).Handle;
            long ex = User32.GetWindowLongPtr(Handle, User32.GWL_EXSTYLE).ToInt64();
            User32.SetWindowLongPtr(Handle, User32.GWL_EXSTYLE, new IntPtr(ex | User32.WS_EX_NOACTIVATE | User32.WS_EX_TOOLWINDOW));
        };
        SizeChanged += (_, _) => Relayout();
    }

    public string Side { get; }
    public IntPtr Handle { get; private set; }

    public event Action? PillClicked;

    /// <summary>측정·문제 찾기용: 넘침 계산 입력과 결과 한 줄.</summary>
    public event Action<string>? LayoutTraced;
    public event Action? ToggleAllRequested;
    public event Action? SummonRequested;
    public event Action? HideAllRequested;
    public event Action? QuitRequested;
    public event Action? ManagerRequested;

    internal IReadOnlyList<ModuleView> Modules => _modules;

    internal void SetModules(IEnumerable<ModuleView> modules)
    {
        _modules.Clear();
        _modules.AddRange(modules.OrderBy(m => m.Placement.Order));
        Items.Children.Clear();
        foreach (var m in _modules)
        {
            if (m.Slot.Parent is Panel p) p.Children.Remove(m.Slot);
            m.Slot.Margin = new Thickness(0, 0, 0, (double)FindResource("Hunsu.Gap"));
            ((FrameworkElement)m.Slot).HorizontalAlignment = Column.HorizontalAlignment;
            m.Tab.HorizontalAlignment = Column.HorizontalAlignment;
            m.Frame.Width = (double)FindResource("Hunsu.PanelWidth");
            Items.Children.Add(m.Slot);
        }
        Relayout();
    }

    /// <summary>넘침 계산: 각 칸의 펼친 높이와 손잡이 높이를 재어 DockLayout에 넣는다.</summary>
    public void Relayout()
    {
        if (_modules.Count == 0 || ActualHeight <= 0) return;
        double available = ActualHeight - Column.Margin.Top - Column.Margin.Bottom - (Pill.Visibility == Visibility.Visible ? Pill.ActualHeight + Pill.Margin.Bottom : 0);
        double gap = (double)FindResource("Hunsu.Gap");
        var size = new Size((double)FindResource("Hunsu.PanelWidth"), double.PositiveInfinity);
        var entries = _modules.Select(m =>
        {
            m.Frame.Measure(size);
            m.Tab.Measure(size);
            double panel = m.Frame.Visibility == Visibility.Visible && m.Frame.DesiredSize.Height > 0 ? m.Frame.DesiredSize.Height : m.LastPanelHeight;
            return new DockEntry(m.Id, m.Placement.Order, m.Placement.Collapsed, panel, Math.Max(m.Tab.DesiredSize.Height, 60));
        }).ToList();
        var plan = DockLayout.Plan(entries, available, gap);
        LayoutTraced?.Invoke($"available={available:0} " + string.Join(";", entries.Select(e => $"{e.Id}:{e.PanelHeight:0}/{e.TabHeight:0}")) + " forced=" + string.Join(",", plan.Where(p => p.ForcedByOverflow).Select(p => p.Id)));
        foreach (var slot in plan)
            _modules.First(m => m.Id == slot.Id).SetForcedCollapsed(slot.ForcedByOverflow);
    }

    public void SetAllCollapsedLabel(bool allCollapsed) => MenuToggleAll.Header = allCollapsed ? "패널 모두 펼치기" : "패널 모두 접기";

    /// <summary>클릭을 받는 알약 구역. 창 왼쪽 위 기준, 실제 픽셀(측정 도구가 클릭 통과를 확인할 때 씀).</summary>
    public PixelRect ZoneRect() => DeviceRect(Pill);

    /// <summary>첫 모듈 패널의 자리(창 기준 실제 픽셀). 보이지 않으면 빈 사각형.</summary>
    internal PixelRect PanelRect(ModuleView m) => m.ShowsPanel ? DeviceRect(m.Frame) : default;

    private PixelRect DeviceRect(FrameworkElement e)
    {
        var src = PresentationSource.FromVisual(this);
        if (src?.CompositionTarget == null || !e.IsVisible) return default;
        var r = e.TransformToAncestor(this).TransformBounds(new Rect(0, 0, e.ActualWidth, e.ActualHeight));
        var m = src.CompositionTarget.TransformToDevice;
        var p1 = m.Transform(r.TopLeft);
        var p2 = m.Transform(r.BottomRight);
        return PixelRect.FromLtrb((int)Math.Ceiling(p1.X), (int)Math.Ceiling(p1.Y), (int)Math.Floor(p2.X), (int)Math.Floor(p2.Y));
    }

    private void Pill_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => PillClicked?.Invoke();
    private void MenuToggleAll_Click(object sender, RoutedEventArgs e) => ToggleAllRequested?.Invoke();
    private void MenuSummon_Click(object sender, RoutedEventArgs e) => SummonRequested?.Invoke();
    private void MenuHideAll_Click(object sender, RoutedEventArgs e) => HideAllRequested?.Invoke();
    private void MenuManager_Click(object sender, RoutedEventArgs e) => ManagerRequested?.Invoke();
    private void MenuQuit_Click(object sender, RoutedEventArgs e) => QuitRequested?.Invoke();
}
