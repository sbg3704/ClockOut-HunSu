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
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Native;
using ClockOut.Hunsu.Ui.Frame;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Overlay;

internal sealed class PinWindow : Window
{
    private readonly Border _host = new();
    private readonly StackPanel _stack = new(); // 엄마 패널: 패널 여러 개를 얇은 선으로 나눠 한 창에 담는다(보통은 하나)
    private readonly Thumb _grip = new();
    private PanelFrame? _frame;
    private PixelRect _rect;
    private IntPtr _owner;
    private bool _interactive;
    private bool _locked;
    private bool _hover; // 제목줄 위에 마우스가 있어 이 순간만 클릭을 받는다(핀 단추 등을 평소에도 누를 수 있게)

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

    /// <summary>true를 돌려주면 Shift 없이도 끌어 옮길 수 있다(배치 잠금이 "free"일 때). null이면 잠금.</summary>
    public Func<bool>? FreeDrag { get; set; }

    /// <summary>끌어 놓을 때 붙일 대상 사각형들(작업 영역, 엑셀 창, 다른 핀 창). null이면 붙이지 않는다. Alt를 누른 채 놓으면 붙이지 않는다.</summary>
    public Func<IReadOnlyList<PixelRect>>? SnapTargets { get; set; }

    /// <summary>끌기를 시작할 때 이 창과 자석으로 이어진 다른 핀 창들(같이 움직일 것). null이면 혼자. Alt를 누른 채 끌면 쓰지 않는다.</summary>
    public Func<IReadOnlyList<PinWindow>>? GroupPeers { get; set; }

    /// <summary>끌어 놓은 자리(실제 픽셀)를 받아, 사이드 패널(오른쪽 도크)에 붙였으면 true를 돌려준다(이 창은 닫히므로 자리를 저장하지 않는다).</summary>
    public Func<PixelRect, bool>? DropHandler { get; set; }

    private PixelRect _followStart;

    /// <summary>묶음에 딸려 가기 시작(지금 자리를 기억).</summary>
    public void FollowBegin() => _followStart = _rect;

    /// <summary>끄는 동안 끈 창의 이동량만큼 따라간다(저장은 끝날 때).</summary>
    public void FollowTo(int dx, int dy)
    {
        if (Handle == IntPtr.Zero || !IsVisible) return;
        User32.SetWindowPos(Handle, IntPtr.Zero, _followStart.X + dx, _followStart.Y + dy, 0, 0, User32.SWP_NOACTIVATE | User32.SWP_NOZORDER | User32.SWP_NOSIZE);
    }

    /// <summary>끌기가 끝났다: 최종 이동량으로 자리를 정하고 저장하게 알린다.</summary>
    public void FollowEnd(int dx, int dy)
    {
        if (dx == 0 && dy == 0) { FollowTo(0, 0); return; }
        MoveTo(_followStart with { X = _followStart.X + dx, Y = _followStart.Y + dy });
        Moved?.Invoke(_rect);
    }

    /// <summary>이 패널의 자리를 잠갔다(모듈별 잠금): 편집 모드에서도 끌거나 크기를 바꾸지 못한다.</summary>
    public bool Locked
    {
        get => _locked;
        set { _locked = value; _grip.Visibility = _interactive && !value && _frame != null ? Visibility.Visible : Visibility.Collapsed; }
    }

    /// <summary>보이는 채로 자리만 옮긴다(편집 취소로 저장된 자리로 되돌릴 때).</summary>
    public void MoveTo(PixelRect rect)
    {
        _rect = rect;
        if (Handle == IntPtr.Zero || !IsVisible) return;
        User32.SetWindowPos(Handle, IntPtr.Zero, rect.X, rect.Y, rect.Width, rect.Height, User32.SWP_NOACTIVATE | User32.SWP_NOZORDER);
    }

    /// <summary>사용자가 끌어 옮긴 뒤 새 자리(실제 픽셀).</summary>
    public event Action<PixelRect>? Moved;

    /// <summary>창 핸들을 미리 만들어 둔다(처음 뗄 때 빨리 뜨게).</summary>
    public void Prepare() => new WindowInteropHelper(this).EnsureHandle();

    /// <summary>이 창에 담긴 패널들(위에서 아래로). 보통 하나, 묶었으면 여럿.</summary>
    public IReadOnlyList<PanelFrame> Frames => [.. _stack.Children.OfType<PanelFrame>()];

    /// <summary>엄마 패널에 패널을 더한다(아래에 이어 붙임). 끌기·부르기 상태는 창 하나가 같이 갖는다.</summary>
    public void AddFrame(PanelFrame f)
    {
        f.IsPinned = true;
        f.Width = double.NaN;
        f.TitleDragStarted += OnTitleDrag;
        f.SetSummoned(_interactive);
        _stack.Children.Add(f);
        ApplyJoins();
    }

    /// <summary>묶음에서 패널 하나를 뺀다(창은 그대로, 나머지는 계속 담김).</summary>
    public void RemoveFrame(PanelFrame f)
    {
        f.TitleDragStarted -= OnTitleDrag;
        _stack.Children.Remove(f);
        f.SetJoin(JoinPos.None);
        if (ReferenceEquals(_frame, f)) _frame = Frames.FirstOrDefault();
        ApplyJoins();
    }

