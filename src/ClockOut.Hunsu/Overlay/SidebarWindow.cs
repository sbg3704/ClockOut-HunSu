// 기능: 창 밖 사이드바(VS Code·비주얼 스튜디오·오토캐드의 속성 패널처럼). 이 파일은 창·앱 바·폭 조절·하단 단추, 묶음 탭과 스플리터는 SidebarWindow.Tabs.cs, 끌어 옮기기는 SidebarWindow.Drag.cs. 엑셀이 최대화면 윈도우 앱 바로 화면 가장자리를 예약해 엑셀이 남은 영역으로 줄어든다. 최대화가 아니면 앱 바를 쓰지 않고 엑셀 창의 높이에 맞춰 옆구리(바깥)에 붙어 창을 따라 움직이고 크기를 맞춘다(바깥 자리가 모자라면 안쪽 가장자리). 안쪽: 활동 막대(묶음 탭 아이콘) + 고른 묶음의 모듈들(위에서 아래, 사이 스플리터로 높이 조절)·세로 스크롤.
//       안쪽 가장자리를 끌어 폭을 바꾼다(저장은 WidthChanged로 앱이). 끄면 예약을 풀어 엑셀이 원래 크기로 돌아온다.
// 사용법: var s = new SidebarWindow(); s.Configure("right", 300); s.SetActive(true); s.SetModules(views, profile, tabs, id => 높이); s.TabsChanged += tabs => 저장; s.HeightChanged += (id, h) => 저장; s.WidthChanged += w => 저장;
// 의존: Native/AppBar, Native/User32, Ui/Frame/PanelFrame(ModuleView의 Slot), Ui/Icons/IconFactory, Theme 이름. 화면(WPF) 일이라 시험은 못 하고 설정 값 정리만 시험한다. 엑셀 창을 직접 옮기지 않는다(앱 바가 일을 한다).
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using ClockOut.Hunsu.Native;
using ClockOut.Hunsu.Shell;
using ClockOut.Hunsu.Ui.Icons;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Overlay;

internal sealed partial class SidebarWindow : Window
{
    private const double BarWidth = 40, GripWidth = 6;
    private readonly Grid _root = new();
    private readonly StackPanel _bar = new();
    private readonly StackPanel _footer = new();
    private readonly DockPanel _barHost = new();
    private bool _tabs = true;
    private string? _placedKey; // 지금 앱 바로 예약한 자리(같으면 다시 예약하지 않음)
    private bool _stay; // 자리 고정: 엑셀 창을 바꿔도 처음 붙은 창 옆에 그대로(내용만 지금 창)
    private bool _collapsed; // 접힘: 활동 막대만 남기고 폭을 줄인다(엑셀이 그만큼 넓어짐)
    private readonly StackPanel _items = new();
    private readonly ScrollViewer _scroll = new();
    private readonly Thumb _grip = new();
    private readonly uint _callbackMsg = AppBar.RegisterWindowMessage("ClockOutHunsuSidebar");
    private HwndSource? _source;
    private bool _active, _appBar, _left;
    private readonly User32.WinEventProc _proc;
    private readonly List<IntPtr> _hooks = [];
    private IntPtr _hooked;
    private int _widthDip = 300;
    private IntPtr _anchor; // 어느 모니터에 붙일지 정하는 엑셀 창

