// 기능: 사이드바 끌어 옮기기(SidebarWindow의 일부, docs/19). 묶음 탭 아이콘은 그냥 끌면 순서만 바뀐다(2026-11-17 사용자 요청: "이 위치에서 순서도 바뀌게").
//   합치기와 모듈 옮기기는 Shift를 누른 채(배치 잠금 "자유"이거나 Ctrl+Alt+H로 부른 동안은 그냥) 끈다.
//   탭 아이콘 → 다른 탭 아이콘의 위/아래 절반(Shift면 위/아래 끝): 순서 바꾸기, Shift면 가운데: 합치기. 막대의 빈 아래쪽: 맨 끝. Shift로 모듈 위에 놓으면 그 묶음에 합치기.
//   모듈 제목 → 탭 아이콘: 그 묶음 맨 아래로, 다른 모듈 위/아래 절반: 그 자리로, 막대 끝의 "＋" 칸: 새 묶음.
//   실수로 옮기지 않게 4px 넘게 끌어야 시작하고, 놓을 곳이 아니면 아무것도 바뀌지 않는다. 결과 계산은 Core/Layout/SideTabs.
// 사용법: 생성자가 InitDrag()를 부르고, Tabs 쪽이 BeginPending(종류, id, 요소, e)를 부른다. 앱은 DragAllowed와 TabsChanged만 안다.
// 의존: Core/Layout/SideTabs, Theme 이름. 창이 초점을 받지 않아도(WS_EX_NOACTIVATE) 마우스 잡기로 동작한다(OLE 끌어 놓기를 쓰지 않음).
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Overlay;

