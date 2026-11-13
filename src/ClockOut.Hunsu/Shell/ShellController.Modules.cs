// 기능: 모듈 동작(ShellController의 일부). 모듈 화면 만들기, 데이터 넣기, 접기·펼치기, 투명도, 도크 ↔ 핀 창, 왼쪽·오른쪽 도크 옮기기, 배치 저장, 켜기·끄기, 순서, 명령 보내기.
// 사용법: ShellController 안에서만 쓴다. 바뀐 배치는 모두 _store.State.Update(...) 후 _store.SaveSoon().
// 의존: Shell/ModuleView, Overlay/PinWindow, Overlay/DockWindow, Core/Layout(Places, PinPlacement, LayoutState).
using System.Windows;
using System.Windows.Controls;
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Core.Settings;
using ClockOut.Hunsu.Diagnostics;
using ClockOut.Hunsu.Excel;
using ClockOut.Hunsu.Native;
using ClockOut.Hunsu.Overlay;

namespace ClockOut.Hunsu.Shell;

internal sealed partial class ShellController
{
    private ModuleView CreateView(LoadedModule lm)
    {
        var man = lm.Manifest;
        var mv = new ModuleView(lm, _store.State.Effective(man.Id, man.Place, man.Order, man.Shapes, _settings.IsEnabledByDefault(man.Id)));
        mv.CommandRequested += (command, args) => OnCommand(mv, command, args);
        mv.Frame.PinClicked += () => { if (mv.InDock) Pin(mv, null); else Unpin(mv, null); };
        mv.Frame.CollapseClicked += () => SetCollapsed(mv, true, null);
        mv.Tab.Clicked += () => SetCollapsed(mv, false, null);
        mv.Frame.OpacityClicked += () =>
        {
            int next = OpacityLevels.Next(mv.Frame.OpacityLevel);
            mv.Frame.SetOpacityLevel(next);
            Save(mv, p => p with { Opacity = next });
        };
        mv.Frame.ContextMenu = SideMenu(mv);
        return mv;
    }

    private ContextMenu SideMenu(ModuleView mv)
    {
        var menu = new ContextMenu();
        var left = new MenuItem { Header = "왼쪽 도크로" };
        var right = new MenuItem { Header = "오른쪽 도크로" };
        left.Click += (_, _) => MoveToDock(mv, Places.EdgeLeft);
        right.Click += (_, _) => MoveToDock(mv, Places.EdgeRight);
        menu.Items.Add(left);
        menu.Items.Add(right);
        menu.Opened += (_, _) => { left.IsEnabled = mv.Placement.Place != Places.EdgeLeft; right.IsEnabled = mv.Placement.Place != Places.EdgeRight; };
        return menu;
    }

    /// <summary>저장된 배치대로 모든 모듈을 도크나 핀 창에 놓는다.</summary>
    private void PlaceAll()
    {
        foreach (var mv in _modules.Where(m => m.Placement.Place == Places.Pin && !m.Placement.Hidden)) Pin(mv, null, restoring: true);
        RefreshDocks();
    }

    private void RefreshDocks()
    {
        _right.SetModules(_modules.Where(m => m.Placement.Place == Places.EdgeRight && !m.Placement.Hidden));
        _left.SetModules(_modules.Where(m => m.Placement.Place == Places.EdgeLeft && !m.Placement.Hidden));
        UpdateLeftVisibility();
        bool allCollapsed = _modules.Where(m => m.InDock && !m.Placement.Hidden).All(m => m.Placement.Collapsed);
        _right.SetAllCollapsedLabel(allCollapsed);
        ModulesChanged?.Invoke();
    }

    private void Save(ModuleView mv, Func<ModulePlacement, ModulePlacement> change)
    {
        _store.State.Update(mv.Id, change);
        var man = mv.Module.Manifest;
        mv.Placement = _store.State.Effective(mv.Id, man.Place, man.Order, man.Shapes, _settings.IsEnabledByDefault(man.Id));
        _store.SaveSoon();
        LogPlacement(mv);
    }