    public SidebarWindow()
    {
        _proc = OnWinEvent;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Title = "훈수꾼 사이드바";
        FontFamily = (FontFamily)Application.Current.FindResource("Hunsu.Font");
        FontSize = (double)Application.Current.FindResource("Hunsu.FontBody");
        SetResourceReference(BackgroundProperty, "Hunsu.Surface");
        _scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _scroll.Focusable = false;
        _scroll.Content = _items;
        _items.Margin = new Thickness(4);
        _bar.Margin = new Thickness(0, 6, 0, 6);
        _footer.Margin = new Thickness(0, 4, 0, 6);
        DockPanel.SetDock(_footer, Dock.Bottom);
        _barHost.LastChildFill = true;
        _barHost.Children.Add(_footer);
        _barHost.Children.Add(_bar);
        _grip.Width = GripWidth;
        _grip.Cursor = System.Windows.Input.Cursors.SizeWE;
        _grip.Opacity = 0.01; // 보이지 않지만 잡힌다
        _grip.Background = Brushes.Gray;
        _grip.Template = new ControlTemplate(typeof(Thumb)) { VisualTree = BorderFactory(Brushes.Transparent) };
        _grip.DragDelta += (_, e) =>
        {
            // 안쪽 가장자리를 끈다: 오른쪽 사이드바는 왼쪽으로 끌면 넓어지고, 왼쪽 사이드바는 오른쪽으로 끌면 넓어진다.
            int next = Math.Clamp((int)Math.Round(_widthDip + (_left ? e.HorizontalChange : -e.HorizontalChange)), 200, 700);
            if (next == _widthDip) return;
            _widthDip = next;
            Reposition();
        };
        _grip.DragCompleted += (_, _) => WidthChanged?.Invoke(_widthDip);
        Content = _root;
        InitDrag();
        Build();
        SourceInitialized += (_, _) =>
        {
            Handle = new WindowInteropHelper(this).Handle;
            long ex = User32.GetWindowLongPtr(Handle, User32.GWL_EXSTYLE).ToInt64();
            // 엑셀의 초점을 빼앗지 않는다(수식 입력 중에도 사이드바를 눌러 볼 수 있게).
            User32.SetWindowLongPtr(Handle, User32.GWL_EXSTYLE, new IntPtr(ex | User32.WS_EX_NOACTIVATE | User32.WS_EX_TOOLWINDOW));
            _source = HwndSource.FromHwnd(Handle);
            _source?.AddHook(WndProc);
        };
    }

    public IntPtr Handle { get; private set; }
    public bool Reserved => _active;

    /// <summary>폭을 끌어 바꾼 뒤(DIP).</summary>
    public event Action<int>? WidthChanged;

    /// <summary>활동 막대 맨 아래 일반 단추를 눌렀다("settings", "modules", "look", "profiles", "view", "cleanup", "fold"). 접힌 채 묶음 아이콘을 눌러도 "fold"(펴기).</summary>

    /// <summary>지금 접혀 있는가(활동 막대만).</summary>
    public bool Collapsed => _collapsed;
    public event Action<string>? FooterClicked;

    /// <summary>접힌 모듈을 펴 달라(모듈 id). 묶음 탭 안의 접힌 모듈 이름을 누르거나, 모두 접힌 탭을 고르면.</summary>
    public event Action<string>? ModuleClicked;

    private static FrameworkElementFactory BorderFactory(Brush b)
    {
        var f = new FrameworkElementFactory(typeof(System.Windows.Controls.Border));
        f.SetValue(System.Windows.Controls.Border.BackgroundProperty, b);
        return f;
    }

    private double Scale() => PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;

    /// <summary>왼쪽/오른쪽과 폭(DIP). 이미 켜져 있으면 바로 다시 자리를 잡는다.</summary>
    public void Configure(string side, int widthDip, bool tabs = true, bool collapsed = false, bool stay = false)
    {
        if (stay != _stay) { _stay = stay; BuildFooter(); }
        _tabs = tabs;
        bool left = side == "left";
        bool changed = left != _left || collapsed != _collapsed;
        _left = left;
        _collapsed = collapsed;
        _widthDip = Math.Clamp(widthDip, 200, 700);
        if (changed)
        {
            Build();
            BuildFooter(); // 접기 단추 모양이 바뀐다
        }
        if (_active) Reposition();
    }

    /// <summary>안쪽 배치: [활동 막대 | 구역 목록 | 끄는 손잡이] (왼쪽 사이드바), 오른쪽 사이드바는 손잡이가 맨 왼쪽.</summary>
    private void Build()
    {
        _root.Children.Clear();
        _root.ColumnDefinitions.Clear();
        UIElement[] order = _left ? [_barHost, _scroll, _grip] : [_grip, _scroll, _barHost];
        double grip = _collapsed ? 0 : GripWidth, rest = _collapsed ? 0 : -1;
        double[] widths = _left ? [BarWidth, rest, grip] : [grip, rest, BarWidth];
        _scroll.Visibility = _grip.Visibility = _collapsed ? Visibility.Collapsed : Visibility.Visible;
        for (int i = 0; i < 3; i++)
        {
            _root.ColumnDefinitions.Add(new ColumnDefinition { Width = widths[i] < 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(widths[i]) });
            Grid.SetColumn(order[i], i);
            _root.Children.Add(order[i]);
        }
        Grid.SetColumnSpan(_overlay, 3); // 끌어 옮길 때 따라다니는 이름표
        _root.Children.Add(_overlay);
    }

