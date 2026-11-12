// 기능: 훈수꾼 틀의 연결부. 도크 창(왼·오), 핀 창, 모듈, 배치 저장, 신호·조회, 단축키, 알림 영역 아이콘을 서로 잇는다.
//       따라갈 엑셀 창을 정하고(보통: 보이는 XLMAIN, 앞으로 나온 엑셀로 바꿈 / 측정: --follow), 부르기·전부 숨김 상태를 맡는다.
// 사용법: App.OnStartup에서 new ShellController(options).Start();  끝낼 때 Dispose()
// 의존: Overlay/*, Shell/*, Excel/InstanceRegistry, Signal/SignalWindow, Diagnostics/*. 모듈 동작은 ShellController.Modules.cs, 측정 메시지는 ShellController.Bench.cs.
using System.IO;
using System.Windows;
using System.Windows.Threading;
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Diagnostics;
using ClockOut.Hunsu.Excel;
using ClockOut.Hunsu.Native;
using ClockOut.Hunsu.Overlay;
using ClockOut.Hunsu.Signal;

namespace ClockOut.Hunsu.Shell;

internal sealed partial class ShellController : IDisposable
{
    private readonly AppOptions _options;
    private readonly RunLog _log;
    private readonly LayoutStore _store;
    private readonly SignalWindow _signal;
    private readonly InstanceRegistry _registry;
    private readonly HotkeyService _hotkeys;
    private readonly TrayIcon _tray;
    private readonly DockWindow _right = new(Places.EdgeRight);
    private readonly DockWindow _left = new(Places.EdgeLeft);
    private readonly List<ModuleView> _modules = [];
    private readonly Dictionary<string, PinWindow> _pins = new(StringComparer.Ordinal); // 지금 떼어 낸 것
    private readonly Dictionary<string, PinWindow> _pinWindows = new(StringComparer.Ordinal); // 모듈마다 미리 만들어 둔 창
    private readonly List<WindowFollower> _followers = [];
    private DispatcherTimer? _finder;
    private IntPtr _target;
    private bool _summoned;
    private bool _hiddenAll;

    public ShellController(AppOptions options)
    {
        _options = options;
        _log = new RunLog(options.LogPath);
        _log.Write("start", ("pid", Environment.ProcessId), ("badArgs", options.Errors.Count));
        _store = new LayoutStore(options.LayoutPath, _log);
        _registry = new InstanceRegistry(_log);
        _registry.SummaryReady += OnSummary;
        _registry.Slow += _ => ForTopic("selection.summary", m => m.SetStale(true));
        _signal = new SignalWindow(_log);
        _signal.Signal += (hwnd, code, seq, at) =>
        {
            _log.Write("signal", ("excel", hwnd.ToInt64()), ("code", SignalCodec.NameOf(code)), ("seq", seq), ("recv", at));
            _registry.OnSignal(hwnd, code, seq, at);
        };
        _signal.Bench += OnBench;
        _hotkeys = new HotkeyService(_signal.Handle, _log);
        _signal.Hotkey += _hotkeys.OnHotkey;
        _hotkeys.Summon += ToggleSummon;
        _hotkeys.HideAll += ToggleHideAll;
        _hotkeys.ForegroundChanged += OnForeground;
        _tray = new TrayIcon();
        _tray.SummonClicked += ToggleSummon;
        _tray.HideAllClicked += ToggleHideAll;
        _tray.QuitClicked += Quit;
        foreach (var d in new[] { _right, _left })
        {
            d.PillClicked += ToggleAllCollapsed;
            d.ToggleAllRequested += ToggleAllCollapsed;
            d.SummonRequested += ToggleSummon;
            d.HideAllRequested += ToggleHideAll;
            d.QuitRequested += Quit;
            var side = d.Side;
            d.LayoutTraced += t => _log.Write("dock_layout", ("side", side), ("plan", t.Replace(" ", "_")));
        }
    }

    public void Start()
    {
        var roots = new[] { Path.Combine(AppContext.BaseDirectory, "modules") };
        foreach (var lm in ModuleCatalog.Load(roots, _log)) _modules.Add(CreateView(lm));
        foreach (var mv in _modules.Where(m => m.Module.Manifest.Shapes.Contains("window") || m.Module.Manifest.Shapes.Contains("panel"))) PinWindowFor(mv);
        _right.Show();
        _left.Show();
        _right.ContentRendered += (_, _) => OnReady();
    }

    private void OnReady()
    {
        IntPtr target = _options.FollowHwnd != 0 ? new IntPtr(_options.FollowHwnd) : ExcelWindows.FindVisibleMain();
        if (target != IntPtr.Zero) Follow(target);
        PlaceAll(); // 핀 창은 엑셀 창(소유 창)을 안 뒤에 놓는다
        if (target == IntPtr.Zero) HideDocksAndWait();
        var size = User32.WindowBounds(_right.Handle);
        _log.Write("ready", ("overlay", _right.Handle.ToInt64()), ("left", _left.Handle.ToInt64()), ("signal", _signal.Handle.ToInt64()), ("zone", _right.ZoneRect()),
            ("size", $"{size.Width},{size.Height}"), ("target", target.ToInt64()), ("modules", _modules.Count), ("hotkeys", _hotkeys.Registered));
        foreach (var m in _modules) LogPlacement(m);
        Snap("dock_idle.png", _right.Root);
        if (_log.Enabled) MeasureFrameBaseline();
    }

