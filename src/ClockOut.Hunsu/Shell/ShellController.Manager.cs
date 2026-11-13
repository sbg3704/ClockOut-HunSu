// 기능: 모듈 관리자가 쓰는 동작(ShellController의 일부). 켜기·끄기, 순서 바꾸기, 자리 옮기기, 배치 처음으로, 관리자 창 열기.
// 사용법: ManagerWindow가 shell.SetEnabled / MoveOrder / PlaceFromManager / ResetLayout 을 부르고 shell.ModulesChanged로 목록을 다시 그린다(docs/07).
// 의존: ShellController.Modules.cs(Save, Pin, MoveToDock, RefreshDocks), Core/Layout/OrderPlan, Core/TopicRouting, Shell/ManagerWindow.
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Core.Settings;
using ClockOut.Hunsu.Overlay;

namespace ClockOut.Hunsu.Shell;

internal sealed partial class ShellController
{
    // ---- 모듈 관리자가 쓰는 동작(docs/07) ----

    /// <summary>배치나 켜짐 상태가 바뀌었다(관리자 창이 목록을 다시 그린다).</summary>
    internal event Action? ModulesChanged;

    internal IReadOnlyList<ModuleView> Modules => _modules;

    internal string SettingsPath => SettingsStore.PathFor(_options.LayoutPath);

    internal HunsuSettings Settings => _settings;

    /// <summary>모듈 켜기·끄기. 꺼진 모듈은 도크·핀 창에서 빠지고 엑셀 조회도 하지 않는다. 켤 때는 지금 값을 한 번 채운다.</summary>
    internal void SetEnabled(ModuleView mv, bool enabled)
    {
        Save(mv, p => p with { Enabled = enabled });
        _pins.TryGetValue(mv.Id, out var pinWindow);
        if (enabled)
        {
            if (mv.Placement.Place == Places.Pin && pinWindow == null) Pin(mv, null, restoring: true);
            else if (pinWindow != null && !_hiddenAll) pinWindow.Reappear();
            if (_target != IntPtr.Zero) _registry.Refresh(_target, TopicRouting.AllTopics(mv.Module.Manifest.Data));
        }
        else pinWindow?.Hide();
        RefreshDocks();
        _log.Write("module_enabled", ("id", mv.Id), ("on", enabled));
    }

    /// <summary>모든 모듈을 한 번에 켜거나 끈다(관리자의 [모두 켜기] [모두 끄기]).</summary>
    internal void SetAllEnabled(bool enabled)
    {
        foreach (var mv in _modules.Where(m => m.Placement.Hidden == enabled).ToList()) SetEnabled(mv, enabled);
    }

    /// <summary>같은 자리 모듈들 사이에서 위(-1)나 아래(+1)로 한 칸.</summary>
    internal void MoveOrder(ModuleView mv, int delta)
    {
        var peers = _modules.Where(m => m.Placement.Place == mv.Placement.Place).Select(m => (m.Id, m.Placement.Order));
        var next = OrderPlan.Move(peers, mv.Id, delta);
        if (next == null) return;
        foreach (var m in _modules.Where(m => next.ContainsKey(m.Id)))
        {
            int order = next[m.Id];
            if (m.Placement.Order != order) Save(m, p => p with { Order = order });
        }
        RefreshDocks();
    }

    /// <summary>관리자의 [왼쪽][오른쪽][떼기]: 도크로 옮기거나 핀 창으로 뗀다.</summary>
    internal void PlaceFromManager(ModuleView mv, string place)
    {
        if (place == Places.Pin) { if (mv.InDock) Pin(mv, null); }
        else MoveToDock(mv, place);
        RefreshDocks();
    }

    /// <summary>사용자 배치를 모두 지우고 선언·설정 기본값으로 되돌린다(켜짐 상태 포함).</summary>
    internal void ResetLayout()
    {
        foreach (var mv in _modules.Where(m => _pins.ContainsKey(m.Id)).ToList()) Unpin(mv, null);
        _store.State.Modules.Clear();
        foreach (var mv in _modules)
        {
            var man = mv.Module.Manifest;
            mv.Placement = _store.State.Effective(mv.Id, man.Place, man.Order, man.Shapes, _settings.IsEnabledByDefault(man.Id));
            mv.Frame.SetOpacityLevel(mv.Placement.Opacity);
            mv.ApplyCollapsed(mv.Placement.Collapsed, animate: false, done: null);
        }
        _store.SaveSoon();
        RefreshDocks();
        if (_target != IntPtr.Zero) _registry.Refresh(_target, TopicRouting.AllTopics(_modules.Where(m => !m.Placement.Hidden).SelectMany(m => m.Module.Manifest.Data)));
        _log.Write("layout_reset");
    }

    private IEnumerable<PinWindow> ActivePins() => _pins.Where(kv => !_modules.Any(m => m.Id == kv.Key && m.Placement.Hidden)).Select(kv => kv.Value);

    private void OpenManager()
    {
        if (_manager is { IsLoaded: true }) { _manager.Activate(); return; }
        _manager = new ManagerWindow(this);
        _manager.Show();
        _manager.Activate();
    }
}