    private void ApplyJoins()
    {
        var list = Frames;
        for (int i = 0; i < list.Count; i++)
        {
            list[i].Margin = new Thickness(0, i == 0 ? 0 : -1, 0, 0); // 테두리 한 줄이 칸막이
            list[i].SetJoin(list.Count == 1 ? JoinPos.None : i == 0 ? JoinPos.First : i == list.Count - 1 ? JoinPos.Last : JoinPos.Middle);
        }
    }

    /// <summary>창 크기를 바꾼다(묶어서 높이가 늘 때).</summary>
    public void ResizeTo(int width, int height)
    {
        _rect = _rect with { Width = width, Height = height };
        if (Handle != IntPtr.Zero && IsVisible) User32.SetWindowPos(Handle, IntPtr.Zero, 0, 0, width, height, User32.SWP_NOACTIVATE | User32.SWP_NOMOVE | User32.SWP_NOZORDER);
    }

    public void Host(PanelFrame frame)
    {
        _stack.Children.Clear();
        _stack.Children.Add(frame);
        frame.Margin = new Thickness(0);
        _frame = frame;
        frame.IsPinned = true;
        frame.Width = double.NaN;
        frame.TitleDragStarted += OnTitleDrag;
        frame.SetTitleTip("Shift를 누른 채 끌어 옮기기(Ctrl+Alt+H로 부르면 그냥 끌기)");
        _host.Child = _stack;
        frame.SetSummoned(_interactive);
        _grip.Visibility = _interactive && !_locked ? Visibility.Visible : Visibility.Collapsed;
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
        foreach (var f in Frames) f.SetSummoned(on);
        _grip.Visibility = on && !_locked && _frame != null ? Visibility.Visible : Visibility.Collapsed;
        ApplyStyle();
    }

    /// <summary>마우스(화면 픽셀)가 제목줄 위에 있으면 그 순간만 클릭을 받는다. 본문은 계속 클릭이 지나간다. 앱이 60ms마다 부른다.</summary>
    public void UpdateHover(int screenX, int screenY)
    {
        if (_interactive || Handle == IntPtr.Zero || !IsVisible || _frame == null) { SetHover(false); return; }
        var r = User32.WindowBounds(Handle);
        if (!r.Contains(screenX, screenY)) { SetHover(false); return; }
        double scale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double title = (double)FindResource("Hunsu.TitleBarHeight");
        // 묶음이면 칸마다 제목줄이 있다: 어느 제목줄 위든 그 순간 클릭을 받는다.
        bool onTitle = false;
        foreach (var f in Frames)
        {
            double top = f.TranslatePoint(new Point(0, 0), this).Y;
            int y0 = r.Y + (int)Math.Round(top * scale), y1 = y0 + (int)Math.Round(title * scale);
            if (screenY >= y0 && screenY < y1) { onTitle = true; break; }
        }
        SetHover(onTitle);
    }

    private void SetHover(bool on)
    {
        if (_hover == on) return;
        _hover = on;
        ApplyStyle();
    }

    private void ApplyStyle()
    {
        if (Handle == IntPtr.Zero) return;
        long ex = User32.GetWindowLongPtr(Handle, User32.GWL_EXSTYLE).ToInt64() | User32.WS_EX_NOACTIVATE | User32.WS_EX_TOOLWINDOW;
        ex = _interactive || _hover ? ex & ~User32.WS_EX_TRANSPARENT : ex | User32.WS_EX_TRANSPARENT;
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
        if (_locked || !(_interactive || _hover) || e.ButtonState != MouseButtonState.Pressed) return;
        // 잠금(기본)이면 Shift를 누른 채만 옮긴다. 부르기(편집 모드) 중이거나 잠금이 free면 그냥 옮긴다.
        if (!_interactive && !(FreeDrag?.Invoke() ?? false) && !User32.ShiftDown()) return;
        IReadOnlyList<PinWindow> peers = User32.AltDown() ? [] : GroupPeers?.Invoke() ?? [];
        var before = User32.WindowBounds(Handle);
        foreach (var p in peers) p.FollowBegin();
        // 끄는 동안 붙은 창들도 같이 움직인다(실시간): 창이 움직일 때마다 이동량만큼 따라간다.
        void Follow(object? s, EventArgs a)
        {
            var b = User32.WindowBounds(Handle);
            foreach (var p in peers) p.FollowTo(b.X - before.X, b.Y - before.Y);
        }
        if (peers.Count > 0) LocationChanged += Follow;
        DragMove(); // 놓을 때까지 돌아오지 않는다
        LocationChanged -= Follow;
        var now = User32.WindowBounds(Handle);
        if (SnapTargets != null && !User32.AltDown())
        {
            var snapped = PinPlacement.Snap(now, SnapTargets());
            if (snapped != now) { MoveTo(snapped); now = snapped; }
        }
        int fdx = now.X - before.X, fdy = now.Y - before.Y;
        foreach (var p in peers) p.FollowEnd(fdx, fdy);
        if (DropHandler?.Invoke(now) == true) return; // 엑셀 오른쪽 가장자리 근처에 놓음 → 사이드 패널로 붙음
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
        _stack.Children.Clear();
        _frame = null;
        _grip.Visibility = Visibility.Collapsed;
        _hover = false;
        BeginAnimation(OpacityProperty, null);
        Hide();
        return frame;
    }

    public bool HasFrame => _stack.Children.Count > 0;
}