    /// <summary>측정용: 아무것도 움직이지 않을 때 1초 동안 프레임 간격(이 PC에서 WPF가 내는 기본 프레임 속도).</summary>
    private void MeasureFrameBaseline()
    {
        var probe = Frames.StartProbe();
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        t.Tick += (_, _) =>
        {
            t.Stop();
            double mean = probe.MeanGapMs;
            var (max, steady, skipped, frames) = probe.Stop();
            _log.Write("frame_baseline", ("frames", frames), ("meanGap", Math.Round(mean, 2)), ("maxGap", max), ("steadyGap", steady), ("skipped", skipped));
        };
        t.Start();
    }

    private void Follow(IntPtr target)
    {
        foreach (var f in _followers) f.Dispose();
        _followers.Clear();
        _target = target;
        int Height(PixelRect t) => OverlayPlacement.DockHeight(t, 200);
        _followers.Add(new WindowFollower(_right.Handle, target, _log, (t, w, h) => OverlayPlacement.TopRightInside(t, w, h), Height));
        _followers.Add(new WindowFollower(_left.Handle, target, _log, (t, w, h) => OverlayPlacement.TopLeftInside(t, w, h), Height));
        foreach (var f in _followers)
        {
            f.Suspended = _hiddenAll;
            f.TargetGone += OnTargetGone;
            f.Start();
        }
        foreach (var p in _pinWindows.Values) p.SetOwner(target);
        UpdateLeftVisibility();
    }

    private void OnTargetGone()
    {
        _log.Write("target_gone", ("target", _target.ToInt64()));
        foreach (var p in _pins.Values) p.Hide();
        if (_options.FollowHwnd == 0) HideDocksAndWait();
    }

    /// <summary>보통 모드에서 엑셀 창이 없으면 숨고 2초마다 찾는다(창 목록만 보고 COM은 쓰지 않음).</summary>
    private void HideDocksAndWait()
    {
        User32.ShowWindow(_right.Handle, User32.SW_HIDE);
        User32.ShowWindow(_left.Handle, User32.SW_HIDE);
        _finder ??= new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, _) =>
        {
            var hwnd = ExcelWindows.FindVisibleMain();
            if (hwnd == IntPtr.Zero) return;
            _finder!.Stop();
            Follow(hwnd);
            if (!_hiddenAll) foreach (var p in _pins.Values) p.Reappear();
        }, Application.Current.Dispatcher);
        _finder.Start();
    }

    /// <summary>다른 엑셀 창이 앞으로 나오면 그 창을 따라간다(보통 모드만).</summary>
    private void OnForeground(IntPtr fg, bool isExcel)
    {
        if (!isExcel || _options.FollowHwnd != 0 || fg == _target) return;
        _log.Write("retarget", ("from", _target.ToInt64()), ("to", fg.ToInt64()));
        Follow(fg);
    }

    /// <summary>왼쪽 도크에 모듈이 없으면 그 창을 숨겨 둔다(빈 투명 창도 숨김).</summary>
    private void UpdateLeftVisibility()
    {
        bool any = _left.Modules.Count > 0;
        var f = _followers.Count > 1 ? _followers[1] : null;
        if (f == null) return;
        f.Suspended = _hiddenAll || !any;
        f.Sync(any ? "show" : "empty");
    }

    public void ToggleSummon()
    {
        _summoned = !_summoned;
        if (_summoned && _hiddenAll) ToggleHideAll();
        foreach (var p in _pins.Values) p.SetInteractive(_summoned);
        foreach (var m in _modules.Where(m => m.InDock)) m.Frame.SetSummoned(_summoned);
        _log.Write("summon", ("on", _summoned), ("pins", _pins.Count));
    }

    public void ToggleHideAll()
    {
        _hiddenAll = !_hiddenAll;
        foreach (var f in _followers) f.Suspended = _hiddenAll;
        if (_followers.Count > 0) _followers[0].Sync("show");
        UpdateLeftVisibility();
        foreach (var p in _pins.Values) { if (_hiddenAll) p.Hide(); else p.Reappear(); }
        _tray.SetHidden(_hiddenAll);
        _log.Write("hide_all", ("on", _hiddenAll));
    }

    private void Quit()
    {
        _log.Write("quit");
        Application.Current.Shutdown();
    }

    private void Snap(string name, FrameworkElement root)
    {
        if (_options.SnapshotDir == null) return;
        try { Frames.Snapshot(root, Path.Combine(_options.SnapshotDir, name)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _log.Write("snapshot_failed", ("name", name)); }
    }

    public void Dispose()
    {
        _store.Flush();
        _log.Write("exit");
        foreach (var f in _followers) f.Dispose();
        _hotkeys.Dispose();
        _tray.Dispose();
        _registry.Dispose();
        _signal.Dispose();
        _log.Dispose();
    }
}
