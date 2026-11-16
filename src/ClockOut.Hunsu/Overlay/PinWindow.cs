// 기능: 핀 창(도크에서 떼어 낸 패널). 엑셀 창의 소유 창이라 엑셀 위에 있고 엑셀을 최소화하면 같이 숨는다.
//       평소에는 클릭이 통과하고(WS_EX_TRANSPARENT), 부르기(Ctrl+Alt+H) 중에만 클릭을 받아 옮기거나 되돌릴 수 있다(docs/01 5장 pin).
//       창은 모듈마다 한 번 만들어 두고 다시 쓴다(되돌릴 때 닫지 않고 숨김) — 새 창을 만드는 데 100ms 넘게 걸려서(1단계 측정).
//       부르기 중에는 오른쪽 아래 모서리 손잡이를 끌어 크기를 바꾼다(최소 Hunsu.WindowMinWidth×MinHeight). 옮기거나 크기를 바꾸면 Moved로 새 자리를 알린다.
// 사용법: var w = new PinWindow(owner); w.Prepare(); w.Host(frame); w.ShowAt(rect, done); w.SetInteractive(true); w.Release();  Moved += rect => 저장
// 의존: Ui/Frame/PanelFrame, Native/User32, Core/PixelRect. 위치와 크기는 실제 픽셀로 다룬다(모니터 배율이 달라도 그대로 돌아오게).
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Native;
using ClockOut.Hunsu.Ui.Frame;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Overlay;

internal sealed class PinWindow : Window
{
    private readonly Border _host = new();
    private readonly Thumb _grip = new();
    private PanelFrame? _frame;
    private PixelRect _rect;
    private IntPtr _owner;
    private bool _interactive;

    public PinWindow(IntPtr owner)
    {
        _owner = owner;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        Title = "훈수꾼 핀 창";
        _host.Margin = (Thickness)Application.Current.FindResource("Hunsu.ShadowMargin");
        var root = new Grid();
        root.Children.Add(_host);
        root.Children.Add(_grip);
        Content = root;
        SetupGrip();
        SourceInitialized += (_, _) =>
        {
            Handle = new WindowInteropHelper(this).Handle;
            ApplyStyle();
            if (_owner != IntPtr.Zero) User32.SetWindowLongPtr(Handle, User32.GWLP_HWNDPARENT, _owner);
        };
    }

    public IntPtr Handle { get; private set; }

    public PixelRect Rect => _rect;

    public FrameworkElement Root => _host;

    /// <summary>사용자가 끌어 옮긴 뒤 새 자리(실제 픽셀).</summary>
    public event Action<PixelRect>? Moved;

    /// <summary>창 핸들을 미리 만들어 둔다(처음 뗄 때 빨리 뜨게).</summary>
    public void Prepare() => new WindowInteropHelper(this).EnsureHandle();

