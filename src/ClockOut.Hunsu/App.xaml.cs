// 기능: 훈수꾼 시작과 연결. 오버레이 창, 창 따라가기, 신호 창, 엑셀 인스턴스 목록을 만들고 서로 잇는다.
// 사용법: ClockOut.Hunsu.exe                 보이는 엑셀 창(XLMAIN)을 찾아 따라간다(엑셀 COM에는 신호가 올 때만 붙는다)
//         ClockOut.Hunsu.exe --follow <hwnd> --log <파일> --snapshot <폴더>   측정 도구가 쓰는 방식(Core/AppOptions)
// 의존: Overlay/*, Signal/SignalWindow, Excel/InstanceRegistry, Diagnostics/RunLog, Core/*
using System.Windows;
using System.Windows.Threading;
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Diagnostics;
using ClockOut.Hunsu.Excel;
using ClockOut.Hunsu.Native;
using ClockOut.Hunsu.Overlay;
using ClockOut.Hunsu.Signal;

namespace ClockOut.Hunsu;

public partial class App : Application
{
    private AppOptions _options = AppOptions.Parse([]);
    private RunLog _log = new(null);
    private SignalWindow? _signal;
    private InstanceRegistry? _registry;
    private OverlayWindow? _overlay;
    private WindowFollower? _follower;
    private DispatcherTimer? _finder;
    private bool _snapshotTaken;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _options = AppOptions.Parse(e.Args);
        _log = new RunLog(_options.LogPath);
        _log.Write("start", ("pid", Environment.ProcessId), ("badArgs", _options.Errors.Count));

        _registry = new InstanceRegistry(_log);
        _registry.SummaryReady += OnSummary;
        _registry.Slow += _ => _overlay?.ShowStale("엑셀이 바쁨 — 끝나면 바로 갱신");
        _signal = new SignalWindow(_log);
        _signal.Signal += (hwnd, code, seq, at) =>
        {
            _log.Write("signal", ("excel", hwnd.ToInt64()), ("code", SignalCodec.NameOf(code)), ("seq", seq), ("recv", at));
            _registry.OnSignal(hwnd, code, seq, at);
        };
        _signal.Bench += OnBench;

        _overlay = new OverlayWindow();
        _overlay.PillClicked += () => { if (_overlay.PanelOpen) _overlay.ClosePanel(); else _overlay.OpenPanel(); };
        _overlay.ContentRendered += (_, _) => OnOverlayReady();
        _overlay.Show();
    }

    private void OnOverlayReady()
    {
        var overlay = _overlay!;
        IntPtr target = _options.FollowHwnd != 0 ? new IntPtr(_options.FollowHwnd) : ExcelWindows.FindVisibleMain();
        if (target != IntPtr.Zero) Follow(target);
        else
        {
            User32.ShowWindow(overlay.Handle, User32.SW_HIDE);
            StartFinder();
        }
        var size = User32.WindowBounds(overlay.Handle);
        _log.Write("ready", ("overlay", overlay.Handle.ToInt64()), ("signal", _signal!.Handle.ToInt64()), ("zone", overlay.ZoneRect()), ("size", $"{size.Width},{size.Height}"), ("target", target.ToInt64()));
        TakeSnapshot("overlay_idle.png");
    }

    private void Follow(IntPtr target)
    {
        _follower?.Dispose();
        _follower = new WindowFollower(_overlay!.Handle, target, _log);
        _follower.TargetGone += () =>
        {
            _follower?.Dispose();
            _follower = null;
            if (_options.FollowHwnd == 0) StartFinder();
        };
        _follower.Start();
    }

    /// <summary>보통 모드에서 엑셀 창이 아직 없으면 2초마다 찾는다(창 목록만 보고 COM은 쓰지 않음).</summary>
    private void StartFinder()
    {
        _finder ??= new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, _) =>
        {
            var hwnd = ExcelWindows.FindVisibleMain();
            if (hwnd == IntPtr.Zero) return;
            _finder!.Stop();
            Follow(hwnd);
        }, Dispatcher);
        _finder.Start();
    }

    private void OnSummary(SummaryResult r)
    {
        var overlay = _overlay!;
        if (r.Summary != null) overlay.ShowSummary(r.Summary, "연결됨 · " + r.Call.Attach);
        else overlay.ShowStale(r.Response.Code == "excel_busy" ? "엑셀이 바쁨 — 곧 다시" : "불러오지 못함: " + r.Response.Code);
        overlay.NextFrame(frame =>
        {
            _log.Write("render", ("excel", r.Excel.ToInt64()), ("seq", r.Seq), ("recv", r.SignalAt), ("qs", r.QueryStart), ("qd", r.QueryDone), ("frame", frame),
                ("ok", r.Response.Ok), ("code", r.Response.Code), ("hr", HResultNames.Name(r.Call.HResult)), ("retries", r.Call.Retries),
                ("addr", r.Summary?.Addr), ("count", r.Summary?.Count), ("attach", r.Call.Attach));
            if (r.Summary != null) TakeSnapshot("overlay_summary.png");
        });
    }

    private async void OnBench(string kind, IntPtr wParam, IntPtr lParam, long at)
    {
        var overlay = _overlay!;
        switch (kind)
        {
            case BenchMessages.OpenPanel:
                long first = 0;
                overlay.NextFrame(t => first = t);
                overlay.OpenPanel(done =>
                {
                    _log.Write("panel", ("sent", lParam.ToInt64()), ("recv", at), ("first", first), ("done", done));
                    TakeSnapshot("overlay_panel.png", force: true);
                });
                break;
            case BenchMessages.ClosePanel:
                overlay.ClosePanel();
                break;
            case BenchMessages.Probe:
                var call = await _registry!.ProbeAsync(wParam);
                _log.Write("probe", ("excel", wParam.ToInt64()), ("sent", lParam.ToInt64()), ("recv", at), ("ok", call.Ok), ("hr", HResultNames.Name(call.HResult)),
                    ("code", call.ErrorCode), ("retries", call.Retries), ("reject", call.RejectType), ("ms", Math.Round(call.Ms, 2)), ("attach", call.Attach));
                break;
            case BenchMessages.Quit:
                Shutdown();
                break;
        }
    }

    private void TakeSnapshot(string name, bool force = false)
    {
        if (_options.SnapshotDir == null || (_snapshotTaken && !force && name != "overlay_summary.png")) return;
        try
        {
            string path = System.IO.Path.Combine(_options.SnapshotDir, name);
            if (!force && System.IO.File.Exists(path) && name == "overlay_summary.png") return;
            _overlay!.SaveSnapshot(path);
            _snapshotTaken = true;
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            _log.Write("snapshot_failed", ("name", name));
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _log.Write("exit");
        _follower?.Dispose();
        _registry?.Dispose();
        _signal?.Dispose();
        _log.Dispose();
        base.OnExit(e);
    }
}