    private void LogPlacement(ModuleView mv) =>
        _log.Write("module_placed", ("id", mv.Id), ("place", mv.Placement.Place), ("collapsed", mv.Placement.Collapsed), ("opacity", mv.Placement.Opacity),
            ("rect", _pins.TryGetValue(mv.Id, out var w) ? w.Rect.ToString() : "-"));

    internal void SetCollapsed(ModuleView mv, bool collapsed, Action<long>? done)
    {
        if (!mv.InDock) { done?.Invoke(0); return; }
        Save(mv, p => p with { Collapsed = collapsed });
        mv.ApplyCollapsed(collapsed, animate: !collapsed, done);
        (mv.Placement.Place == Places.EdgeLeft ? _left : _right).Relayout();
        RefreshDocks();
    }

    private void ToggleAllCollapsed()
    {
        var docked = _modules.Where(m => m.InDock && !m.Placement.Hidden).ToList();
        bool expand = docked.All(m => m.Placement.Collapsed);
        foreach (var m in docked) SetCollapsed(m, !expand, null);
    }

    private void MoveToDock(ModuleView mv, string side)
    {
        if (!mv.InDock) Unpin(mv, null);
        Save(mv, p => p with { Place = side });
        RefreshDocks();
    }

    /// <summary>도크에서 떼어 핀 창으로. 저장된 자리가 있으면 거기(화면 밖이면 안으로), 없으면 도크 패널 왼쪽 옆.</summary>
    internal void Pin(ModuleView mv, Action<long>? done, bool restoring = false)
    {
        if (_pins.ContainsKey(mv.Id)) { done?.Invoke(0); return; }
        var dock = mv.Placement.Place == Places.EdgeLeft ? _left : _right;
        var rect = mv.Placement.PinRect ?? DefaultPinRect(mv, dock);
        rect = PinPlacement.KeepOnScreen(rect, WorkAreas());
        var w = PinWindowFor(mv);
        w.SetOwner(_target);
        w.SetInteractive(_summoned);
        w.Host(mv.DetachFrame());
        _pins[mv.Id] = w;
        if (!restoring) Save(mv, p => p with { Place = Places.Pin, PinRect = rect });
        // 그림은 나타나는 움직임이 끝난 뒤에 찍는다(첫 프레임은 투명도 0이라 빈 그림).
        Action<long> shown = t => { done?.Invoke(t); if (!restoring) Snap("pin.png", w); };
        if (!_hiddenAll && _target != IntPtr.Zero) w.ShowAt(rect, shown);
        else { w.SetRectOnly(rect); done?.Invoke(0); }
        RefreshDocks();
        LogPlacement(mv);
    }

    /// <summary>모듈마다 핀 창을 한 번만 만든다(처음 뗄 때 빨리 뜨게 시작할 때 미리 만듦).</summary>
    private PinWindow PinWindowFor(ModuleView mv)
    {
        if (_pinWindows.TryGetValue(mv.Id, out var w)) return w;
        w = new PinWindow(_target);
        w.Moved += r => Save(mv, p => p with { PinRect = r });
        w.Prepare();
        _pinWindows[mv.Id] = w;
        return w;
    }

    /// <summary>핀 창을 닫고 패널을 원래 도크(선언 자리가 도크면 그쪽, 아니면 오른쪽)로 되돌린다.</summary>
    internal void Unpin(ModuleView mv, Action<long>? done)
    {
        if (!_pins.Remove(mv.Id, out var w)) { done?.Invoke(0); return; }
        w.Release();
        string side = Places.IsDock(mv.Module.Manifest.Place) ? mv.Module.Manifest.Place : Places.EdgeRight;
        Save(mv, p => p with { Place = side, Collapsed = false });
        mv.AttachFrameToSlot();
        RefreshDocks();
        mv.ApplyCollapsed(false, animate: true, done);
        if (_summoned) mv.Frame.SetSummoned(true);
    }