    public void Host(PanelFrame frame)
    {
        _frame = frame;
        frame.IsPinned = true;
        frame.Width = double.NaN;
        frame.TitleDragStarted += OnTitleDrag;
        _host.Child = frame;
        frame.SetSummoned(_interactive);
        _grip.Visibility = _interactive ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>자리를 정하고 나타난다. 엑셀 창 바로 위 z순서에 끼우고, fast 120ms 동안 투명도만 움직인다. done은 움직임 뒤 첫 프레임 시각.</summary>
    public void ShowAt(PixelRect rect, Action<long>? done)
    {
        _rect = rect;
        Opacity = 0;
        if (!IsVisible) Show();
        var after = _owner != IntPtr.Zero ? User32.InsertAfterJustAbove(Handle, _owner) : IntPtr.Zero;
        User32.SetWindowPos(Handle, after, rect.X, rect.Y, rect.Width, rect.Height, User32.SWP_NOACTIVATE | User32.SWP_SHOWWINDOW);
        var dur = _frame != null ? ThemeKeys.Get<Duration>(_frame, ThemeKeys.Fast) : new Duration(TimeSpan.FromMilliseconds(120));
        var fade = new DoubleAnimation(0, 1, dur) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        fade.Completed += (_, _) => { if (done != null) Diagnostics.Frames.Next(done); };
        BeginAnimation(OpacityProperty, fade);
    }

    /// <summary>보이지 않는 채로 자리만 정해 둔다(엑셀이 아직 없거나 전부 숨김 중일 때).</summary>
    public void SetRectOnly(PixelRect rect) => _rect = rect;

    /// <summary>전부 숨김에서 돌아올 때: 움직임 없이 원래 자리에.</summary>
    public void Reappear()
    {
        if (_frame == null) return;
        BeginAnimation(OpacityProperty, null);
        Opacity = 1;
        Show();
        var after = _owner != IntPtr.Zero ? User32.InsertAfterJustAbove(Handle, _owner) : IntPtr.Zero;
        User32.SetWindowPos(Handle, after, _rect.X, _rect.Y, _rect.Width, _rect.Height, User32.SWP_NOACTIVATE | User32.SWP_SHOWWINDOW);
    }

    public void SetOwner(IntPtr owner)
    {
        _owner = owner;
        if (Handle != IntPtr.Zero) User32.SetWindowLongPtr(Handle, User32.GWLP_HWNDPARENT, owner);
    }

    /// <summary>부르기 중이면 클릭을 받고 강조 테두리, 아니면 클릭이 지나간다.</summary>
    public void SetInteractive(bool on)
    {
        _interactive = on;
        _frame?.SetSummoned(on);
        _grip.Visibility = on && _frame != null ? Visibility.Visible : Visibility.Collapsed;
        ApplyStyle();
    }

    private void ApplyStyle()
    {
        if (Handle == IntPtr.Zero) return;
        long ex = User32.GetWindowLongPtr(Handle, User32.GWL_EXSTYLE).ToInt64() | User32.WS_EX_NOACTIVATE | User32.WS_EX_TOOLWINDOW;
        ex = _interactive ? ex & ~User32.WS_EX_TRANSPARENT : ex | User32.WS_EX_TRANSPARENT;
        User32.SetWindowLongPtr(Handle, User32.GWL_EXSTYLE, new IntPtr(ex));
        User32.ApplyStyleChange(Handle);
    }

    /// <summary>오른쪽 아래 모서리 손잡이: 점 세 개짜리 작은 삼각형. 부르기 중에만 보인다.</summary>
    private void SetupGrip()
    {
        var margin = _host.Margin;
        _grip.Width = 16;
        _grip.Height = 16;
        _grip.HorizontalAlignment = HorizontalAlignment.Right;
        _grip.VerticalAlignment = VerticalAlignment.Bottom;
        _grip.Margin = new Thickness(0, 0, margin.Right + 2, margin.Bottom + 2);
        _grip.Cursor = Cursors.SizeNWSE;
        _grip.Visibility = Visibility.Collapsed;
        var dots = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
        dots.SetValue(System.Windows.Shapes.Path.DataProperty, System.Windows.Media.Geometry.Parse("M12,4 L13,4 L13,5 L12,5 Z M8,8 L9,8 L9,9 L8,9 Z M12,8 L13,8 L13,9 L12,9 Z M4,12 L5,12 L5,13 L4,13 Z M8,12 L9,12 L9,13 L8,13 Z M12,12 L13,12 L13,13 L12,13 Z"));
        dots.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, ThemeKeys.Muted);
        var bg = new FrameworkElementFactory(typeof(Grid));
        bg.SetValue(Panel.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
        bg.AppendChild(dots);
        _grip.Template = new ControlTemplate(typeof(Thumb)) { VisualTree = bg };
        _grip.DragDelta += OnGripDrag;
        _grip.DragCompleted += (_, _) =>
        {
            var now = User32.WindowBounds(Handle);
            if (now == _rect) return;
            _rect = now;
            Moved?.Invoke(now);
        };
    }

    private void OnGripDrag(object sender, DragDeltaEventArgs e)
    {
        if (!_interactive || Handle == IntPtr.Zero) return;
        double scale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        var now = User32.WindowBounds(Handle);
        var margin = _host.Margin;
        int minW = (int)(((double)FindResource("Hunsu.WindowMinWidth") + margin.Left + margin.Right) * scale);
        int minH = (int)(((double)FindResource("Hunsu.WindowMinHeight") + margin.Top + margin.Bottom) * scale);
        int w = Math.Max(minW, now.Width + (int)Math.Round(e.HorizontalChange * scale));
        int h = Math.Max(minH, now.Height + (int)Math.Round(e.VerticalChange * scale));
        if (w == now.Width && h == now.Height) return;
        User32.SetWindowPos(Handle, IntPtr.Zero, 0, 0, w, h, User32.SWP_NOACTIVATE | User32.SWP_NOMOVE | User32.SWP_NOZORDER);
    }

    private void OnTitleDrag(MouseButtonEventArgs e)
    {
        if (!_interactive || e.ButtonState != MouseButtonState.Pressed) return;
        DragMove(); // 놓을 때까지 돌아오지 않는다
        var now = User32.WindowBounds(Handle);
        if (now != _rect)
        {
            _rect = now;
            Moved?.Invoke(now);
        }
    }

    /// <summary>패널 틀을 놓아주고 창은 숨겨 둔다(다음에 다시 씀).</summary>
    public PanelFrame? Release()
    {
        var frame = _frame;
        if (frame != null)
        {
            frame.TitleDragStarted -= OnTitleDrag;
            frame.SetSummoned(false);
        }
        _host.Child = null;
        _frame = null;
        _grip.Visibility = Visibility.Collapsed;
        BeginAnimation(OpacityProperty, null);
        Hide();
        return frame;
    }

    public bool HasFrame => _frame != null;
}
