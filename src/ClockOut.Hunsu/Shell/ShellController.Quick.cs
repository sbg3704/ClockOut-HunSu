// 기능: 빠른 띠와 모듈 기본 모양 적용(ShellController의 일부). 빠른 띠는 켜진 도크 모듈 중 앞의 6개 아이콘을 엑셀 위에 띄워 눌러서 패널을 열고 닫게 한다. 선언의 form이 hud인 모듈은 처음부터 떼어 낸 반투명 창으로 띄운다.
// 사용법: 생성자가 CreateQuick()을, Follow()가 AttachQuick()을, RefreshDocks·설정 바뀜이 UpdateQuickBar()를, RefreshDocks가 ApplyDefaultForms()를 부른다. 자리는 LayoutState.Docks의 "quickX"·"quickY"(엑셀 창 안 비율). 커서·고른 칸 옆 글 HUD는 ShellController.Hud.cs.
// 의존: Overlay/QuickBar, Overlay/WindowFollower, Core/Layout(LayoutState), 테마의 Hunsu.Hud* 크기. 프로필을 적용하면 켜진 모듈과 순서가 바뀌어 띠도 따라 바뀐다.
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

    private void CreateQuick()
    {
        _quick = new QuickBar(_settings.IconProfile);
        _quick.Prepare();
        _quick.ItemClicked += OnModuleIcon;
        _quick.ItemMenu = ModuleIconMenu;
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
            .Select(m => new QuickItem(m.Id, m.Module.Manifest.Name, m.Module.Manifest.Icon, m.ShowsPanel, IsModuleWorking(m))).ToList();
        _quick.SetItems(items);
        bool show = _settings.QuickBar && items.Count > 0;
        if (show && !_quick.IsVisible) _quick.Show();
        if (_quickFollower == null) return;
        _quickFollower.Suspended = _hiddenAll || !show;
        _quickFollower.Sync(show ? "show" : "empty");
    }

    private readonly HashSet<string> _formApplied = new(StringComparer.Ordinal);

    /// <summary>선언의 form이 "hud"이고 아직 사용자가 자리를 정한 적 없는 모듈은 처음부터 반투명 떼어 낸 창으로 엑셀 시트 오른쪽 위에 띄운다(한 번만).</summary>
    private void ApplyDefaultForms()
    {
        if (_target == IntPtr.Zero) return;
        ApplyMiniForms();
        foreach (var mv in _modules)
        {
            if (mv.Module.Manifest.Form != "hud" || mv.Placement.Hidden || _formApplied.Contains(mv.Id) || _store.State.Modules.ContainsKey(mv.Id)) continue;
            _formApplied.Add(mv.Id);
            var excel = User32.VisibleBounds(_target);
            double scale = System.Windows.PresentationSource.FromVisual(_right)?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            double Token(string key) => (double)System.Windows.Application.Current.FindResource(key);
            var size = mv.Module.Manifest.Size; // 모듈이 선언한 크기, 없으면 테마 기본
            int w = (int)((size?.Width ?? Token("Hunsu.HudWidth")) * scale), h = (int)((size?.Height ?? Token("Hunsu.HudHeight")) * scale);
            var rect = new PixelRect(excel.Right - w - (int)(Token("Hunsu.HudRightGap") * scale), excel.Y + (int)(Token("Hunsu.HudTop") * scale), w, h);
            Save(mv, p => p with { Place = Places.Pin, PinRect = rect, Opacity = 2 });
            mv.Frame.SetOpacityLevel(2);
            Pin(mv, null, restoring: true);
            _log.Write("form_hud", ("id", mv.Id));
        }
        RefreshDocks();
    }
}