    private PixelRect DefaultPinRect(ModuleView mv, DockWindow dock)
    {
        var win = User32.WindowBounds(dock.Handle);
        var local = dock.PanelRect(mv);
        var onScreen = local.IsEmpty ? new PixelRect(win.X, win.Y, win.Width, 240) : local with { X = win.X + local.X, Y = win.Y + local.Y };
        double scale = System.Windows.PresentationSource.FromVisual(dock)?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        int w = (int)((double)Application.Current.FindResource("Hunsu.WindowMinWidth") * scale) + 24;
        int h = Math.Max((int)((double)Application.Current.FindResource("Hunsu.WindowMinHeight") * scale), onScreen.Height) + 24;
        return PinPlacement.Default(onScreen, w, h);
    }

    private static IReadOnlyList<PixelRect> WorkAreas() =>
        [.. System.Windows.Forms.Screen.AllScreens.Select(s => new PixelRect(s.WorkingArea.X, s.WorkingArea.Y, s.WorkingArea.Width, s.WorkingArea.Height))];

    private void ForTopic(string topic, Action<ModuleView> act)
    {
        foreach (var m in _modules.Where(m => !m.Placement.Hidden && m.Wants(topic))) act(m);
    }

    /// <summary>조회 결과를 그 주제를 구독한(켜진) 모듈에 넣는다. 실패하면 마지막 내용을 흐리게 둔다(docs/04 6장).</summary>
    private void OnTopic(TopicResult r)
    {
        if (r.Response.Ok) ForTopic(r.Topic, m => { m.SetData(r.Topic, r.Response.Data); m.SetStale(false); });
        else ForTopic(r.Topic, m => m.SetStale(true));
    }

    /// <summary>다리 신호 하나가 왔을 때 다시 조회할 주제: 켜진 모듈의 구독만(꺼진 모듈은 엑셀을 바쁘게 하지 않는다).</summary>
    private IReadOnlyList<string> TopicsForSignal(int code) =>
        TopicRouting.TopicsFor(code, _modules.Where(m => !m.Placement.Hidden).SelectMany(m => m.Module.Manifest.Data));

    private IReadOnlyList<string> AllEnabledTopics() =>
        TopicRouting.AllTopics(_modules.Where(m => !m.Placement.Hidden).SelectMany(m => m.Module.Manifest.Data));

    /// <summary>단추나 목록 줄을 눌렀다. 모듈 선언의 commands에 있는 이름만 다리로 보낸다(없으면 거절하고 기록).</summary>
    private async void OnCommand(ModuleView mv, string command, string argsJson)
    {
        if (!mv.Module.Manifest.Commands.Contains(command))
        {
            _log.Write("command_denied", ("module", mv.Id), ("cmd", command));
            return;
        }
        if (_target == IntPtr.Zero) return;
        var call = await _registry.DoAsync(_target, command, argsJson);
        var resp = call.Ok ? BridgeResponse.Parse(call.Json) : BridgeResponse.Failed(call.ErrorCode, "엑셀 호출 실패");
        _log.Write("command", ("module", mv.Id), ("cmd", command), ("ok", resp.Ok), ("code", resp.Code), ("hr", HResultNames.Name(call.HResult)), ("ms", Math.Round(call.Ms, 1)));
    }

    private void OnSummary(SummaryResult r)
    {
        Frames.Next(frame =>
        {
            _log.Write("render", ("excel", r.Excel.ToInt64()), ("seq", r.Seq), ("recv", r.SignalAt), ("qs", r.QueryStart), ("qd", r.QueryDone), ("frame", frame),
                ("ok", r.Response.Ok), ("code", r.Response.Code), ("hr", HResultNames.Name(r.Call.HResult)), ("retries", r.Call.Retries),
                ("addr", r.Summary?.Addr), ("count", r.Summary?.Count), ("attach", r.Call.Attach));
            // 측정 도구가 앱을 다시 켜도 첫 앱의 그림을 덮지 않는다.
            if (r.Summary != null && !_summarySnapped) { _summarySnapped = true; if (_options.SnapshotDir != null && !System.IO.File.Exists(System.IO.Path.Combine(_options.SnapshotDir, "dock_summary.png"))) Snap("dock_summary.png", _right.Root); }
        });
    }

    private bool _summarySnapped;
}
