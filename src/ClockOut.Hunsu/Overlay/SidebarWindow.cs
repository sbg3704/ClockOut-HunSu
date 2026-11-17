// 기능: 창 밖 사이드바(VS Code·비주얼 스튜디오·오토캐드의 속성 패널처럼). 윈도우 앱 바로 화면 왼쪽/오른쪽 가장자리를 예약해 엑셀(최대화일 때)이 남은 영역으로 저절로 줄어든다. 안쪽: 아이콘 막대(활동 막대) + 모듈 구역들(위에서 아래, 구역마다 ▾로 접기)·세로 스크롤.
//       안쪽 가장자리를 끌어 폭을 바꾼다(저장은 WidthChanged로 앱이). 끄면 예약을 풀어 엑셀이 원래 크기로 돌아온다.
// 사용법: var s = new SidebarWindow(); s.Configure("right", 300); s.SetActive(true); s.SetModules(panelModules, iconModules); s.ModuleClicked += id => ...; s.WidthChanged += w => 저장;
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

internal sealed class SidebarWindow : Window
{
    private const double BarWidth = 40, GripWidth = 6;
    private readonly Grid _root = new();
    private readonly StackPanel _bar = new();
    private readonly StackPanel _items = new();
    private readonly ScrollViewer _scroll = new();
    private readonly Thumb _grip = new();
    private readonly uint _callbackMsg = AppBar.RegisterWindowMessage("ClockOutHunsuSidebar");
    private HwndSource? _source;
    private bool _registered, _left;
    private int _widthDip = 300;
    private IntPtr _anchor; // 어느 모니터에 붙일지 정하는 엑셀 창

