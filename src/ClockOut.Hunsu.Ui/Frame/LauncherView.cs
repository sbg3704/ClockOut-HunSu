// 기능: 엑셀 옆 모듈 아이콘 런처(docs/09 2장). 평소엔 훈 원과 점(또는 흐린 아이콘)만, 가장자리로 마우스가 오면 깨어나 아이콘이 펴진다.
//       기능 묶음에 올리면 그 안 모듈이 한 줄로 펴지고, 아이콘에 올리면 이름 알약이 나온다. 누르면 ModuleClicked, 우클릭은 메뉴.
// 사용법: var l = new LauncherView(); l.Configure(shape, grouped, scale, idle, profile, mirror); l.SetModules(list);
//         창이 커서 위치를 주기적으로 넣는다: l.UpdatePointer(localPointOrNull).  l.ModuleClicked += id => ...;  l.HubBounds → 훈 원 자리
// 의존: Core/Layout(LauncherGroups, LauncherShapes), Frame/LauncherParts, Theme. 바탕이 비어 있어(배경 없음) 그림 밖 클릭은 아래로 지나간다.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Ui.Frame;

public sealed class LauncherView : Canvas
{
    private readonly List<(LauncherItem Item, FrameworkElement El, LPoint P)> _items = [];
    private readonly List<FrameworkElement> _dots = [];
    private readonly List<FrameworkElement> _row = [];
    private readonly DispatcherTimer _sleep;
    private IReadOnlyList<LauncherModule> _modules = [];
    private LauncherPlan? _plan;
    private FrameworkElement? _hub, _tag;
    private string _shape = DockShapes.Crescent, _profile = "fluent";
    private bool _grouped = true, _mirror;
    private double _scale = 1, _idle = 0.45;
    private Rect _rowZone = Rect.Empty;

    public LauncherView()
    {
        IsHitTestVisible = true; // 배경이 없어 그림 위에서만 마우스를 받는다
        _sleep = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _sleep.Tick += (_, _) => { _sleep.Stop(); SetAwake(false); };
        SizeChanged += (_, _) => Rebuild();
        Loaded += (_, _) => Rebuild();
    }

    public event Action<string>? ModuleClicked;
    public event Action? HubClicked;

    /// <summary>모듈 우클릭 메뉴를 만드는 곳(모듈 id). 훈 원 메뉴는 HubMenu.</summary>
    public Func<string, ContextMenu?>? ModuleMenu { get; set; }
    public ContextMenu? HubMenu { get; set; }

    public bool Awake { get; private set; }
    public string? ExpandedGroup { get; private set; }

    /// <summary>훈 원 자리(이 부품 기준 논리 px). 측정 도구가 클릭 받는 구역으로 쓴다.</summary>
    public Rect HubBounds => _hub == null ? Rect.Empty : new Rect(GetLeft(_hub), GetTop(_hub), _hub.Width, _hub.Height);

    public void Configure(string shape, bool grouped, double scale, double idleOpacity, string profile, bool mirror)
    {
        (_shape, _grouped, _scale, _idle, _profile, _mirror) = (DockShapes.Normalize(shape), grouped, scale, Math.Clamp(idleOpacity, 0.15, 1), profile, mirror);
        Rebuild();
    }

    /// <summary>모듈 목록(이름, 아이콘, 배지, 열림)을 넣는다. 배지만 바뀌어도 다시 그린다(칸이 스무 개 남짓이라 가볍다).</summary>
    public void SetModules(IReadOnlyList<LauncherModule> modules)
    {
        _modules = modules;
        Rebuild();
    }

