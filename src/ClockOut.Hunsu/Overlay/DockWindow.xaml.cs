// 기능: 가장자리 도크 창의 동작. 가장자리 쪽에 모듈 아이콘 런처(Ui/Frame/LauncherView)를 두고, 열린 모듈 패널을 그 안쪽에 order 순으로 쌓는다.
//       패널이 넘치면 order가 큰 것부터 접는다(Core/DockLayout). 훈 원 우클릭 메뉴 이벤트를 밖으로 알린다. 커서 위치를 60ms마다 런처에 넣는다(깨우기).
// 사용법: var d = new DockWindow(Places.EdgeRight, launcherSettings); d.Show(); d.SetModules(list); d.Relayout();  d.ZoneRect() → 훈 원 자리(측정용)
// 의존: Core/Layout(DockLayout, Places, LauncherLayout), Ui/Frame/LauncherView, Shell/ModuleView, Native/User32. 창은 활성화되지 않는다(엑셀 포커스를 뺏지 않음).
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Core.Settings;
using ClockOut.Hunsu.Native;
using ClockOut.Hunsu.Shell;

namespace ClockOut.Hunsu.Overlay;

public partial class DockWindow : Window
{
    private readonly List<ModuleView> _modules = [];
    private readonly HunsuSettings _settings;
    private readonly DispatcherTimer _pointer;
    private readonly MenuItem _toggleAll = new() { Header = "패널 모두 접기" };

