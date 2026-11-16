// 기능: 빠른 띠와 커서 옆 HUD(ShellController의 일부). 빠른 띠는 켜진 도크 모듈 중 앞의 6개 아이콘을 엑셀 위에 띄워 눌러서 패널을 열고 닫게 하고, 커서 옆 HUD는 범위를 끄는 동안 합계를 커서 옆에 보인다.
// 사용법: 생성자가 CreateQuick()을, Follow()가 AttachQuick()을, RefreshDocks·설정 바뀜이 UpdateQuickBar()를, OnSummary가 ShowCursorHud()를 부른다. 자리는 LayoutState.Docks의 "quickX"·"quickY"(엑셀 창 안 비율).
// 의존: Overlay/QuickBar, Overlay/CursorHud, Overlay/WindowFollower, Core/Layout(LayoutState), Core/SelectionSummary. 프로필을 적용하면 켜진 모듈과 순서가 바뀌어 띠도 따라 바뀐다.
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Native;
using ClockOut.Hunsu.Overlay;

namespace ClockOut.Hunsu.Shell;

internal sealed partial class ShellController
{
    private const int QuickMax = 6;
    private const double QuickDefaultX = 0.5, QuickDefaultY = 0.92;

    private QuickBar? _quick;
    private WindowFollower? _quickFollower;
    private CursorHud? _cursorHud;
    private DateTime _lastDragSeen = DateTime.MinValue;

    private void CreateQuick()
    {
        _quick = new QuickBar(_settings.IconProfile);
        _quick.Prepare();
        _quick.ItemClicked += OnModuleIcon;
        _quick.TargetBounds = () => _target == IntPtr.Zero ? default : User32.VisibleBounds(_target);
        _quick.Moved += (rx, ry) =>
        {
            _store.State.Docks["quickX"] = rx;
            _store.State.Docks["quickY"] = ry;
            _store.SaveSoon();
            _quickFollower?.Sync("move");
            _log.Write("quick_moved", ("x", Math.Round(rx, 3)), ("y", Math.Round(ry, 3)));
        };
    }

    private PixelRect QuickPlace(PixelRect t, int w, int h)
    {
        double rx = _store.State.Docks.TryGetValue("quickX", out var x) ? Math.Clamp(x, 0, 1) : QuickDefaultX;
        double ry = _store.State.Docks.TryGetValue("quickY", out var y) ? Math.Clamp(y, 0, 1) : QuickDefaultY;
        return new PixelRect(t.X + (int)Math.Round(rx * Math.Max(0, t.Width - w)), t.Y + (int)Math.Round(ry * Math.Max(0, t.Height - h)), w, h);
    }

    /// <summary>엑셀 창을 따라가는 띠 추적기를 만든다(Follow가 부름). 만든 추적기는 _followers에도 넣는다.</summary>
    private WindowFollower? AttachQuick(IntPtr target, Action<WindowFollower> add)
    {
        if (_quick == null) return null;
        _quickFollower = new WindowFollower(_quick.Handle, target, _log, QuickPlace, null);
        add(_quickFollower);
        return _quickFollower;
    }

    /// <summary>띠에 넣을 칸(켜진 도크 모듈 앞 6개)을 다시 넣고, 보일지 정한다.</summary>
    private void UpdateQuickBar()
    {
        if (_quick == null) return;
        var items = _modules.Where(m => m.InDock && !m.Placement.Hidden).OrderBy(m => m.Placement.Order).Take(QuickMax)
            .Select(m => new QuickItem(m.Id, m.Module.Manifest.Name, m.Module.Manifest.Icon, m.ShowsPanel)).ToList();
        _quick.SetItems(items);
        bool show = _settings.QuickBar && items.Count > 0;
        if (show && !_quick.IsVisible) _quick.Show();
        if (_quickFollower == null) return;
        _quickFollower.Suspended = _hiddenAll || !show;
        _quickFollower.Sync(show ? "show" : "empty");
    }

    private SelectionSummary? _lastSummary;
    private PixelRect? _activeCell;

