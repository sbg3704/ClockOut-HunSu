// 기능: 빠른 띠. 프로필의 주요 모듈 아이콘을 한 줄로 엑셀 위에 띄워 두고 눌러서 패널을 열고 닫는다(와우 쿨다운 관리자처럼). 편집 모드에서는 끌어 옮긴다.
// 사용법: var q = new QuickBar(iconProfile); q.ItemClicked += id => ...; q.Moved += (rx, ry) => 저장; q.SetItems(items); q.EditMode = true/false.
//   자리는 WindowFollower가 엑셀 창 기준 비율(rx, ry)로 잡는다. 엑셀 포커스를 뺏지 않는다.
// 의존: Ui/Icons/IconFactory, Native/User32, Core/PixelRect, Ui Theme. 값은 Theme 이름으로만 쓴다.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Native;
using ClockOut.Hunsu.Ui.Frame;
using ClockOut.Hunsu.Ui.Icons;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Overlay;

/// <summary>빠른 띠 칸 하나: 모듈 id, 이름(툴팁), 아이콘 뜻 이름, 지금 패널이 열려 있는가.</summary>
internal sealed record QuickItem(string Id, string Name, string Icon, bool Open, bool Working = false);

internal sealed class QuickBar : Window
{
    private readonly string _iconProfile;
    private readonly StackPanel _row = new() { Orientation = Orientation.Horizontal };
    private readonly Border _card;
    private bool _edit;

    public QuickBar(string iconProfile)
    {
        _iconProfile = iconProfile;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        Title = "훈수꾼 빠른 띠";
        _card = new Border
        {
            Child = new Grid { Children = { _row, new Ui.Frame.HandFrame { Mode = Ui.Frame.HandMode.Box, Seed = "quickbar" } } },
            Margin = new Thickness(8),
            Padding = new Thickness(4),
            Background = (Brush)FindResource("Hunsu.Glass"),
            BorderBrush = (Brush)FindResource("Hunsu.Line"),
            BorderThickness = new Thickness(1),
            Effect = (System.Windows.Media.Effects.Effect)FindResource("Hunsu.Shadow1"),
            Opacity = 0.85,
        };
        _card.SetResourceReference(Border.CornerRadiusProperty, "Hunsu.PanelRadius"); // 룩을 바꾸면 따라간다
        _card.MouseEnter += (_, _) => _card.Opacity = 1.0;
        _card.MouseLeave += (_, _) => _card.Opacity = 0.85;
        _card.MouseLeftButtonDown += OnCardDown;
        Content = _card;
        SourceInitialized += (_, _) =>
        {
            Handle = new WindowInteropHelper(this).Handle;
            long ex = User32.GetWindowLongPtr(Handle, User32.GWL_EXSTYLE).ToInt64();
            User32.SetWindowLongPtr(Handle, User32.GWL_EXSTYLE, new IntPtr(ex | User32.WS_EX_NOACTIVATE | User32.WS_EX_TOOLWINDOW));
        };
    }

    public IntPtr Handle { get; private set; }

    /// <summary>창 핸들만 미리 만든다(보이지 않은 채 추적기에 넘기려고).</summary>
    public void Prepare() => new WindowInteropHelper(this).EnsureHandle();

    public event Action<string>? ItemClicked;

    /// <summary>칸 우클릭 메뉴를 만드는 곳(셸이 넣는다: 빠른 설정 · 열기 · 자세히).</summary>
    public Func<string, ContextMenu?>? ItemMenu { get; set; }

    /// <summary>편집 모드에서 끌어 놓은 뒤 (엑셀 창 안 가로 비율, 세로 비율).</summary>
    public event Action<double, double>? Moved;

    /// <summary>끌어 옮길 때 엑셀 창 사각형을 알려 주는 함수(비율 계산용).</summary>
    public Func<PixelRect>? TargetBounds { get; set; }

    public bool EditMode
    {
        get => _edit;
        set
        {
            _edit = value;
            _card.BorderBrush = (Brush)FindResource(value ? ThemeKeys.Accent : "Hunsu.Line");
            _card.Cursor = value ? Cursors.SizeAll : Cursors.Arrow;
            _card.ToolTip = value ? "끌어서 옮기기" : null;
        }
    }

    public void SetItems(IReadOnlyList<QuickItem> items)
    {
        _row.Children.Clear();
        foreach (var it in items) _row.Children.Add(Button(it));
    }

    private FrameworkElement Button(QuickItem it)
    {
        var icon = IconFactory.Create(_iconProfile, it.Icon, 20, (Brush)FindResource(it.Open ? ThemeKeys.Accent : ThemeKeys.Text), it.Name.Length > 0 ? it.Name[..1] : "?");
        var host = new Border
        {
            Child = icon,
            Width = 32,
            Height = 32,
            CornerRadius = (CornerRadius)FindResource("Hunsu.RowRadius"),
            Background = it.Open ? (Brush)FindResource("Hunsu.GlassHover") : Brushes.Transparent,
            ToolTip = it.Name,
            Cursor = Cursors.Hand,
            Margin = new Thickness(1),
            ContextMenu = ItemMenu?.Invoke(it.Id),
        };
        if (it.Working && !it.Open)
        {
            var dot = ActivityDot.Create(this, 6);
            dot.HorizontalAlignment = HorizontalAlignment.Right; dot.VerticalAlignment = VerticalAlignment.Bottom; dot.Margin = new Thickness(0, 0, 2, 2);
            host.Child = null; // 이미 host의 자식이라 먼저 떼어야 Grid에 넣을 수 있다(안 떼면 예외로 프로그램이 꺼진다)
            host.Child = new Grid { Children = { icon, dot } };
        }
        if (icon is FrameworkElement fe) { fe.HorizontalAlignment = HorizontalAlignment.Center; fe.VerticalAlignment = VerticalAlignment.Center; }
        host.MouseEnter += (_, _) => { if (!it.Open) host.Background = (Brush)FindResource("Hunsu.GlassHover"); };
        host.MouseLeave += (_, _) => host.Background = it.Open ? (Brush)FindResource("Hunsu.GlassHover") : Brushes.Transparent;
        host.MouseLeftButtonUp += (_, e) => { if (!_edit) { ItemClicked?.Invoke(it.Id); e.Handled = true; } };
        return host;
    }

    private void OnCardDown(object sender, MouseButtonEventArgs e)
    {
        if (!_edit || e.ButtonState != MouseButtonState.Pressed) return;
        DragMove(); // 놓을 때까지 돌아오지 않는다
        var t = TargetBounds?.Invoke() ?? default;
        if (t.IsEmpty) return;
        var own = User32.WindowBounds(Handle);
        double rx = t.Width - own.Width <= 0 ? 0.5 : (own.X - t.X) / (double)(t.Width - own.Width);
        double ry = t.Height - own.Height <= 0 ? 0.5 : (own.Y - t.Y) / (double)(t.Height - own.Height);
        Moved?.Invoke(Math.Clamp(rx, 0, 1), Math.Clamp(ry, 0, 1));
    }
}