    /// <summary>커서 위치(이 부품 기준), 창 밖이면 null. 가장자리 가까이면 깨우고, 멀어지면 잠깐 뒤 잠든다.</summary>
    public void UpdatePointer(Point? p)
    {
        if (_plan == null || ActualWidth <= 0) return;
        bool inside = p is { } q && q.Y >= 0 && q.Y <= ActualHeight && q.X >= 0 && q.X <= ActualWidth;
        double edge = inside ? (_mirror ? p!.Value.X : ActualWidth - p!.Value.X) : double.MaxValue;
        bool near = inside && edge <= _plan.WakeDistance;
        bool onRow = inside && !_rowZone.IsEmpty && _rowZone.Contains(p!.Value);
        if (near || onRow)
        {
            _sleep.Stop();
            if (!Awake) SetAwake(true);
        }
        else if (Awake && !_sleep.IsEnabled) _sleep.Start();
        if (ExpandedGroup != null && !onRow && !near) Collapse();
        if (Awake && _shape == DockShapes.Rail) Magnify(near ? p!.Value.Y : (double?)null);
    }

    private void Rebuild()
    {
        Children.Clear();
        _items.Clear(); _dots.Clear(); _row.Clear(); _hub = null; _tag = null; _rowZone = Rect.Empty;
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        var items = LauncherGroups.Build(_modules, _grouped);
        _plan = LauncherShapes.Plan(_shape, items.Count, _scale, ActualWidth, ActualHeight, awake: true, _grouped);
        if (_plan.ShowHub)
        {
            _hub = LauncherParts.Hub(this, _plan.HubSize);
            Place(_hub, _plan.Hub, _plan.HubSize, _plan.HubSize);
            _hub.MouseLeftButtonUp += (_, e) => { e.Handled = true; SetAwake(!Awake); HubClicked?.Invoke(); };
            _hub.ContextMenu = HubMenu;
            Children.Add(_hub);
        }
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            var p = _plan.Items[i];
            FrameworkElement el;
            if (_plan.Pills)
            {
                el = LauncherParts.Pill(this, _profile, it.Icon, it.Name, _plan.ItemSize, it.Badge, it.Tone, it.Open, it.IsGroup);
                el.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double w = el.DesiredSize.Width;
                Place(el, new LPoint(0, p.Y), w, _plan.ItemSize, pillRight: p.X);
            }
            else
            {
                el = LauncherParts.Icon(this, _profile, it.Icon, it.Name, _plan.ItemSize, it.Badge, it.Tone, it.Open, it.IsGroup);
                Place(el, p, _plan.ItemSize, _plan.ItemSize);
                el.MouseEnter += (_, _) => ShowTag(it, p);
                el.MouseLeave += (_, _) => HideTag();
            }
            Wire(el, it);
            el.RenderTransform = new TransformGroup { Children = { new ScaleTransform(1, 1, _plan.ItemSize / 2, _plan.ItemSize / 2), new TranslateTransform() } };
            Children.Add(el);
            _items.Add((it, el, p));
        }
        if (_plan.Rest == "dots")
            for (int i = 0; i < items.Count && i < _plan.RestDots.Count; i++)
            {
                var d = LauncherParts.Dot(this, LauncherParts.Shown(items[i].Badge), items[i].Open);
                Place(d, _plan.RestDots[i], 6, 6);
                Children.Add(d);
                _dots.Add(d);
            }
        if (ExpandedGroup != null && items.FirstOrDefault(x => x.Key == ExpandedGroup) is { IsGroup: true } g) Expand(g);
        else ExpandedGroup = null;
        ApplyAwake(animate: false);
    }

    private void Wire(FrameworkElement el, LauncherItem it)
    {
        el.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            if (it.IsGroup) { if (ExpandedGroup == it.Key) Collapse(); else Expand(it); }
            else ModuleClicked?.Invoke(it.Key);
        };
        if (it.IsGroup) el.MouseEnter += (_, _) => { if (Awake && ExpandedGroup != it.Key) Expand(it); };
        else el.ContextMenu = ModuleMenu?.Invoke(it.Key);
    }

    private void SetAwake(bool awake)
    {
        if (Awake == awake) return;
        Awake = awake;
        if (!awake) Collapse();
        ApplyAwake(animate: true);
    }

    /// <summary>깨어남·잠듦: 투명도와 이동만 움직인다(fast, docs/03 6장). 잠든 아이콘은 클릭을 받지 않게 숨긴다(흐린 모양 제외).</summary>
    private void ApplyAwake(bool animate)
    {
        if (_plan == null) return;
        bool dim = _plan.Rest == "dim";
        if (_hub != null) Fade(_hub, Awake ? 1 : Math.Max(_idle, 0.6), animate);
        foreach (var (_, el, p) in _items)
        {
            var tr = (TranslateTransform)((TransformGroup)el.RenderTransform).Children[1];
            double dx = (_plan.Hub.X - p.X) * 0.5, dy = (_plan.Hub.Y - p.Y) * 0.5;
            if (Awake || dim)
            {
                el.Visibility = Visibility.Visible;
                Fade(el, Awake ? 1 : _idle, animate);
                Move(tr, 0, 0, animate);
            }
            else
            {
                Fade(el, 0, animate, () => { if (!Awake) el.Visibility = Visibility.Hidden; });
                Move(tr, _mirror ? -dx : dx, dy, animate);
            }
        }
        foreach (var d in _dots) Fade(d, Awake ? 0 : Math.Max(_idle, 0.5), animate);
    }

    private void Expand(LauncherItem g)
    {
        Collapse();
        if (_plan == null || !Awake) return;
        ExpandedGroup = g.Key;
        var idx = _items.FindIndex(x => x.Item.Key == g.Key);
        double y = _items[idx].P.Y, size = Math.Round(_plan.ItemSize * 0.84);
        double edgeLeft = _items.Min(x => Dist(x.El, x.P));
        var pts = LauncherShapes.Row(edgeLeft, y, g.Members.Count, size);
        double far = 0, minX = double.MaxValue, maxX = double.MinValue;
        for (int j = 0; j < g.Members.Count; j++)
        {
            var m = g.Members[j];
            var p = Flip(pts[j]);
            var el = LauncherParts.Icon(this, _profile, m.Icon, m.Name, size, m.Badge, m.Tone, m.Open, false);
            Place(el, p, size, size);
            el.MouseLeftButtonUp += (_, e) => { e.Handled = true; ModuleClicked?.Invoke(m.Id); };
            el.ContextMenu = ModuleMenu?.Invoke(m.Id);
            var tip = LauncherParts.Label(this, m.Name, true);
            tip.Visibility = Visibility.Hidden;
            tip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            SetLeft(tip, p.X - tip.DesiredSize.Width / 2); SetTop(tip, p.Y - size / 2 - tip.DesiredSize.Height - 4);
            el.MouseEnter += (_, _) => tip.Visibility = Visibility.Visible;
            el.MouseLeave += (_, _) => tip.Visibility = Visibility.Hidden;
            el.RenderTransform = new TranslateTransform(_mirror ? -14 : 14, 0);
            el.Opacity = 0;
            Children.Add(el); Children.Add(tip);
            _row.Add(el); _row.Add(tip);
            var delay = TimeSpan.FromMilliseconds(14 * j);
            Fade(el, 1, true, null, delay);
            Move((TranslateTransform)el.RenderTransform, 0, 0, true, delay);
            far = Math.Abs(pts[j].X - edgeLeft) + size;
            minX = Math.Min(minX, p.X - size / 2); maxX = Math.Max(maxX, p.X + size / 2);
        }
        var cap = LauncherParts.Label(this, g.Name, false);
        cap.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double capX = edgeLeft - far - 18 - cap.DesiredSize.Width;
        var capP = Flip(new LPoint(capX + cap.DesiredSize.Width / 2, y));
        SetLeft(cap, capP.X - cap.DesiredSize.Width / 2); SetTop(cap, y - cap.DesiredSize.Height / 2);
        Children.Add(cap); _row.Add(cap);
        minX = Math.Min(minX, capP.X - cap.DesiredSize.Width / 2); maxX = Math.Max(maxX, capP.X + cap.DesiredSize.Width / 2);
        // 묶음 칸에서 줄까지 마우스가 지나가는 길도 포함(가장자리 쪽 끝까지)
        _rowZone = _mirror ? new Rect(0, y - size, maxX + 20, size * 2) : new Rect(minX - 20, y - size, ActualWidth - minX + 20, size * 2);
        foreach (var (it, el, _) in _items) if (it.Key == g.Key && el.Tag is Border c) c.BorderBrush = LauncherParts.B(this, ThemeKeys.Accent);
    }

    private void Collapse()
    {
        foreach (var e in _row) Children.Remove(e);
        _row.Clear();
        _rowZone = Rect.Empty;
        if (ExpandedGroup != null)
            foreach (var (it, el, _) in _items) if (it.Key == ExpandedGroup && el.Tag is Border c) c.BorderBrush = LauncherParts.B(this, "Hunsu.Line");
        ExpandedGroup = null;
    }

    private void ShowTag(LauncherItem it, LPoint p)
    {
        HideTag();
        if (!Awake || _plan == null || (it.IsGroup && ExpandedGroup == it.Key)) return;
        string text = it.IsGroup ? $"{it.Name} · {it.Members.Count}" : it.Name;
        _tag = LauncherParts.Tag(this, text, it.Badge, it.Tone, _plan.ItemSize, _mirror);
        _tag.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double w = _tag.DesiredSize.Width;
        SetLeft(_tag, _mirror ? p.X - _plan.ItemSize / 2 : p.X + _plan.ItemSize / 2 - w);
        SetTop(_tag, p.Y - _tag.DesiredSize.Height / 2);
        SetZIndex(_tag, -1);
        Children.Add(_tag);
        Fade(_tag, 1, true);
    }

    private void HideTag()
    {
        if (_tag != null) Children.Remove(_tag);
        _tag = null;
    }

    private void Magnify(double? y)
    {
        foreach (var (_, el, p) in _items)
        {
            double k = y is { } v ? 1 + Math.Max(0, 0.3 * (1 - Math.Abs(v - p.Y) / 90)) : 1;
            var s = (ScaleTransform)((TransformGroup)el.RenderTransform).Children[0];
            s.ScaleX = s.ScaleY = k;
        }
    }

    // 자리 도우미: 계획은 오른쪽 가장자리 기준이라 왼쪽 도크(mirror)는 x를 뒤집는다.
    private LPoint Flip(LPoint p) => _mirror ? new LPoint(ActualWidth - p.X, p.Y) : p;

    private void Place(FrameworkElement el, LPoint p, double w, double h, double? pillRight = null)
    {
        if (pillRight is { } right)
        {
            double x = _mirror ? ActualWidth - right : right - w;
            SetLeft(el, x); SetTop(el, p.Y - h / 2);
            return;
        }
        var f = Flip(p);
        SetLeft(el, f.X - w / 2); SetTop(el, f.Y - h / 2);
    }

    /// <summary>오른쪽 기준 계획 좌표에서, 그 칸의 가장자리 반대쪽 끝(묶음 줄이 시작할 자리).</summary>
    private double Dist(FrameworkElement el, LPoint p) => _plan!.Pills ? p.X - el.DesiredSize.Width : p.X - _plan.ItemSize / 2;

    private void Fade(UIElement e, double to, bool animate, Action? done = null, TimeSpan? delay = null)
    {
        if (!animate || to == e.Opacity && delay == null)
        {
            e.BeginAnimation(OpacityProperty, null);
            e.Opacity = to;
            done?.Invoke();
            return;
        }
        var a = new DoubleAnimation(to, ThemeKeys.Get<Duration>(this, ThemeKeys.Fast)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, BeginTime = delay ?? TimeSpan.Zero };
        if (done != null) a.Completed += (_, _) => done();
        e.BeginAnimation(OpacityProperty, a);
    }

    private void Move(TranslateTransform t, double x, double y, bool animate, TimeSpan? delay = null)
    {
        if (!animate) { t.BeginAnimation(TranslateTransform.XProperty, null); t.BeginAnimation(TranslateTransform.YProperty, null); t.X = x; t.Y = y; return; }
        var d = ThemeKeys.Get<Duration>(this, ThemeKeys.Fast);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        t.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(x, d) { EasingFunction = ease, BeginTime = delay ?? TimeSpan.Zero });
        t.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(y, d) { EasingFunction = ease, BeginTime = delay ?? TimeSpan.Zero });
    }
}