    private static string HudText(SelectionSummary s)
    {
        string text = s.Sum is double sum ? $"합계 {sum:#,##0.##} · {s.NonEmpty:n0}개" : $"{s.Count:n0}칸";
        if (s.Errors > 0) text += $" · 오류 {s.Errors:n0}";
        return text;
    }

    /// <summary>HudAnchor가 "cell"일 때 mark 조회를 함께 받는다(고른 칸 자리를 알려고). 켜진 모듈의 구독에 더해 신호마다 다시 묻는다.</summary>
    private IEnumerable<ClockOut.Hunsu.Core.Manifest.DataSubscription> ExtraSubscriptions() =>
        _settings.CursorHud && _settings.HudAnchor == "cell"
            ? [new ClockOut.Hunsu.Core.Manifest.DataSubscription("mark", ["sel", "change", "sheet", "calc"], "normal")]
            : [];

    /// <summary>mark 조회 결과에서 고른 칸 자리를 읽어 두고 HUD를 갱신한다.</summary>
    private void OnMarkForHud(System.Text.Json.JsonElement data)
    {
        if (_settings.HudAnchor != "cell") return;
        _activeCell = data.ValueKind == System.Text.Json.JsonValueKind.Object && data.TryGetProperty("active", out var a)
            ? Core.Marks.MarkGeometry.Read(a).Cast<PixelRect?>().FirstOrDefault() : null;
        RefreshCellHud();
    }

    /// <summary>고른 칸 옆 HUD: 여러 칸을 골랐을 때만 그 칸 오른쪽에 합계를 보인다. 아니면 숨긴다.</summary>
    private void RefreshCellHud()
    {
        if (_settings.HudAnchor != "cell" || !_settings.CursorHud || _hiddenAll || _target == IntPtr.Zero || _lastSummary is not { Count: > 1 } s || _activeCell is not { } c)
        {
            _cursorHud?.HideNow();
            return;
        }
        (_cursorHud ??= new CursorHud()).ShowAt(HudText(s), c.X + c.Width + 6, c.Y);
    }

    /// <summary>범위를 끄는 동안(또는 놓은 직후) 합계를 커서 옆에 보인다. "cell" 모드면 고른 칸 옆 HUD를 갱신한다.</summary>
    private void ShowCursorHud(SelectionSummary? s)
    {
        if (_settings.HudAnchor == "cell") { _lastSummary = s; RefreshCellHud(); return; }
        if (s == null || !_settings.CursorHud || _hiddenAll || _target == IntPtr.Zero) return;
        var now = DateTime.UtcNow;
        if (User32.LeftButtonDown()) _lastDragSeen = now;
        else if (now - _lastDragSeen > TimeSpan.FromMilliseconds(700)) return;
        (_cursorHud ??= new CursorHud()).Show(HudText(s));
    }

    private readonly HashSet<string> _formApplied = new(StringComparer.Ordinal);

    /// <summary>선언의 form이 "hud"이고 아직 사용자가 자리를 정한 적 없는 모듈은 처음부터 반투명 떼어 낸 창으로 엑셀 시트 오른쪽 위에 띄운다(한 번만).</summary>
    private void ApplyDefaultForms()
    {
        if (_target == IntPtr.Zero) return;
        foreach (var mv in _modules)
        {
            if (mv.Module.Manifest.Form != "hud" || mv.Placement.Hidden || _formApplied.Contains(mv.Id) || _store.State.Modules.ContainsKey(mv.Id)) continue;
            _formApplied.Add(mv.Id);
            var excel = User32.VisibleBounds(_target);
            double scale = System.Windows.PresentationSource.FromVisual(_right)?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            int w = (int)(250 * scale), h = (int)(170 * scale);
            var rect = new PixelRect(excel.Right - w - (int)(24 * scale), excel.Y + (int)(200 * scale), w, h);
            Save(mv, p => p with { Place = Places.Pin, PinRect = rect, Opacity = 2 });
            mv.Frame.SetOpacityLevel(2);
            Pin(mv, null, restoring: true);
            _log.Write("form_hud", ("id", mv.Id));
        }
        RefreshDocks();
    }
}
