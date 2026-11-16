// 기능: 편집 모드(ShellController의 일부). Ctrl+Alt+H로 들어가면 패널을 끌어 옮기고 자물쇠로 잠그며, 나갈 때 [저장하고 나가기] 또는 [버리고 나가기]로 들어오기 전 배치로 돌아갈 수 있다(docs/10, EllesmereUI의 저장·버리기 세션과 같은 방식).
// 사용법: ToggleSummon()이 들어가기·저장하고 나가기를 맡는다. 엑셀 위쪽의 EditBar 단추가 SaveAndExit / DiscardAndExit를 부른다.
// 의존: Overlay/EditBar, Shell/ShellController.Modules(Pin, RefreshDocks, WorkAreas), Core/Layout(LayoutState, PinPlacement). 들어올 때의 배치를 글로 붙잡아 두고 버릴 때 되돌려 놓는다.
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Native;
using ClockOut.Hunsu.Overlay;

namespace ClockOut.Hunsu.Shell;

internal sealed partial class ShellController
{
    private EditBar? _editBar;
    private string? _editSnapshot;

    /// <summary>편집 모드를 켜고 끈다. 끌 때는 지금 배치를 저장한다.</summary>
    public void ToggleSummon()
    {
        if (_summoned) ExitEdit(save: true);
        else EnterEdit();
    }

    internal void SaveAndExit() { if (_summoned) ExitEdit(save: true); }

    internal void DiscardAndExit() { if (_summoned) ExitEdit(save: false); }

    private void EnterEdit()
    {
        _summoned = true;
        _editSnapshot = _store.State.ToJson();
        if (_hiddenAll) ToggleHideAll();
        ApplySummoned();
        if (_editBar == null)
        {
            _editBar = new EditBar(_target);
            _editBar.SaveClicked += SaveAndExit;
            _editBar.DiscardClicked += DiscardAndExit;
        }
        _editBar.SetOwner(_target);
        if (_target != IntPtr.Zero) _editBar.ShowOver(User32.VisibleBounds(_target));
    }

    private void ExitEdit(bool save)
    {
        if (!save) RestoreSnapshot();
        _summoned = false;
        _editBar?.Hide();
        ApplySummoned();
        if (save) _store.Flush();
        _editSnapshot = null;
        _log.Write("edit_exit", ("save", save));
    }

    private void ApplySummoned()
    {
        foreach (var p in _pins.Values) p.SetInteractive(_summoned);
        foreach (var m in _modules.Where(m => m.InDock)) m.Frame.SetSummoned(_summoned);
        if (_quick != null) _quick.EditMode = _summoned;
        _left.SetEditMode(_summoned);
        _right.SetEditMode(_summoned);
        _log.Write("summon", ("on", _summoned), ("pins", _pins.Count));
    }

    /// <summary>[버리고 나가기]: 들어올 때의 배치 글을 되살리고 모듈마다 자리, 크기, 투명도, 잠금, 접힘을 그대로 맞춘다.</summary>
    private void RestoreSnapshot()
    {
        var (snap, ok) = LayoutState.FromJson(_editSnapshot);
        if (!ok) return;
        _store.State.Modules.Clear();
        foreach (var kv in snap.Modules) _store.State.Modules[kv.Key] = kv.Value;
        _store.State.Docks.Clear();
        foreach (var kv in snap.Docks) _store.State.Docks[kv.Key] = kv.Value;
        _left.SetCenterRatio(_store.State.DockRatio(Places.EdgeLeft));
        _right.SetCenterRatio(_store.State.DockRatio(Places.EdgeRight));
        _quickFollower?.Sync("move");
        foreach (var mv in _modules)
        {
            var man = mv.Module.Manifest;
            bool pinned = _pins.ContainsKey(mv.Id);
            var eff = _store.State.Effective(mv.Id, man.Place, man.Order, man.Shapes, _settings.IsEnabledByDefault(man.Id));
            if (pinned && eff.Place != Places.Pin && _pins.Remove(mv.Id, out var gone))
            {
                gone.Release();
                mv.AttachFrameToSlot();
            }
            mv.Placement = eff;
            mv.Frame.SetOpacityLevel(eff.Opacity);
            mv.Frame.SetLocked(eff.Locked);
            if (_pinWindows.TryGetValue(mv.Id, out var pw)) pw.Locked = eff.Locked;
            if (eff.Place == Places.Pin && !eff.Hidden)
            {
                if (!pinned) Pin(mv, null, restoring: true);
                else if (_pins.TryGetValue(mv.Id, out var live) && eff.PinRect is PixelRect r) live.MoveTo(PinPlacement.KeepOnScreen(r, WorkAreas()));
            }
            if (mv.InDock) mv.ApplyCollapsed(eff.Collapsed, animate: false, done: null);
        }
        if (_pins.Count == 0) _pinHover?.Stop();
        RefreshDocks();
        _store.SaveSoon();
    }

    /// <summary>훈 원을 놓았다: 세로 위치를 저장하고, 엑셀 창의 반대쪽 절반에 놓았으면 그 도크의 모듈을 모두 반대편 도크로 옮긴다.</summary>
    private void OnHubDropped(string side, double ratio, int cursorX)
    {
        var from = side == Places.EdgeLeft ? _left : _right;
        string target = side;
        if (_target != IntPtr.Zero && cursorX != int.MinValue)
        {
            var b = User32.VisibleBounds(_target);
            int mid = b.X + b.Width / 2;
            target = cursorX < mid ? Places.EdgeLeft : Places.EdgeRight;
        }
        _store.State.Docks[target] = ratio;
        var to = target == Places.EdgeLeft ? _left : _right;
        to.SetCenterRatio(ratio);
        if (target != side)
        {
            from.SetCenterRatio(_store.State.DockRatio(side));
            foreach (var mv in _modules.Where(m => m.InDock && m.Placement.Place == side).ToList())
                Save(mv, p => p with { Place = target });
            RefreshDocks();
        }
        _store.SaveSoon();
        _log.Write("hub_drop", ("from", side), ("to", target), ("ratio", Math.Round(ratio, 3)));
    }

    /// <summary>패널의 자물쇠 단추: 이 모듈의 자리를 잠그거나 푼다.</summary>
    private void ToggleLock(ModuleView mv)
    {
        Save(mv, p => p with { Locked = !p.Locked });
        mv.Frame.SetLocked(mv.Placement.Locked);
        if (_pinWindows.TryGetValue(mv.Id, out var pw)) pw.Locked = mv.Placement.Locked;
    }
}