    public SidebarWindow()
    {
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
        _grip.Width = GripWidth;
        _grip.Cursor = System.Windows.Input.Cursors.SizeWE;
        _grip.Opacity = 0.01; // 보이지 않지만 잡힌다
        _grip.Background = Brushes.Gray;
        _grip.Template = new ControlTemplate(typeof(Thumb)) { VisualTree = Border(Brushes.Transparent) };
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
    public bool Reserved => _registered;

    /// <summary>폭을 끌어 바꾼 뒤(DIP).</summary>
    public event Action<int>? WidthChanged;

    /// <summary>활동 막대의 아이콘을 눌렀다(모듈 id).</summary>
    public event Action<string>? ModuleClicked;

    private static FrameworkElementFactory Border(Brush b)
    {
        var f = new FrameworkElementFactory(typeof(System.Windows.Controls.Border));
        f.SetValue(System.Windows.Controls.Border.BackgroundProperty, b);
        return f;
    }

    private double Scale() => PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;

    /// <summary>왼쪽/오른쪽과 폭(DIP). 이미 켜져 있으면 바로 다시 자리를 잡는다.</summary>
    public void Configure(string side, int widthDip)
    {
        bool left = side == "left";
        bool sideChanged = left != _left;
        _left = left;
        _widthDip = Math.Clamp(widthDip, 200, 700);
        if (sideChanged) Build();
        if (_registered) Reposition();
    }

    /// <summary>안쪽 배치: [활동 막대 | 구역 목록 | 끄는 손잡이] (왼쪽 사이드바), 오른쪽 사이드바는 손잡이가 맨 왼쪽.</summary>
    private void Build()
    {
        _root.Children.Clear();
        _root.ColumnDefinitions.Clear();
        UIElement[] order = _left ? [_bar, _scroll, _grip] : [_grip, _scroll, _bar];
        double[] widths = _left ? [BarWidth, -1, GripWidth] : [GripWidth, -1, BarWidth];
        for (int i = 0; i < 3; i++)
        {
            _root.ColumnDefinitions.Add(new ColumnDefinition { Width = widths[i] < 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(widths[i]) });
            Grid.SetColumn(order[i], i);
            _root.Children.Add(order[i]);
        }
    }

    /// <summary>엑셀이 있을 때만 켠다: 앱 바 등록(가장자리 예약)과 보이기. 끄면 예약을 풀어 엑셀 창이 원래 크기로 돌아온다. anchor = 붙일 모니터를 정하는 엑셀 창.</summary>
    public void SetActive(bool on, IntPtr anchor = default)
    {
        if (anchor != IntPtr.Zero) _anchor = anchor;
        if (on == _registered)
        {
            if (on) Reposition();
            return;
        }
        if (on)
        {
            if (!IsVisible) { Show(); } // 핸들을 만든다
            if (Handle == IntPtr.Zero) return;
            _registered = AppBar.Register(Handle, _callbackMsg);
            if (_registered) Reposition();
            else Hide(); // 앱 바를 못 쓰면(드문 일) 숨어 있는다
        }
        else
        {
            if (Handle != IntPtr.Zero) AppBar.Remove(Handle);
            _registered = false;
            Hide();
        }
    }

    private void Reposition()
    {
        if (!_registered || Handle == IntPtr.Zero) return;
        var screen = _anchor != IntPtr.Zero ? System.Windows.Forms.Screen.FromHandle(_anchor) : System.Windows.Forms.Screen.PrimaryScreen ?? System.Windows.Forms.Screen.AllScreens[0];
        int px = (int)Math.Round(_widthDip * Scale());
        var rc = AppBar.Place(Handle, _left, screen.Bounds, screen.WorkingArea, px);
        User32.SetWindowPos(Handle, User32.HWND_TOP, rc.Left, rc.Top, rc.Right - rc.Left, rc.Bottom - rc.Top, User32.SWP_NOACTIVATE | User32.SWP_SHOWWINDOW);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if ((uint)msg == _callbackMsg && wParam.ToInt32() == AppBar.ABN_POSCHANGED) Reposition();
        return IntPtr.Zero;
    }

    /// <summary>모듈 구역(panels)과 활동 막대 아이콘(icons)을 지금 상태로 다시 넣는다. 접힌 모듈은 구역을 감추고 아이콘만 남는다.</summary>
    internal void SetModules(IEnumerable<ModuleView> panels, string profile)
    {
        var list = panels.OrderBy(m => m.Placement.Order).ToList();
        _items.Children.Clear();
        _bar.Children.Clear();
        double gap = (double)Application.Current.FindResource("Hunsu.Gap");
        foreach (var m in list)
        {
            if (m.Slot.Parent is Panel p) p.Children.Remove(m.Slot);
            m.UseTab = false;
            m.Slot.Margin = new Thickness(0, 0, 0, gap);
            ((FrameworkElement)m.Slot).HorizontalAlignment = HorizontalAlignment.Stretch;
            m.Frame.Width = double.NaN; // 사이드바 폭에 맞춘다
            m.Frame.ApplyBodyLimit(true); // 높이는 사이드바 스크롤이 맡는다
            m.Tab.Visibility = Visibility.Collapsed;
            _items.Children.Add(m.Slot);
            _bar.Children.Add(ActivityIcon(m, profile));
        }
    }

    private UIElement ActivityIcon(ModuleView m, string profile)
    {
        bool open = m.ShowsPanel;
        var brush = (Brush)Application.Current.FindResource(open ? "Hunsu.AccentInk" : ThemeKeys.Text);
        var glyph = IconFactory.Create(profile, m.Module.Manifest.Icon, 18, brush, m.Module.Manifest.Name);
        var b = new System.Windows.Controls.Border
        {
            Width = 32, Height = 32, Margin = new Thickness(4, 2, 4, 2), CornerRadius = new CornerRadius(6), Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = m.Module.Manifest.Name + (open ? " (누르면 접기)" : " (누르면 펴기)"),
            Child = glyph,
        };
        b.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, open ? "Hunsu.Accent" : "Hunsu.GlassAlt");
        string id = m.Id;
        b.MouseLeftButtonUp += (_, _) => ModuleClicked?.Invoke(id);
        return b;
    }

    /// <summary>앱 바를 풀고 닫는다(프로그램 끝).</summary>
    public void Shutdown()
    {
        if (_registered && Handle != IntPtr.Zero) AppBar.Remove(Handle);
        _registered = false;
        Close();
    }
}