internal sealed partial class SidebarWindow
{
    private const double DragThreshold = 6; // 클릭이 끌기로 바뀌지 않게
    private readonly Canvas _overlay = new() { IsHitTestVisible = false };
    private readonly Border _ghost = new() { CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 3, 8, 3), Visibility = Visibility.Collapsed, Opacity = 0.92 };
    private readonly TextBlock _ghostText = new() { FontSize = 12 };
    private readonly Border _newZone = new()
    {
        Width = 32, Height = 32, Margin = new Thickness(4, 6, 4, 2), CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1.5), Visibility = Visibility.Collapsed,
        Child = new TextBlock { Text = "＋", FontSize = 15, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        ToolTip = "여기 놓으면 새 묶음",
    };
    private string? _pendKind, _pendId;
    private FrameworkElement? _pendSource;
    private Point _pendStart;
    private bool _dragging, _renderPending;
    private (string Kind, string Key, int Zone, FrameworkElement El)? _target;

    /// <summary>지금 끌어 옮길 수 있는가(앱이 정함: 배치 잠금 "자유", 부르기 중, 또는 Shift를 누름).</summary>
    public Func<bool>? DragAllowed { get; set; }

    private void InitDrag()
    {
        _ghost.SetResourceReference(Border.BackgroundProperty, ThemeKeys.Accent);
        _ghostText.SetResourceReference(TextBlock.ForegroundProperty, "Hunsu.AccentInk");
        _ghost.Child = _ghostText;
        _overlay.Children.Add(_ghost);
        _newZone.SetResourceReference(Border.BorderBrushProperty, ThemeKeys.Muted);
        ((TextBlock)_newZone.Child).SetResourceReference(TextBlock.ForegroundProperty, ThemeKeys.Muted);
        _root.PreviewMouseMove += OnDragMove;
        _root.PreviewMouseLeftButtonUp += OnDragUp;
        _root.LostMouseCapture += (_, _) => { if (_dragging) CancelDrag(); };
    }

    private void BeginPending(string kind, string id, FrameworkElement source, MouseButtonEventArgs e)
    {
        // 묶음 아이콘은 그냥 끌어도 순서만 바뀐다(실수해도 되돌리기 쉬움). 합치기와 모듈 옮기기는 Shift(또는 자유·부르기 중)가 있어야 한다.
        if (_dragging || (kind != "tab" && !MergeAllowed())) return;
        (_pendKind, _pendId, _pendSource, _pendStart) = (kind, id, source, e.GetPosition(_root));
    }

    private void OnDragMove(object sender, MouseEventArgs e)
    {
        if (_pendKind == null) return;
        if (e.LeftButton != MouseButtonState.Pressed) { CancelDrag(); return; }
        var p = e.GetPosition(_root);
        if (!_dragging)
        {
            if (Math.Abs(p.X - _pendStart.X) < DragThreshold && Math.Abs(p.Y - _pendStart.Y) < DragThreshold) return;
            _dragging = true;
            _root.CaptureMouse();
            if (_pendSource != null) _pendSource.Opacity = 0.45;
            _ghostText.Text = _pendKind == "tab" ? "묶음: " + (_tabList.FirstOrDefault(t => t.Key == _pendId)?.Name ?? "") : ViewOf(_pendId!)?.Module.Manifest.Name ?? "";
            _ghost.Visibility = Visibility.Visible;
            _newZone.Visibility = _pendKind == "module" ? Visibility.Visible : Visibility.Collapsed;
        }
        Canvas.SetLeft(_ghost, Math.Max(0, Math.Min(p.X + 12, _root.ActualWidth - _ghost.ActualWidth - 4))); // 창 밖으로 잘리지 않게
        Canvas.SetTop(_ghost, p.Y + 10);
        SetTarget(FindTarget(p));
        e.Handled = true;
    }

    private bool MergeAllowed() => DragAllowed?.Invoke() ?? false;

    private void OnDragUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) { ClearPending(); return; }
        var target = _target;
        string kind = _pendKind!, id = _pendId!;
        CancelDrag();
        e.Handled = true; // 탭 아이콘의 클릭(고르기)으로 이어지지 않게
        if (target is { } t) DropAt(kind, id, t.Kind, t.Key, t.Zone);
        FlushRender();
    }

    /// <summary>끄는 동안 들어온 다시 그리기를 끝난 뒤에 한다(끄는 중에 막대를 새로 지으면 끌기가 끊긴다).</summary>
    private void FlushRender()
    {
        if (!_renderPending || _dragging) return;
        _renderPending = false;
        Render();
    }

    /// <summary>놓을 곳 찾기: 탭 아이콘(위/가운데/아래), 모듈(위/아래 절반), 묶음 머리글, 새 묶음 칸.</summary>
    private (string Kind, string Key, int Zone, FrameworkElement El)? FindTarget(Point p)
    {
        if (_newZone.IsVisible && Hit(_newZone, p, out _)) return ("new", "", 0, _newZone);
        bool merge = _pendKind == "module" || MergeAllowed();
        foreach (var (key, el) in _tabIcons)
        {
            if (!Hit(el, p, out double ry, pad: 3)) continue; // 아이콘 사이 틈도 잡히게
            if (_pendKind == "tab" && key == _pendId) return null;
            int zone = _pendKind != "tab" ? 0 : merge ? (ry < 0.3 ? -1 : ry > 0.7 ? 1 : 0) : (ry < 0.5 ? -1 : 1);
            return ("tab", key, zone, el);
        }
        if (_pendKind == "tab" && _tabIcons.Count > 0 && Hit(_bar, p, out _, pad: 0))
        {
            // 막대의 빈 아래쪽: 맨 끝으로
            var last = _tabIcons.Last();
            if (last.Key != _pendId) return ("tab", last.Key, 1, last.Value);
            return null;
        }
        if (!merge) return null; // 그냥 끌기는 순서만
        foreach (var (key, el) in _heads)
            if (Hit(el, p, out _)) return ("head", key, 0, el);
        foreach (var (mid, el) in _sections)
        {
            if (!Hit(el, p, out double ry)) continue;
            if (_pendKind == "module" && mid == _pendId) return null;
            return ("section", mid, ry < 0.5 ? -1 : 1, el);
        }
        return null;
    }

    private bool Hit(FrameworkElement el, Point p, out double relY, double pad = 0)
    {
        relY = 0;
        if (!el.IsVisible || el.ActualHeight <= 0 || !_root.IsAncestorOf(el)) return false;
        var r = el.TransformToAncestor(_root).TransformBounds(new Rect(0, 0, el.ActualWidth, el.ActualHeight));
        r.Inflate(0, pad);
        if (!r.Contains(p)) return false;
        relY = (p.Y - r.Top) / r.Height;
        return true;
    }

    private void SetTarget((string Kind, string Key, int Zone, FrameworkElement El)? t)
    {
        if (_target is { } old && (t is not { } n || n.El != old.El || n.Zone != old.Zone)) Mark(old.Kind, old.El, 0, false);
        _target = t;
        if (t is { } now) Mark(now.Kind, now.El, now.Zone, true);
    }

    /// <summary>놓을 곳 표시: 가운데(합치기)는 테두리 전체, 앞/뒤는 위나 아래 굵은 선.</summary>
    private static void Mark(string kind, FrameworkElement el, int zone, bool on)
    {
        if (el is not Border b) return;
        if (on) b.SetResourceReference(Border.BorderBrushProperty, ThemeKeys.Accent);
        else b.BorderBrush = kind == "new" ? (Brush)Application.Current.FindResource(ThemeKeys.Muted) : Brushes.Transparent;
        b.BorderThickness = kind switch
        {
            "tab" => !on || zone == 0 ? new Thickness(2) : zone < 0 ? new Thickness(0, 4, 0, 0) : new Thickness(0, 0, 0, 4),
            "section" => !on ? new Thickness(0, 2, 0, 2) : zone < 0 ? new Thickness(0, 4, 0, 0) : new Thickness(0, 0, 0, 4),
            "head" => new Thickness(0, 0, 0, 2),
            _ => new Thickness(1.5),
        };
    }

    private void ClearPending() => (_pendKind, _pendId, _pendSource) = (null, null, null);

    private void CancelDrag()
    {
        SetTarget(null);
        if (_pendSource != null) _pendSource.Opacity = 1;
        _ghost.Visibility = Visibility.Collapsed;
        _newZone.Visibility = Visibility.Collapsed;
        bool was = _dragging;
        _dragging = false;
        ClearPending();
        if (was && _root.IsMouseCaptured) _root.ReleaseMouseCapture();
        if (was && _renderPending) Dispatcher.BeginInvoke(FlushRender);
    }

    /// <summary>놓은 결과를 계산해 바뀌었으면 알린다. 옮긴 모듈·합친 묶음이 보이도록 그 탭을 고른다.</summary>
    private void DropAt(string kind, string id, string targetKind, string key, int zone)
    {
        var tabs = _tabList;
        IReadOnlyList<SideTab> next;
        string? show;
        if (kind == "module")
        {
            switch (targetKind)
            {
                case "new": next = SideTabs.NewTab(tabs, id); show = SideTabs.TabOf(next, id); break;
                case "tab": next = SideTabs.MoveModule(tabs, id, key, null); show = key; break;
                case "head": next = SideTabs.MoveModule(tabs, id, key, tabs.First(t => t.Key == key).Members.FirstOrDefault()); show = key; break;
                default:
                {
                    string tabKey = SideTabs.TabOf(tabs, key) ?? "";
                    next = SideTabs.MoveModule(tabs, id, tabKey, zone < 0 ? key : NextMember(tabs, tabKey, key, id));
                    show = tabKey;
                    break;
                }
            }
        }
        else
        {
            switch (targetKind)
            {
                case "tab" when zone < 0: next = SideTabs.MoveTab(tabs, id, key); show = id; break;
                case "tab" when zone > 0: next = SideTabs.MoveTab(tabs, id, NextTab(tabs, key, id)); show = id; break;
                case "tab" or "head": next = SideTabs.MergeTab(tabs, id, key); show = key; break;
                case "section":
                {
                    string into = SideTabs.TabOf(tabs, key) ?? "";
                    next = SideTabs.MergeTab(tabs, id, into, zone < 0 ? key : NextMember(tabs, into, key, null));
                    show = into;
                    break;
                }
                default: return;
            }
        }
        if (Same(tabs, next)) return;
        _activeKey = show ?? _activeKey;
        TabsChanged?.Invoke(next);
    }

    /// <summary>그 탭에서 anchor 바로 다음 모듈(끄는 모듈 자신은 건너뜀). 없으면 null = 맨 아래.</summary>
    private static string? NextMember(IReadOnlyList<SideTab> tabs, string tabKey, string anchor, string? skip)
    {
        var m = tabs.FirstOrDefault(t => t.Key == tabKey)?.Members.Where(x => x != skip).ToList() ?? [];
        int i = m.IndexOf(anchor);
        return i >= 0 && i + 1 < m.Count ? m[i + 1] : null;
    }

    private static string? NextTab(IReadOnlyList<SideTab> tabs, string anchor, string skip)
    {
        var k = tabs.Select(t => t.Key).Where(x => x != skip).ToList();
        int i = k.IndexOf(anchor);
        return i >= 0 && i + 1 < k.Count ? k[i + 1] : null;
    }

    private static bool Same(IReadOnlyList<SideTab> a, IReadOnlyList<SideTab> b) =>
        a.Count == b.Count && a.Zip(b).All(p => p.First.Key == p.Second.Key && p.First.Members.SequenceEqual(p.Second.Members));
}
