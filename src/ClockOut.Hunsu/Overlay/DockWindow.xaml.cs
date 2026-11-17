// 기능: 가장자리 도크 창의 동작. 가장자리 쪽에 모듈 아이콘 런처(Ui/Frame/LauncherView)를 두고, 열린 모듈 패널을 그 안쪽에 order 순으로 쌓는다.
//       패널이 넘치면 order가 큰 것부터 접는다(Core/DockLayout). 훈 원 우클릭 메뉴 이벤트를 밖으로 알린다. 커서 위치를 60ms마다 런처에 넣는다(깨우기).
// 사용법: var d = new DockWindow(Places.EdgeRight, launcherSettings); d.Show(); d.SetModules(list); d.Relayout();  d.ZoneRect() → 훈 원 자리(측정용)
// 의존: Core/Layout(DockLayout, Places, LauncherLayout), Ui/Frame/LauncherView, Shell/ModuleView, Native/User32. 창은 활성화되지 않는다(엑셀 포커스를 뺏지 않음).
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Core.Settings;
using ClockOut.Hunsu.Native;
using ClockOut.Hunsu.Ui.Frame;
using ClockOut.Hunsu.Shell;

namespace ClockOut.Hunsu.Overlay;

public partial class DockWindow : Window
{
    private readonly List<ModuleView> _modules = [];          // 이 창이 패널을 두는 모듈(오른쪽 창)
    private readonly List<ModuleView> _launcherModules = [];  // 이 창 런처에 아이콘이 나오는 모듈
    private HunsuSettings _settings;
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
        Launcher.HubDragMoved += OnHubDragMoved;
        Launcher.HubDragEnded += OnHubDragEnded;
        Configure();
        Resize();
        Launcher.AwakeChanged += awake => Slide(awake, animate: true);
        Launcher.SizeChanged += (_, _) => Slide(Launcher.Awake, animate: false);
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

    /// <summary>패널을 두는 창인가(기본: 오른쪽 창). 왼쪽 창은 런처만 둔다.</summary>
    private bool HostsPanels => Side == Places.EdgeRight;

    /// <summary>런처를 그리는 창인가: 왼쪽은 늘, 오른쪽은 그쪽에 아이콘을 둔 모듈이 있을 때만.</summary>
    private bool HostsLauncher => Side == Places.EdgeLeft || _launcherModules.Count > 0;

    /// <summary>패널 쪽 일이 바뀌어(넘쳐서 접힘) 다른 창의 런처도 다시 그려야 한다.</summary>
    internal event Action? LauncherRefreshNeeded;