    /// <summary>엑셀이 있을 때만 켠다: 보이기, 최대화면 앱 바 등록(가장자리 예약), 아니면 엑셀 옆에 붙이기. 끄면 예약을 풀어 엑셀 창이 원래 크기로 돌아온다. anchor = 붙을 엑셀 창.</summary>
    public void SetActive(bool on, IntPtr anchor = default)
    {
        // 자리 고정이면 붙어 있던 엑셀 창이 살아 있고 보이는 동안 다른 창으로 옮기지 않는다(두 창을 나란히 두고 비교할 때).
        bool keep = _stay && _anchor != IntPtr.Zero && User32.IsWindow(_anchor) && !User32.IsIconic(_anchor);
        if (anchor != IntPtr.Zero && anchor != _anchor && !keep)
        {
            _anchor = anchor;
            Unhook();
        }
        if (on == _active)
        {
            if (on) Reposition();
            return;
        }
        if (on)
        {
            if (!IsVisible) { Show(); } // 핸들을 만든다
            if (Handle == IntPtr.Zero) return;
            _active = true;
            Hook();
            Reposition();
        }
        else
        {
            if (_appBar && Handle != IntPtr.Zero) AppBar.Remove(Handle);
            _appBar = false;
            _placedKey = null;
            _active = false;
            Unhook();
            if (Handle != IntPtr.Zero) User32.SetWindowLongPtr(Handle, User32.GWLP_HWNDPARENT, IntPtr.Zero);
            Hide();
        }
    }

    private bool Maximized => _anchor == IntPtr.Zero || User32.IsZoomed(_anchor);

    private void Reposition()
    {
        if (!_active || Handle == IntPtr.Zero) return;
        var screen = _anchor != IntPtr.Zero ? System.Windows.Forms.Screen.FromHandle(_anchor) : System.Windows.Forms.Screen.PrimaryScreen ?? System.Windows.Forms.Screen.AllScreens[0];
        int px = (int)Math.Round((_collapsed ? BarWidth : _widthDip) * Scale());
        if (Maximized)
        {
            // 최대화: 앱 바로 가장자리를 예약한다(엑셀이 남은 영역으로 줄어든다).
            if (!_appBar)
            {
                User32.SetWindowLongPtr(Handle, User32.GWLP_HWNDPARENT, IntPtr.Zero);
                _appBar = AppBar.Register(Handle, _callbackMsg);
                if (!_appBar) { _active = false; Unhook(); Hide(); return; } // 앱 바를 못 쓰면(드문 일) 숨어 있는다
            }
            // 같은 화면·같은 폭·같은 쪽으로 이미 예약했으면 다시 예약하지 않는다(예약할 때마다 최대화된 엑셀들이 줄었다 늘었다 한다, 2026-11-17 사용자 지적).
            string key = $"{screen.DeviceName}|{screen.Bounds}|{px}|{_left}";
            if (key == _placedKey) return;
            _placedKey = key;
            var rc = AppBar.Place(Handle, _left, screen.Bounds, screen.WorkingArea, px);
            User32.SetWindowPos(Handle, User32.HWND_TOP, rc.Left, rc.Top, rc.Right - rc.Left, rc.Bottom - rc.Top, User32.SWP_NOACTIVATE | User32.SWP_SHOWWINDOW);
            return;
        }
        // 최대화가 아님: 앱 바를 풀고(엑셀은 제 크기 그대로), 엑셀 창의 높이에 맞춰 옆에 붙인다. 엑셀의 소유 창이라 늘 엑셀 위에 있고 엑셀이 최소화되면 함께 숨는다.
        if (_appBar)
        {
            AppBar.Remove(Handle);
            _appBar = false;
            _placedKey = null;
        }
        if (User32.IsIconic(_anchor)) return;
        User32.SetWindowLongPtr(Handle, User32.GWLP_HWNDPARENT, _anchor);
        var ex = User32.VisibleBounds(_anchor);
        if (ex.IsEmpty) return;
        var work = screen.WorkingArea;
        int x = _left ? ex.X - px : ex.Right;
        if (_left && x < work.Left) x = ex.X; // 바깥 자리가 없으면 엑셀 안쪽 가장자리에 겹친다
        if (!_left && x + px > work.Right) x = ex.Right - px;
        User32.SetWindowPos(Handle, User32.HWND_TOP, x, ex.Y, px, ex.Height, User32.SWP_NOACTIVATE | User32.SWP_SHOWWINDOW);
    }