    internal DockWindow(string side, HunsuSettings settings)
    {
        InitializeComponent();
        Side = side;
        _settings = settings;
        bool left = side == Places.EdgeLeft;
        Column.HorizontalAlignment = left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        Launcher.HubMenu = HubMenu();
        Launcher.ModuleClicked += id => ModuleIconClicked?.Invoke(id);
        Launcher.Configure(settings.DockShape, settings.DockGroups, LauncherShapes.Scale(settings.DockSize), settings.IdleOpacity, settings.IconProfile, mirror: left);
        Resize(0);
        SourceInitialized += (_, _) =>
        {
            Handle = new WindowInteropHelper(this).Handle;
            long ex = User32.GetWindowLongPtr(Handle, User32.GWL_EXSTYLE).ToInt64();
            User32.SetWindowLongPtr(Handle, User32.GWL_EXSTYLE, new IntPtr(ex | User32.WS_EX_NOACTIVATE | User32.WS_EX_TOOLWINDOW));
        };
        SizeChanged += (_, _) => Relayout();
        // 커서가 가장자리에 다가왔는지 본다(투명한 곳은 마우스 이벤트가 오지 않아 위치를 직접 읽는다). 보일 때만 돈다.
        _pointer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(60) };
        _pointer.Tick += (_, _) => FeedPointer();
        IsVisibleChanged += (_, _) => { if (IsVisible) _pointer.Start(); else { _pointer.Stop(); Launcher.UpdatePointer(null); } };
    }

    public string Side { get; }
    public IntPtr Handle { get; private set; }

    /// <summary>측정·문제 찾기용: 넘침 계산 입력과 결과 한 줄.</summary>
    public event Action<string>? LayoutTraced;
    public event Action? ToggleAllRequested;
    public event Action? SummonRequested;
    public event Action? HideAllRequested;
    public event Action? QuitRequested;
    public event Action? ManagerRequested;

    /// <summary>런처의 모듈 아이콘을 눌렀다(모듈 id): 패널 열기·닫기.</summary>
    public event Action<string>? ModuleIconClicked;

    /// <summary>모듈 아이콘 우클릭 메뉴를 만드는 곳(ShellController가 넣는다).</summary>
    internal Func<string, ContextMenu?>? ModuleMenu { get => Launcher.ModuleMenu; set => Launcher.ModuleMenu = value; }

    internal IReadOnlyList<ModuleView> Modules => _modules;

    internal void SetModules(IEnumerable<ModuleView> modules)
    {
        _modules.Clear();
        _modules.AddRange(modules.OrderBy(m => m.Placement.Order));
        Items.Children.Clear();
        foreach (var m in _modules)
        {
            if (m.Slot.Parent is Panel p) p.Children.Remove(m.Slot);
            m.UseTab = false; // 접힌 모듈은 손잡이 대신 런처 아이콘으로만 남는다
            m.Slot.Margin = new Thickness(0, 0, 0, (double)FindResource("Hunsu.Gap"));
            ((FrameworkElement)m.Slot).HorizontalAlignment = Column.HorizontalAlignment;
            m.Tab.HorizontalAlignment = Column.HorizontalAlignment;
            m.Frame.Width = (double)FindResource("Hunsu.PanelWidth");
            m.Tab.Visibility = Visibility.Collapsed;
            Items.Children.Add(m.Slot);
        }
        Resize(_modules.Count);
        RefreshLauncher();
        Relayout();
    }

    /// <summary>런처 아이콘(이름, 아이콘, 배지, 열림)을 지금 모듈 상태로 다시 넣는다.</summary>
    internal void RefreshLauncher() =>
        Launcher.SetModules(_modules.Select(m => new LauncherModule(m.Id, m.Module.Manifest.Name, m.Placement.Order, m.Module.Manifest.Icon, m.Badge, m.Tone, m.ShowsPanel)).ToList());

    /// <summary>창 너비 = 패널 너비 + 런처 폭 + 그림자 여백. 패널 칸은 런처 폭만큼 가장자리에서 떨어진다.</summary>
    private void Resize(int count)
    {
        var shadow = (Thickness)FindResource("Hunsu.ShadowMargin");
        double reserved = LauncherShapes.ReservedWidth(_settings.DockShape, Math.Max(count, 5), LauncherShapes.Scale(_settings.DockSize), _settings.DockGroups);
        Width = (double)FindResource("Hunsu.PanelWidth") + reserved + shadow.Left + shadow.Right;
        bool left = Side == Places.EdgeLeft;
        Column.Margin = new Thickness(shadow.Left + (left ? reserved : 0), shadow.Top, shadow.Right + (left ? 0 : reserved), shadow.Bottom);
    }

    /// <summary>넘침 계산: 각 칸의 펼친 높이를 재어 DockLayout에 넣는다(손잡이는 그리지 않아 높이 0).</summary>
    public void Relayout()
    {
        if (_modules.Count == 0 || ActualHeight <= 0) return;
        double available = ActualHeight - Column.Margin.Top - Column.Margin.Bottom;
        double gap = (double)FindResource("Hunsu.Gap");
        var size = new Size((double)FindResource("Hunsu.PanelWidth"), double.PositiveInfinity);
        var entries = _modules.Select(m =>
        {
            m.Frame.Measure(size);
            double panel = m.Frame.Visibility == Visibility.Visible && m.Frame.DesiredSize.Height > 0 ? m.Frame.DesiredSize.Height : m.LastPanelHeight;
            return new DockEntry(m.Id, m.Placement.Order, m.Placement.Collapsed, panel, 0);
        }).ToList();
        var plan = DockLayout.Plan(entries, available, gap);
        LayoutTraced?.Invoke($"available={available:0} " + string.Join(";", entries.Select(e => $"{e.Id}:{e.PanelHeight:0}")) + " forced=" + string.Join(",", plan.Where(p => p.ForcedByOverflow).Select(p => p.Id)));
        bool changed = false;
        foreach (var slot in plan)
        {
            var m = _modules.First(x => x.Id == slot.Id);
            changed |= m.ForcedCollapsed != slot.ForcedByOverflow;
            m.SetForcedCollapsed(slot.ForcedByOverflow);
        }
        if (changed) RefreshLauncher();
    }

    public void SetAllCollapsedLabel(bool allCollapsed) => _toggleAll.Header = allCollapsed ? "패널 모두 펼치기" : "패널 모두 접기";

    /// <summary>클릭을 받는 구역(훈 원). 창 왼쪽 위 기준, 실제 픽셀(측정 도구가 클릭 통과를 확인할 때 씀).</summary>
    public PixelRect ZoneRect()
    {
        var r = Launcher.HubBounds;
        if (r.IsEmpty) return default;
        return ToDevice(Launcher.TransformToAncestor(this).TransformBounds(r));
    }

    /// <summary>모듈 패널의 자리(창 기준 실제 픽셀). 보이지 않으면 빈 사각형.</summary>
    internal PixelRect PanelRect(ModuleView m) =>
        m.ShowsPanel && m.Frame.IsVisible ? ToDevice(m.Frame.TransformToAncestor(this).TransformBounds(new Rect(0, 0, m.Frame.ActualWidth, m.Frame.ActualHeight))) : default;

    private PixelRect ToDevice(Rect r)
    {
        var src = PresentationSource.FromVisual(this);
        if (src?.CompositionTarget == null || !IsVisible) return default;
        var m = src.CompositionTarget.TransformToDevice;
        var p1 = m.Transform(r.TopLeft);
        var p2 = m.Transform(r.BottomRight);
        return PixelRect.FromLtrb((int)Math.Ceiling(p1.X), (int)Math.Ceiling(p1.Y), (int)Math.Floor(p2.X), (int)Math.Floor(p2.Y));
    }

    private void FeedPointer()
    {
        if (Handle == IntPtr.Zero || !User32.GetCursorPos(out var pt)) return;
        var win = User32.WindowBounds(Handle);
        if (!win.Contains(pt.X, pt.Y)) { Launcher.UpdatePointer(null); return; }
        var local = PointFromScreen(new Point(pt.X, pt.Y));
        Launcher.UpdatePointer(TranslatePoint(local, Launcher));
    }

    private ContextMenu HubMenu()
    {
        var menu = new ContextMenu();
        MenuItem Item(string header, string gesture, Action? act)
        {
            var i = new MenuItem { Header = header, InputGestureText = gesture };
            i.Click += (_, _) => act?.Invoke();
            return i;
        }
        _toggleAll.Click += (_, _) => ToggleAllRequested?.Invoke();
        menu.Items.Add(_toggleAll);
        menu.Items.Add(Item("부르기 · 닫기", "Ctrl+Alt+H", () => SummonRequested?.Invoke()));
        menu.Items.Add(Item("전부 숨기기", "Ctrl+Alt+Shift+H", () => HideAllRequested?.Invoke()));
        menu.Items.Add(Item("모듈 관리…", "", () => ManagerRequested?.Invoke()));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("훈수꾼 끄기", "", () => QuitRequested?.Invoke()));
        return menu;
    }
}
