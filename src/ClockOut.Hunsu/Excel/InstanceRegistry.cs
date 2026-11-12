// 기능: 엑셀 창 핸들마다 전용 작업 스레드(ExcelStaWorker)와 순번 추적기를 하나씩 둔다. 신호를 받아 조회하고, 결과를 화면에 넘긴다.
//       한 인스턴스에 진행 중인 조회는 하나만, 그동안 온 신호는 마지막 것 하나로 묶는다(docs/04 2장).
// 사용법: var reg = new InstanceRegistry(log); reg.SummaryReady += r => ...; reg.OnSignal(hwnd, code, seq, at); await reg.ProbeAsync(hwnd)
// 의존: Excel/ExcelStaWorker, Core/SeqTracker, Core/BridgeResponse, Core/SelectionSummary. 화면 스레드에서 부른다.
using System.Diagnostics;
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Diagnostics;

namespace ClockOut.Hunsu.Excel;

/// <summary>선택 요약 한 번의 결과와 구간별 시각(Stopwatch 눈금).</summary>
internal sealed record SummaryResult(IntPtr Excel, int Seq, long SignalAt, long QueryStart, long QueryDone, BridgeCall Call, BridgeResponse Response, SelectionSummary? Summary);

internal sealed class InstanceRegistry : IDisposable
{
    private sealed class Instance(ExcelStaWorker worker)
    {
        public ExcelStaWorker Worker { get; } = worker;
        public SeqTracker Seq { get; } = new();
        public bool Busy { get; set; }
        public (int Seq, long At)? Pending { get; set; }
    }

    private readonly Dictionary<IntPtr, Instance> _items = [];
    private readonly RunLog _log;

    public InstanceRegistry(RunLog log) => _log = log;

    /// <summary>조회가 이만큼(ms) 넘게 걸리면 화면을 흐리게 바꾼다(재시도 최악 대기 250ms와 같게 둠).</summary>
    public const int SlowCallMs = 250;

    public event Action<SummaryResult>? SummaryReady;

    /// <summary>조회가 SlowCallMs를 넘겨 아직 기다리는 중(엑셀이 바쁨).</summary>
    public event Action<IntPtr>? Slow;

    public int Count => _items.Count;

    private Instance Get(IntPtr hwnd)
    {
        if (!_items.TryGetValue(hwnd, out var inst))
        {
            inst = new Instance(new ExcelStaWorker(hwnd));
            _items[hwnd] = inst;
            _log.Write("instance_new", ("excel", hwnd.ToInt64()), ("count", _items.Count));
        }
        return inst;
    }

    public async void OnSignal(IntPtr hwnd, int code, int seq, long at)
    {
        var inst = Get(hwnd);
        if (!inst.Seq.Accept(seq))
        {
            _log.Write("signal_stale", ("excel", hwnd.ToInt64()), ("seq", seq));
            return;
        }
        if (code != SignalCodec.Sel) return; // 0단계 다리는 sel만 보낸다
        if (inst.Busy)
        {
            inst.Pending = (seq, at);
            _log.Write("signal_coalesced", ("excel", hwnd.ToInt64()), ("seq", seq));
            return;
        }
        inst.Busy = true;
        try
        {
            (int Seq, long At)? next = (seq, at);
            while (next is { } n)
            {
                inst.Pending = null;
                long qs = Stopwatch.GetTimestamp();
                var task = inst.Worker.CallAsync("CoHunsu_Query", "selection.summary", "{}");
                // 엑셀이 매크로를 도는 중이면 호출은 거절되지 않고 끝날 때까지 기다린다(0단계 확인). 오래 걸리면 먼저 "바쁨"을 알린다.
                if (await Task.WhenAny(task, Task.Delay(SlowCallMs)) != task)
                {
                    _log.Write("query_slow", ("excel", hwnd.ToInt64()), ("seq", n.Seq), ("after", SlowCallMs));
                    Slow?.Invoke(hwnd);
                }
                var call = await task;
                long qd = Stopwatch.GetTimestamp();
                var resp = call.Ok ? BridgeResponse.Parse(call.Json) : BridgeResponse.Failed(call.ErrorCode, "엑셀 호출 실패");
                var summary = resp.Ok ? SelectionSummary.From(resp.Data) : null;
                SummaryReady?.Invoke(new SummaryResult(hwnd, n.Seq, n.At, qs, qd, call, resp, summary));
                next = inst.Pending;
            }
        }
        finally
        {
            inst.Busy = false;
        }
    }

    /// <summary>hello를 불러 거절·재시도 결과를 본다(측정 도구와 연결 확인용).</summary>
    public Task<BridgeCall> ProbeAsync(IntPtr hwnd) => Get(hwnd).Worker.CallAsync("CoHunsu_Hello");

    public void Dispose()
    {
        foreach (var inst in _items.Values) inst.Worker.Dispose();
        _items.Clear();
    }
}
