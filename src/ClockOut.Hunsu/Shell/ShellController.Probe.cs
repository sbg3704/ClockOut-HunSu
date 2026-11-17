// 기능: 따라가는 엑셀에 먼저 말을 걸어 본다(ShellController의 일부). 다리는 엑셀이 신호를 보내야만 조회가 시작되는데, 다리가 안 열린 엑셀은 신호도 못 보내서 패널이 빈 채로 아무 안내도 없었다(2026-11-17 확인용 엑셀 신고).
//       그래서 따라가기를 시작할 때 hello를 한 번 불러 본다: 되면 켜진 모듈의 값을 한꺼번에 채우고, 안 되면 이유(다리 없음·엑셀에 못 붙음)를 기록하고 풍선으로 알린 뒤, 될 때까지 5초마다 다시 해 본다(그 사이 사용자가 다리를 열면 저절로 채워진다).
// 사용법: Follow()와 직접 연결이 StartProbe()를 부른다. OnTopic이 성공 결과를 받으면 NoteHealthy()로 재시도를 멈춘다.
// 의존: Excel/InstanceRegistry(ProbeAsync, Refresh), Shell/ShellController.BridgeNotice(NoteBridgeProblem), Core/BridgeResponse. 엑셀 안의 값은 바꾸지 않는다(hello는 읽기 전용).
using System.Windows;
using System.Windows.Threading;
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Excel;

namespace ClockOut.Hunsu.Shell;

internal sealed partial class ShellController
{
    private DispatcherTimer? _probeTimer;
    private bool _probing;
    private readonly HashSet<IntPtr> _toldNoAttach = [];
    private readonly HashSet<IntPtr> _toldNoBooks = [];

    /// <summary>따라가는 엑셀에 hello를 불러 본다. 곧바로 한 번(엑셀이 막 켜졌을 수 있어 0.6초 뒤), 안 되면 5초마다.</summary>
    private void StartProbe()
    {
        // 주의(0.0.211 강제 종료의 원인): DispatcherTimer 생성자에 콜백으로 null을 넘기면 ArgumentNullException이라 시작하자마자 꺼졌다. 콜백은 Tick에 따로 붙인다.
        if (_probeTimer == null)
        {
            _probeTimer = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher) { Interval = TimeSpan.FromSeconds(5) };
            _probeTimer.Tick += async (_, _) => await ProbeOnce();
        }
        _probeTimer.Stop();
        var first = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher) { Interval = TimeSpan.FromMilliseconds(600) };
        first.Tick += async (_, _) => { first.Stop(); await ProbeOnce(); };
        first.Start();
    }

    private async Task ProbeOnce()
    {
        try { await ProbeOnceCore(); }
        catch (Exception ex)
        {
            // 먼저 말 걸기는 덤이다: 무슨 일이 있어도 훈수꾼을 꺼뜨리지 않는다(기록만 남기고 다음 5초에 다시).
            _log.Write("probe_exception", ("type", ex.GetType().Name), ("msg", ex.Message));
            _probing = false;
        }
    }

    private async Task ProbeOnceCore()
    {
        if (_probing || _target == IntPtr.Zero) return;
        _probing = true;
        var hwnd = _target;
        try
        {
            var call = await _registry.ProbeAsync(hwnd);
            if (hwnd != _target) return; // 그 사이 다른 엑셀로 옮겨 갔다
            if (call.Ok)
            {
                bool wasDown = _probeTimer is { IsEnabled: true };
                _probeTimer?.Stop();
                _log.Write("probe_ok", ("excel", hwnd.ToInt64()), ("recovered", wasDown), ("ms", Math.Round(call.Ms, 1)));
                _registry.Refresh(hwnd, AllEnabledTopics()); // 신호를 기다리지 않고 지금 값을 채운다
                _ = CheckOpenBooksAsync(hwnd);
                return;
            }
            string code = call.ErrorCode;
            _log.Write("probe_failed", ("excel", hwnd.ToInt64()), ("code", code), ("hr", HResultNames.Name(call.HResult)), ("attach", call.Attach));
            if (code == "bridge_missing") NoteBridgeProblem(new TopicResult(hwnd, "hello", 0, 0, 0, 0, call, BridgeResponse.Failed(code, "hello 실패")));
            else if (code == "excel_missing" && _toldNoAttach.Add(hwnd))
                _tray.Balloon("이 엑셀에 붙지 못했어요. 엑셀을 관리자 권한으로 켰다면 훈수꾼도 같은 권한으로 켜야 해요. 알림 영역 아이콘 우클릭 → 「엑셀 연결…」 → 「이 엑셀 점검」에서 자세히 볼 수 있어요.");
            _probeTimer?.Start(); // 될 때까지 5초마다
        }
        finally
        {
            _probing = false;
        }
    }

    /// <summary>다리는 되는데 패널이 비는 흔한 이유: 열린 통합 문서가 없거나, 파일이 「보호된 보기」로 열려 있다(그때 ActiveWorkbook이 비어 다리가 "열린 통합 문서가 없음"을 돌려준다, 2026-11-17 VM 재현). 이유를 기록하고 풍선으로 한 번 알린다.</summary>
    private async Task CheckOpenBooksAsync(IntPtr hwnd)
    {
        try
        {
            var r = await _registry.InspectAsync(hwnd);
            _log.Write("books_check", ("excel", hwnd.ToInt64()), ("books", r.Workbooks), ("pv", r.ProtectedViews), ("active", r.ActiveBook));
            if (!r.Attached || r.Workbooks > 0) return;
            if (!_toldNoBooks.Add(hwnd)) return;
            _tray.Balloon(r.ProtectedViews > 0
                ? "이 엑셀의 파일이 「보호된 보기」로 열려 있어 훈수꾼이 읽지 못해요. 엑셀 위쪽 노란 막대의 「편집 사용」을 눌러 주세요."
                : "이 엑셀에는 열린 통합 문서가 없어요. 파일을 열면 훈수꾼이 채워져요.");
        }
        catch (Exception ex) { _log.Write("books_check_exception", ("type", ex.GetType().Name), ("msg", ex.Message)); }
    }

    /// <summary>조회가 한 번이라도 성공했다: 더 두드려 볼 필요가 없다.</summary>
    private void NoteHealthy(IntPtr excel)
    {
        if (excel == _target) _probeTimer?.Stop();
    }
}