    private void Hook()
    {
        if (_anchor == IntPtr.Zero || _hooked == _anchor) return;
        Unhook();
        uint tid = User32.GetWindowThreadProcessId(_anchor, out uint pid);
        const uint flags = User32.WINEVENT_OUTOFCONTEXT | User32.WINEVENT_SKIPOWNPROCESS;
        foreach (var (lo, hi) in new[] { (User32.EVENT_OBJECT_LOCATIONCHANGE, User32.EVENT_OBJECT_LOCATIONCHANGE), (User32.EVENT_SYSTEM_MINIMIZESTART, User32.EVENT_SYSTEM_MINIMIZEEND) })
        {
            var h = User32.SetWinEventHook(lo, hi, IntPtr.Zero, _proc, pid, tid, flags);
            if (h != IntPtr.Zero) _hooks.Add(h);
        }
        _hooked = _anchor;
    }

    private void Unhook()
    {
        foreach (var h in _hooks) User32.UnhookWinEvent(h);
        _hooks.Clear();
        _hooked = IntPtr.Zero;
    }

    /// <summary>엑셀 창이 움직이거나 크기가 바뀌거나 최소화·복원되면 따라간다. 최대화 상태에서는 앱 바가 이미 일하므로 건드리지 않는다(다시 예약하면 엑셀 크기가 흔들린다).</summary>
    private void OnWinEvent(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        try { OnWinEventCore(evt, hwnd, idObject, idChild); }
        catch (Exception ex) { Diagnostics.ErrorLog.Caught("Sidebar.WinEvent", ex); }
    }

    private void OnWinEventCore(uint evt, IntPtr hwnd, int idObject, int idChild)
    {
        if (!_active || hwnd != _anchor || idObject != User32.OBJID_WINDOW || idChild != 0) return;
        if (Maximized && _appBar && evt == User32.EVENT_OBJECT_LOCATIONCHANGE) return;
        Reposition();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if ((uint)msg == _callbackMsg && wParam.ToInt32() == AppBar.ABN_POSCHANGED && _appBar) { _placedKey = null; Reposition(); } // 작업 표시줄 등이 바뀌면 다시 예약
        return IntPtr.Zero;
    }

    /// <summary>활동 막대 맨 아래(VS Code의 톱니·계정 자리)에 일반 단추들.</summary>
    private void BuildFooter()
    {
        _footer.Children.Clear();
        (string Key, string Glyph, string Tip)[] items =
        [
            ("settings", "⚙", "설정 창"),
            ("modules", "☰", "모듈 관리"),
            ("look", "✎", "모양 바꾸기"),
            ("profiles", "▣", "작업 프로필"),
            ("view", _tabs ? "≣" : "▤", _tabs ? "쌓아 보기로 바꾸기" : "탭 보기로 바꾸기"),
            ("link", "🔗", "엑셀 연결 (직접 고르기)"),
            ("cleanup", "🧹", "남은 엑셀 정리"),
            // 끄기 대신 접기·펴기(2026-11-17 사용자 요청). 끄기는 설정 창·훈 원 메뉴에 남아 있다.
            ("stay", _stay ? "📌" : "📍", _stay ? "자리 고정 중: 엑셀 창을 바꿔도 여기 그대로 (누르면 따라가기)" : "따라가는 중: 앞에 나온 엑셀 창 옆으로 옮김 (누르면 자리 고정)"),
            ("fold", _collapsed == _left ? "»" : "«", _collapsed ? "사이드바 펴기" : "사이드바 접기 (아이콘 막대만)"),
        ];
        foreach (var (key, glyph, tip) in items)
        {
            var t = new TextBlock { Text = glyph, FontSize = 15, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            t.SetResourceReference(TextBlock.ForegroundProperty, ThemeKeys.Text);
            var b = new System.Windows.Controls.Border { Width = 32, Height = 28, Margin = new Thickness(4, 1, 4, 1), CornerRadius = new CornerRadius(6), Cursor = System.Windows.Input.Cursors.Hand, ToolTip = tip, Background = Brushes.Transparent, Child = t };
            b.MouseEnter += (_, _) => b.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "Hunsu.GlassAlt");
            b.MouseLeave += (_, _) => b.Background = Brushes.Transparent;
            string k = key;
            b.MouseLeftButtonUp += (_, _) => FooterClicked?.Invoke(k);
            _footer.Children.Add(b);
        }
    }

    /// <summary>앱 바를 풀고 닫는다(프로그램 끝).</summary>
    public void Shutdown()
    {
        if (_appBar && Handle != IntPtr.Zero) AppBar.Remove(Handle);
        _appBar = false;
        _placedKey = null;
        _active = false;
        Unhook();
        Close();
    }
}
