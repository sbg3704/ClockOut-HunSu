// 기능: 모듈 관리자가 쓰는 동작(ShellController의 일부). 켜기·끄기, 순서 바꾸기, 자리 옮기기, 배치 처음으로, 도크 모양 바꾸기, 관리자 창 열기.
// 사용법: ManagerWindow가 shell.SetEnabled / MoveOrder / PlaceFromManager / ResetLayout 을 부르고 shell.ModulesChanged로 목록을 다시 그린다(docs/07).
// 의존: ShellController.Modules.cs(Save, Pin, MoveToDock, RefreshDocks), Core/Layout/OrderPlan, Core/TopicRouting, Shell/ManagerWindow.
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Ui.Frame;
using ClockOut.Hunsu.Core.Settings;
using ClockOut.Hunsu.Overlay;
using ClockOut.Hunsu.Ui.Controls;

namespace ClockOut.Hunsu.Shell;

internal sealed partial class ShellController
{
    // ---- 모듈 관리자가 쓰는 동작(docs/07) ----

    /// <summary>배치나 켜짐 상태가 바뀌었다(관리자 창이 목록을 다시 그린다).</summary>
    internal event Action? ModulesChanged;

    internal IReadOnlyList<ModuleView> Modules => _modules;

    internal string SettingsPath => SettingsStore.PathFor(_options.LayoutPath);

    internal HunsuSettings Settings => _settings;

    /// <summary>도크 모양이 바뀌었다(설정 창 [모양]이 미리보기를 다시 그린다).</summary>
    internal event Action? LookChanged;

    /// <summary>설정 창 [모양]에서 고른 값: 두 도크에 바로 반영하고 settings.json에 쓴다. 묶음·순서·켜짐은 그대로다.</summary>
    internal void ApplyLook(DockLook look)
    {
        var next = _settings.WithLook(look);
        if (next.Look == _settings.Look) return;
        _settings = next;
        _right.ApplyLook(next);
        _left.ApplyLook(next);
        SettingsStore.Save(SettingsPath, next, _log);
        _log.Write("launcher", ("shape", next.DockShape), ("groups", next.DockGroups), ("size", next.DockSize), ("icons", next.IconProfile), ("idle", next.IdleOpacity));
        LookChanged?.Invoke();
    }

    /// <summary>일반 설정이 바뀌었다(설정 창 [일반]이 다시 그린다).</summary>
    internal event Action? SettingsChanged;

    /// <summary>설정 창 [일반]에서 고른 값 하나를 settings.json에 쓰고 바로 쓸 수 있는 것은 반영한다(목록 줄 수). 범위 밖 값은 기본값으로 되돌려진다.</summary>
    internal void ApplySetting(string key, object value)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(_settings.ToJson())!;
        node[key] = System.Text.Json.JsonSerializer.SerializeToNode(value);
        var next = HunsuSettings.FromJson(node.ToJsonString());
        _settings = next;
        Parts.ListMaxRows = next.ListMaxRows;
        PanelFrame.SharpenOnHover = next.HoverSharpen;
        PanelFrame.MaxBodyHeight = next.PanelMaxHeight;
        UpdateQuickBar();
        if (key == "hudAnchor")
        {
            _activeCell = null;
            RefreshCellHud();
            if (_target != IntPtr.Zero) _registry.Refresh(_target, AllEnabledTopics());
        }
        foreach (var mv in _modules) mv.Frame.ApplyBodyLimit(!mv.InDock);
        SettingsStore.Save(SettingsPath, next, _log);
        OnMarkSettingChanged(key);
        _log.Write("setting", ("key", key), ("value", value.ToString() ?? ""));
        SettingsChanged?.Invoke();
        ModulesChanged?.Invoke();
    }

    /// <summary>일반 설정(모양 제외)을 처음 값으로 되돌린다. 도크 모양은 그대로 둔다.</summary>
    internal void ResetGeneralSettings()
    {
        var d = new HunsuSettings().WithLook(_settings.Look);
        _settings = d;
        Parts.ListMaxRows = d.ListMaxRows;
        PanelFrame.SharpenOnHover = d.HoverSharpen;
        PanelFrame.MaxBodyHeight = d.PanelMaxHeight;
        foreach (var mv in _modules) mv.Frame.ApplyBodyLimit(!mv.InDock);
        SettingsStore.Save(SettingsPath, d, _log);
        SettingsChanged?.Invoke();
    }

    internal void OpenSettingsFile()
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = SettingsPath, UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or System.IO.FileNotFoundException) { /* 열 프로그램이 없으면 아무 일도 하지 않는다 */ }
    }

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

    /// <summary>작업 프로필 적용: 프로필의 모듈은 켜고 순서대로 왼쪽 도크(떼어 둔 것은 그대로)에, 나머지는 끈다. 켜고 끄기는 설정 창 [모듈]에서 언제든 바꿀 수 있다.</summary>
    internal void ApplyProfile(string id)
    {
        var profile = Profiles.Find(id);
        if (profile == null) return;
        var plan = Profiles.Plan(profile, _modules.Select(m => m.Id));
        foreach (var mv in _modules)
        {
            if (!plan.TryGetValue(mv.Id, out var step)) continue;
            if (step.Order is int order)
            {
                Save(mv, p => p with { Order = order, Place = mv.InDock ? Places.EdgeLeft : p.Place });
            }
            if (mv.Placement.Hidden == step.Enabled) SetEnabled(mv, step.Enabled);
        }
        RefreshDocks();
        ApplySetting("profile", profile.Id);
        _log.Write("profile", ("id", profile.Id));
    }

    /// <summary>패널 투명도를 한 단계 바꾼다(제목줄 단추와 설정 창 ◐ 단추가 같이 씀). 5단계를 돌아 처음으로 간다.</summary>
    internal void CycleOpacity(ModuleView mv)
    {
        int next = OpacityLevels.Next(mv.Frame.OpacityLevel);
        mv.Frame.SetOpacityLevel(next);
        Save(mv, p => p with { Opacity = next });
        ModulesChanged?.Invoke();
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

    /// <summary>설정 창을 연다(page: "modules" | "look"). 이미 열려 있으면 그 쪽으로 넘기고 앞으로 가져온다.</summary>
    private void OpenManager(string page = "modules")
    {
        if (_manager is { IsLoaded: true }) { _manager.ShowPage(page); _manager.Activate(); return; }
        _manager = new ManagerWindow(this, page);
        _manager.Show();
        _manager.Activate();
    }
}
