// 기능: 훈수꾼 틀의 연결부. 도크 창(왼·오), 핀 창, 모듈, 배치 저장, 신호·조회, 단축키, 알림 영역 아이콘을 서로 잇는다.
//       따라갈 엑셀 창을 정하고(보통: 보이는 XLMAIN, 앞으로 나온 엑셀로 바꿈 / 측정: --follow), 부르기·전부 숨김 상태를 맡는다.
// 사용법: App.OnStartup에서 new ShellController(options).Start();  끝낼 때 Dispose()
// 의존: Overlay/*, Shell/*, Excel/InstanceRegistry, Signal/SignalWindow, Diagnostics/*. 모듈 동작은 ShellController.Modules.cs, 최근 이동은 .History.cs, 셀 위 표시층은 .Marks.cs, 측정 메시지는 ShellController.Bench.cs.
using System.IO;
using System.Windows;
using System.Windows.Threading;
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Ui.Frame;
using ClockOut.Hunsu.Core.Settings;
using ClockOut.Hunsu.Ui.Controls;
using ClockOut.Hunsu.Ui.Theme;
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
    private HunsuSettings _settings;
    private ManagerWindow? _manager;
    private readonly SignalWindow _signal;
    private readonly InstanceRegistry _registry;
    private readonly HotkeyService _hotkeys;
    private readonly TrayIcon _tray;
    private BridgeUpdater? _bridgeUpdater;
    private readonly DockWindow _right;
    private readonly DockWindow _left;
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
        _settings = SettingsStore.Load(SettingsStore.PathFor(options.LayoutPath), _log);
        Parts.ListMaxRows = _settings.ListMaxRows;
        PanelFrame.SharpenOnHover = _settings.HoverSharpen;
        PanelFrame.MaxBodyHeight = _settings.PanelMaxHeight;
        // 테마와 패널 너비는 창을 만들기 전에 넣어야 StaticResource가 받는다(settings.theme, panelWidth).
        string theme = ThemePicker.Apply(Application.Current, _settings.Theme, _settings.PanelWidth, _settings.Skin);
        _log.Write("theme", ("name", theme), ("skin", _settings.Skin), ("panelWidth", _settings.PanelWidth));
        _right = new DockWindow(Places.EdgeRight, _settings);
        _left = new DockWindow(Places.EdgeLeft, _settings);
        CreateQuick();
        CreateSidebar();
        StartActivityWatch();
        _log.Write("launcher", ("shape", _settings.DockShape), ("groups", _settings.DockGroups), ("size", _settings.DockSize), ("icons", _settings.IconProfile), ("idle", _settings.IdleOpacity));
        _registry = new InstanceRegistry(_log, _settings) { TopicsFor = TopicsForSignal, AllTopics = AllEnabledTopics };
        _registry.TopicReady += OnTopic;
        _registry.SummaryReady += OnSummary;
        _registry.Stale += (_, topic) => ForTopic(topic, m => m.SetStale(true)); // 오래 걸릴 때만 흐리게(settings.staleAfterMs)
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
        _hotkeys.ModuleHotkey += OnModuleHotkey;
        _hotkeys.ForegroundChanged += OnForeground;
        _tray = new TrayIcon();
        _tray.SummonClicked += ToggleSummon;
        _tray.HideAllClicked += ToggleHideAll;
        _tray.QuitClicked += Quit;
        _tray.ManagerClicked += () => OpenManager();
        _tray.LookClicked += () => OpenManager("look");
        _tray.ExcelCleanupClicked += OpenExcelCleanup;
        foreach (var d in new[] { _right, _left })
        {
            d.ModuleIconClicked += OnModuleIcon;
            d.ModuleMenu = ModuleIconMenu;
            d.ToggleAllRequested += ToggleAllCollapsed;
            d.SummonRequested += ToggleSummon;
            d.HideAllRequested += ToggleHideAll;
            d.HubExtras = HubToggles;
            d.JoinToggleRequested += () => ApplySetting("panelJoin", !_settings.PanelJoin);
            d.SidebarRequested += v => ApplySetting("sidebar", v);
            d.QuitRequested += Quit;
            d.ManagerRequested += () => OpenManager();
            d.LookRequested += () => OpenManager("look");
            d.ExcelCleanupRequested += OpenExcelCleanup;
            var side = d.Side;
            d.SetCenterRatio(_store.State.DockRatio(side));
            d.HubDropped += (ratio, x) => OnHubDropped(side, ratio, x);
            d.LauncherRefreshNeeded += () => { _left.RefreshLauncher(); _right.RefreshLauncher(); };
            d.LayoutTraced += t => _log.Write("dock_layout", ("side", side), ("plan", t.Replace(" ", "_")));
        }
    }

    public void Start()
    {
        // 이미 깔린 다리가 이 묶음의 다리보다 낮으면 스스로 바꾼다(엑셀이 잡고 있으면 놓을 때까지 기다림).
        _bridgeUpdater = new BridgeUpdater(_log, text => Application.Current.Dispatcher.BeginInvoke(() => _tray.Balloon(text)));
        _bridgeUpdater.Start();
        var roots = new[] { Path.Combine(AppContext.BaseDirectory, "modules") };
        foreach (var lm in ModuleCatalog.Load(roots, _log)) _modules.Add(CreateView(lm));
        foreach (var mv in _modules.Where(m => m.Module.Manifest.Shapes.Contains("window") || m.Module.Manifest.Shapes.Contains("panel"))) PinWindowFor(mv);
        _right.Show();
        _left.Show();
        StartExcelCleanup();
        _right.ContentRendered += (_, _) => OnReady();
        if (_settings.ManagerOnStart) OpenManager();
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
        AttachQuick(target, _followers.Add);
        foreach (var f in _followers)
        {
            f.Suspended = _hiddenAll;
            f.TargetGone += OnTargetGone;
            f.Start();
        }
        foreach (var p in _pinWindows.Values) p.SetOwner(target);
        _markData.Clear();
        _markWindow?.Clear();
        UpdateDockVisibility();
        UpdateQuickBar();
        ApplyDefaultForms();
        UpdateSidebar();
    }

    private void OnTargetGone()
    {
        _log.Write("target_gone", ("target", _target.ToInt64()));
        foreach (var p in _pins.Values) p.Hide();
        _markWindow?.Clear();
        UpdateSidebarOffNoTarget();
        if (_options.FollowHwnd == 0) HideDocksAndWait();
    }

    /// <summary>보통 모드에서 엑셀 창이 없으면 숨고 2초마다 찾는다(창 목록만 보고 COM은 쓰지 않음).</summary>
    private void HideDocksAndWait()
    {
        User32.ShowWindow(_right.Handle, User32.SW_HIDE);
        User32.ShowWindow(_left.Handle, User32.SW_HIDE);
        UpdateSidebarOffNoTarget();
        _finder ??= new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, _) =>
        {
            var hwnd = ExcelWindows.FindVisibleMain();
            if (hwnd == IntPtr.Zero) return;
            _finder!.Stop();
            Follow(hwnd);
            if (!_hiddenAll) foreach (var p in ActivePins()) p.Reappear();
        }, Application.Current.Dispatcher);
        _finder.Start();
    }

    /// <summary>다른 엑셀 창이 앞으로 나오면 그 창을 따라간다(보통 모드만).</summary>
    private void OnForeground(IntPtr fg, bool isExcel)
    {
        if (!isExcel) HideHuds();
        if (!isExcel || _options.FollowHwnd != 0 || fg == _target) return;
        _log.Write("retarget", ("from", _target.ToInt64()), ("to", fg.ToInt64()));
        Follow(fg);
    }

    /// <summary>모듈이 없는 도크는 창을 숨겨 둔다(빈 투명 창과 훈 원도 보이지 않게). 왼쪽 도크는 비어도 훈 원이 남고, 오른쪽 도크는 사용자가 모듈을 옮겨 놓았을 때만 나타난다. _followers[0]=오른쪽, [1]=왼쪽.</summary>
    private void UpdateDockVisibility()
    {
        if (_followers.Count < 2) return;
        (WindowFollower f, DockWindow dock)[] docks = [(_followers[0], _right), (_followers[1], _left)];
        foreach (var (f, dock) in docks)
        {
            // 왼쪽 도크(훈 원)는 모듈이 하나도 없어도 늘 보인다: 훈수꾼이 켜져 있다는 표시이고, 우클릭 메뉴(모듈 관리)로 가는 길이다(2026-11-17 "안 보여" 신고).
            bool any = !SidebarOn && (dock.Modules.Count > 0 || dock == _left); // 사이드바가 켜지면 도크 창은 둘 다 감춘다
            f.Suspended = _hiddenAll || !any;
            f.Sync(any ? "show" : "empty");
        }
    }

    public void ToggleHideAll()
    {
        _hiddenAll = !_hiddenAll;
        foreach (var f in _followers) f.Suspended = _hiddenAll;
        UpdateSidebar();
        UpdateDockVisibility();
        UpdateQuickBar();
        foreach (var p in ActivePins()) { if (_hiddenAll) p.Hide(); else p.Reappear(); }
        _tray.SetHidden(_hiddenAll);
        DrawMarks();
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
        _cleanupTimer?.Stop();
        _store.Flush();
        _log.Write("exit");
        _sidebar?.Shutdown(); // 앱 바 예약을 풀어 엑셀 창이 원래 크기로 돌아오게
        foreach (var f in _followers) f.Dispose();
        _hotkeys.Dispose();
        _bridgeUpdater?.Dispose();
        _tray.Dispose();
        _registry.Dispose();
        _signal.Dispose();
        _log.Dispose();
    }
}