    /// <summary>왼쪽 창 런처의 반쯤 숨김: 잠들면 훈 원 중심이 창 왼쪽 가장자리에 오게 밀고(잘려서 반만 보임), 깨어나면 튀어나온다.</summary>
    private void Slide(bool awake, bool animate)
    {
        if (Side != Places.EdgeLeft) return;
        var r = Launcher.HubBounds;
        double hidden = r.IsEmpty ? 0 : r.X + r.Width / 2;
        double to = awake ? 0 : -hidden;
        if (LauncherHost.RenderTransform is not TranslateTransform tr || tr.IsFrozen) LauncherHost.RenderTransform = tr = new TranslateTransform();
        if (!animate) { tr.BeginAnimation(TranslateTransform.XProperty, null); tr.X = to; return; }
        tr.BeginAnimation(TranslateTransform.XProperty, new System.Windows.Media.Animation.DoubleAnimation(to, TimeSpan.FromMilliseconds(awake ? 160 : 220)) { EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = awake ? System.Windows.Media.Animation.EasingMode.EaseOut : System.Windows.Media.Animation.EasingMode.EaseIn } });
    }

    public string Side { get; }
    public IntPtr Handle { get; private set; }

    /// <summary>측정·문제 찾기용: 넘침 계산 입력과 결과 한 줄.</summary>
    public event Action<string>? LayoutTraced;
    public event Action? ToggleAllRequested;
    public event Action? SummonRequested;
    public event Action? HideAllRequested;
    /// <summary>모듈이 선언한 켜고 끄기 항목(이름, 단축키 글, 동작). 허브 우클릭 메뉴가 열릴 때마다 새로 묻는다(ShellController가 넣는다).</summary>
    internal Func<IReadOnlyList<(string Label, string Gesture, Action Act)>>? HubExtras { get; set; }
    public event Action? QuitRequested;
    public event Action? JoinToggleRequested;

    /// <summary>허브 메뉴에서 창 밖 사이드바를 골랐다("off", "left", "right").</summary>
    public event Action<string>? SidebarRequested;
    public event Action? ManagerRequested;
    public event Action? LookRequested;
    public event Action? ExcelCleanupRequested;

    /// <summary>편집 모드에서 훈 원을 놓았다: (새 세로 위치 비율, 놓은 곳의 화면 가로 좌표).</summary>
    public event Action<double, int>? HubDropped;

    private double _centerRatio = Core.Layout.LayoutState.DefaultDockRatio;
    private double _dragStartY;
    private bool _dragStarted;
    private double _dragRatio;

    /// <summary>훈 원의 세로 위치(0~1). 편집 모드에서 끌어 바꾼 값이다.</summary>
    public void SetCenterRatio(double ratio) { _centerRatio = ratio; Launcher.CenterRatio = ratio; }

    /// <summary>편집 모드: 훈 원을 끌어 옮길 수 있게 한다.</summary>
    public void SetEditMode(bool on) => Launcher.EditMode = on;

    private void OnHubDragMoved()
    {
        if (!User32.GetCursorPos(out var pt)) return;
        double y = PointFromScreen(new Point(pt.X, pt.Y)).Y;
        if (!_dragStarted) { _dragStarted = true; _dragStartY = y; }
        double h = Math.Max(1, Launcher.ActualHeight);
        _dragRatio = Math.Clamp(_centerRatio + (y - _dragStartY) / h, 0.1, 0.9);
        Launcher.SetDragOffset((_dragRatio - _centerRatio) * h);
    }

    private void OnHubDragEnded()
    {
        if (!_dragStarted) return;
        _dragStarted = false;
        Launcher.SetDragOffset(0);
        if (User32.GetCursorPos(out var pt)) HubDropped?.Invoke(_dragRatio, pt.X);
        else HubDropped?.Invoke(_dragRatio, int.MinValue);
    }

    /// <summary>런처의 모듈 아이콘을 눌렀다(모듈 id): 패널 열기·닫기.</summary>
    public event Action<string>? ModuleIconClicked;

    /// <summary>모듈 아이콘 우클릭 메뉴를 만드는 곳(ShellController가 넣는다).</summary>
    internal Func<string, ContextMenu?>? ModuleMenu { get => Launcher.ModuleMenu; set => Launcher.ModuleMenu = value; }

    internal IReadOnlyList<ModuleView> Modules => _modules;

    /// <summary>panels = 이 창에 패널을 놓을 모듈(왼쪽 창은 없음), launcher = 이 창 런처에 아이콘을 둘 모듈.</summary>
    internal void SetModules(IEnumerable<ModuleView> panels, IEnumerable<ModuleView> launcher)
    {
        _modules.Clear();
        _modules.AddRange(HostsPanels ? panels.OrderBy(m => m.Placement.Order) : []);
        _launcherModules.Clear();
        _launcherModules.AddRange(launcher.OrderBy(m => m.Placement.Order));
        Items.Children.Clear();
        foreach (var m in _modules)
        {
            if (m.Slot.Parent is Panel p) p.Children.Remove(m.Slot);
            m.UseTab = false; // 접힌 모듈은 손잡이 대신 런처 아이콘으로만 남는다
            double g = (double)FindResource("Hunsu.Gap");
            m.Slot.Margin = new Thickness(0, 0, 0, g);
            ((FrameworkElement)m.Slot).HorizontalAlignment = Column.HorizontalAlignment;
            m.Tab.HorizontalAlignment = Column.HorizontalAlignment;
            m.Frame.Width = (double)FindResource("Hunsu.PanelWidth");
            m.Tab.Visibility = Visibility.Collapsed;
            Items.Children.Add(m.Slot);
        }
        Resize();
        RefreshLauncher();
        Relayout();
        ApplyJoin();
    }

    /// <summary>합쳐 보기(panelJoin): 보이는 패널들을 틈 없이 이어 붙이고 이웃과 닿는 선은 하나로 겹친다(얇은 칸막이 선). 끄면 따로 뜬 카드.</summary>
    private void ApplyJoin()
    {
        double gap = (double)FindResource("Hunsu.Gap");
        var shown = _modules.Where(m => m.ShowsPanel).ToList();
        foreach (var m in _modules)
        {
            int i = shown.IndexOf(m);
            if (!_settings.PanelJoin || i < 0)
            {
                m.Slot.Margin = new Thickness(0, 0, 0, gap);
                m.Frame.SetJoin(JoinPos.None);
                continue;
            }
            m.Slot.Margin = new Thickness(0, i == 0 ? 0 : -1, 0, 0); // 선 한 줄만 겹쳐 칸막이로
            m.Frame.SetJoin(shown.Count == 1 ? JoinPos.Only : i == 0 ? JoinPos.First : i == shown.Count - 1 ? JoinPos.Last : JoinPos.Middle);
        }
    }

    /// <summary>설정 창 [모양]에서 바꾼 모양을 바로 쓴다(창 너비와 패널 자리도 다시 잰다).</summary>
    internal void ApplyLook(HunsuSettings settings)
    {
        _settings = settings;
        Configure();
        Resize();
        Relayout();
        ApplyJoin();
    }

    private void Configure() =>
        Launcher.Configure(_settings.DockShape, _settings.DockGroups, LauncherShapes.Scale(_settings.DockSize), _settings.IdleOpacity, _settings.IconProfile, mirror: Side == Places.EdgeLeft);

    /// <summary>런처 아이콘(이름, 아이콘, 배지, 열림)을 지금 모듈 상태로 다시 넣는다.</summary>
    /// <summary>줄어든 모듈이 일하는 중인지 묻는 함수(셸이 넣는다). 아이콘에 숨 쉬는 점을 그린다.</summary>
    internal Func<ModuleView, bool>? IsWorking { get; set; }

    internal void RefreshLauncher() =>
        Launcher.SetModules(_launcherModules.Select(m => new LauncherModule(m.Id, m.Module.Manifest.Name, m.Placement.Order, m.Module.Manifest.Icon, m.Badge, m.Tone, m.ShowsPanel, IsWorking?.Invoke(m) ?? false)).ToList());

    /// <summary>창 너비 = 패널 너비 + 런처 폭 + 그림자 여백. 패널 칸은 런처 폭만큼 가장자리에서 떨어진다.</summary>
    private void Resize()
    {
        var shadow = (Thickness)FindResource("Hunsu.ShadowMargin");
        bool launcher = HostsLauncher;
        Launcher.Visibility = launcher ? Visibility.Visible : Visibility.Collapsed;
        double reserved = launcher ? LauncherShapes.ReservedWidth(_settings.DockShape, Math.Max(_launcherModules.Count, 5), LauncherShapes.Scale(_settings.DockSize), _settings.DockGroups) : 0;
        // 왼쪽 창 = 런처 폭만, 오른쪽 창 = 패널 폭(+ 그쪽에 아이콘을 둔 모듈이 있을 때만 런처 폭). 바탕이 투명이라 비어 있어도 화면에는 영향이 없다.
        Width = (HostsPanels ? (double)FindResource("Hunsu.PanelWidth") : 0) + reserved + shadow.Left + shadow.Right;
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
        // 패널은 한 줄로 쌓고, 높이가 모자라면 앞쪽부터 접는다.
        var plan = DockLayout.Plan(entries, available, gap);
        LayoutTraced?.Invoke($"available={available:0} " + string.Join(";", entries.Select(e => $"{e.Id}:{e.PanelHeight:0}")) + " forced=" + string.Join(",", plan.Where(p => p.ForcedByOverflow).Select(p => p.Id)));
        bool changed = false;
        foreach (var slot in plan)
        {
            var m = _modules.First(x => x.Id == slot.Id);
            changed |= m.ForcedCollapsed != slot.ForcedByOverflow;
            m.SetForcedCollapsed(slot.ForcedByOverflow);
        }
        if (changed) LauncherRefreshNeeded?.Invoke();
        ApplyJoin(); // 접힘이 바뀌면 이어 붙이는 순서도 바뀐다
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
        var extras = new List<MenuItem>();
        menu.Opened += (_, _) =>
        {
            foreach (var x in extras) menu.Items.Remove(x);
            extras.Clear();
            int at = menu.Items.IndexOf(_toggleAll) + 2;
            foreach (var (label, gesture, act) in HubExtras?.Invoke() ?? [])
            {
                var mi = Item(label, gesture, act);
                extras.Add(mi);
                menu.Items.Insert(Math.Min(at++, menu.Items.Count), mi);
            }
        };
        var join = Item("패널 한 창처럼 붙여 보기", "", () => JoinToggleRequested?.Invoke());
        menu.Opened += (_, _) => join.IsChecked = _settings.PanelJoin;
        menu.Items.Add(join);
        var side = new MenuItem { Header = "사이드바 (엑셀 창 밖)" };
        foreach (var (key, label) in new[] { ("off", "끄기"), ("left", "왼쪽"), ("right", "오른쪽") })
        {
            var mi = new MenuItem { Header = label, Tag = key, IsCheckable = false };
            mi.Click += (_, _) => SidebarRequested?.Invoke(key);
            side.Items.Add(mi);
        }
        menu.Opened += (_, _) => { foreach (MenuItem mi in side.Items) mi.IsChecked = (string?)mi.Tag == _settings.Sidebar; };
        menu.Items.Add(side);
        menu.Items.Add(Item("전부 숨기기", "Ctrl+Alt+Shift+H", () => HideAllRequested?.Invoke()));
        menu.Items.Add(Item("모듈 관리…", "", () => ManagerRequested?.Invoke()));
        menu.Items.Add(Item("모양 바꾸기…", "", () => LookRequested?.Invoke()));
        menu.Items.Add(Item("남은 엑셀 정리…", "", () => ExcelCleanupRequested?.Invoke()));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("훈수꾼 끄기", "", () => QuitRequested?.Invoke()));
        return menu;
    }
}
