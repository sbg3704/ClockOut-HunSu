// 기능: 엑셀 창 핸들마다 전용 작업 스레드(ExcelStaWorker)와 순번 추적기를 하나씩 둔다. 신호를 받아 조회하고, 결과를 화면에 넘긴다.
//       한 인스턴스에 진행 중인 조회는 하나만, 그동안 온 신호는 마지막 것 하나로 묶는다(docs/04 2장).
//       조회 사이에는 QueryPacer가 정한 만큼 쉰다: 범위를 끄는 동안 엑셀이 조회에 묶여 버벅이지 않게(docs/06, 일#304).
// 사용법: var reg = new InstanceRegistry(log); reg.SummaryReady += r => ...; reg.OnSignal(hwnd, code, seq, at); await reg.ProbeAsync(hwnd)
// 의존: Excel/ExcelStaWorker, Core/SeqTracker, Core/QueryPacer, Core/BridgeResponse, Core/SelectionSummary, Native/User32(마우스 단추).
//       화면 스레드에서 부른다(await 뒤에도 화면 스레드로 돌아온다).
using System.Diagnostics;
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Diagnostics;
using ClockOut.Hunsu.Native;

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
        public int Coalesced { get; set; }
        public double? LastEndMs { get; set; }
        public double LastQueryMs { get; set; }
    }

    private readonly Dictionary<IntPtr, Instance> _items = [];
    private readonly RunLog _log;
    private readonly QueryPacer _pacer;
    private readonly Func<bool> _mouseDown;

    public InstanceRegistry(RunLog log, QueryPacer? pacer = null, Func<bool>? mouseDown = null)
    {
        _log = log;
        _pacer = pacer ?? QueryPacer.Default;
        _mouseDown = mouseDown ?? User32.PrimaryButtonDown;
    }

    /// <summary>
    /// 조회가 이만큼(ms) 넘게 걸리면 화면을 흐리게 바꾼다. 처음 250ms였는데 큰 범위 조회마다 흐림↔선명이 반복돼
    /// 떨림처럼 보여서 600ms로 늘렸다(docs/06, 2026-11-13).
    /// </summary>
    public const int SlowCallMs = 600;

    public event Action<SummaryResult>? SummaryReady;

    /// <summary>조회가 SlowCallMs를 넘겨 아직 기다리는 중(엑셀이 바쁨).</summary>
    public event Action<IntPtr>? Slow;

    public int Count => _items.Count;

    private static double NowMs() => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;

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

    public void OnSignal(IntPtr hwnd, int code, int seq, long at)
    {
        var inst = Get(hwnd);
        if (!inst.Seq.Accept(seq))
        {
            _log.Write("signal_stale", ("excel", hwnd.ToInt64()), ("seq", seq));
            return;
        }
        if (code != SignalCodec.Sel) return; // 다리는 아직 sel만 보낸다
        if (inst.Pending != null) inst.Coalesced++;
        inst.Pending = (seq, at);
        if (!inst.Busy) Pump(hwnd, inst);
    }

    /// <summary>기다리는 신호가 없어질 때까지: 쉴 만큼 쉬고, 마지막 신호 하나로 조회한다.</summary>
    private async void Pump(IntPtr hwnd, Instance inst)
    {
        inst.Busy = true;
        try
        {
            while (inst.Pending is { } n)
            {
                double wait = _pacer.DelayMs(new PaceInput(NowMs(), inst.LastEndMs, inst.LastQueryMs, _mouseDown()));
                if (wait > 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(wait));
                    continue; // 기다리는 동안 새 신호가 왔거나 단추를 놓았을 수 있으니 다시 판단한다.
                }
                inst.Pending = null;
                if (inst.Coalesced > 0)
                {
                    _log.Write("signal_coalesced", ("excel", hwnd.ToInt64()), ("seq", n.Seq), ("dropped", inst.Coalesced));
                    inst.Coalesced = 0;
                }
                await QueryOnce(hwnd, inst, n.Seq, n.At);
            }
        }
        finally
        {
            inst.Busy = false;
        }
    }

    private async Task QueryOnce(IntPtr hwnd, Instance inst, int seq, long at)
    {
        long qs = Stopwatch.GetTimestamp();
        var task = inst.Worker.CallAsync("CoHunsu_Query", "selection.summary", "{}");
        // 엑셀이 매크로를 도는 중이면 호출은 거절되지 않고 끝날 때까지 기다린다(0단계 확인). 오래 걸리면 먼저 "바쁨"을 알린다.
        if (await Task.WhenAny(task, Task.Delay(SlowCallMs)) != task)
        {
            _log.Write("query_slow", ("excel", hwnd.ToInt64()), ("seq", seq), ("after", SlowCallMs));
            Slow?.Invoke(hwnd);
        }
        var call = await task;
        long qd = Stopwatch.GetTimestamp();
        inst.LastEndMs = NowMs();
        inst.LastQueryMs = (qd - qs) * 1000.0 / Stopwatch.Frequency;
        var resp = call.Ok ? BridgeResponse.Parse(call.Json) : BridgeResponse.Failed(call.ErrorCode, "엑셀 호출 실패");
        var summary = resp.Ok ? SelectionSummary.From(resp.Data) : null;
        SummaryReady?.Invoke(new SummaryResult(hwnd, seq, at, qs, qd, call, resp, summary));
    }

    /// <summary>hello를 불러 거절·재시도 결과를 본다(측정 도구와 연결 확인용).</summary>
    public Task<BridgeCall> ProbeAsync(IntPtr hwnd) => Get(hwnd).Worker.CallAsync("CoHunsu_Hello");

    public void Dispose()
    {
        foreach (var inst in _items.Values) inst.Worker.Dispose();
        _items.Clear();
    }
}
