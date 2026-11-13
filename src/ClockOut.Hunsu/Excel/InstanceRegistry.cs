// 기능: 엑셀 창 핸들마다 전용 작업 스레드(ExcelStaWorker)와 순번 추적기를 하나씩 둔다. 신호를 받아 켜진 모듈이 구독한 주제만 조회하고 결과를 화면에 넘긴다.
//       주제마다 진행 중인 조회는 하나만, 그동안 온 신호는 마지막 것 하나로 묶고, 조회 사이에는 간격을 둔다(Core/QueryThrottle: 범위를 끌 때 엑셀이 버벅이던 문제, docs/06).
// 사용법: var reg = new InstanceRegistry(log, settings) { TopicsFor = code => ... }; reg.TopicReady += r => ...; reg.OnSignal(hwnd, code, seq, at);
//         reg.Refresh(hwnd, topics)  처음 한 번 채우기;  await reg.DoAsync(hwnd, "goto", argsJson)  명령 보내기;  await reg.ProbeAsync(hwnd)
// 의존: Excel/ExcelStaWorker, Core(SeqTracker, BridgeResponse, SelectionSummary, QueryThrottle, Settings). 화면 스레드에서 부른다.
using System.Diagnostics;
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Core.Settings;
using ClockOut.Hunsu.Diagnostics;
using ClockOut.Hunsu.Native;

namespace ClockOut.Hunsu.Excel;

/// <summary>선택 요약 한 번의 결과와 구간별 시각(Stopwatch 눈금). 측정 도구가 읽는 render 기록용.</summary>
internal sealed record SummaryResult(IntPtr Excel, int Seq, long SignalAt, long QueryStart, long QueryDone, BridgeCall Call, BridgeResponse Response, SelectionSummary? Summary);

/// <summary>주제 하나의 조회 결과.</summary>
internal sealed record TopicResult(IntPtr Excel, string Topic, int Seq, long SignalAt, long QueryStart, long QueryDone, BridgeCall Call, BridgeResponse Response);

internal sealed class InstanceRegistry : IDisposable
{
    private sealed class TopicState
    {
        public bool Active { get; set; }
        public (int Seq, long At)? Pending { get; set; }
        public double LastEndMs { get; set; } = -1;
        public double LastDurMs { get; set; }
    }

    private sealed class Instance(ExcelStaWorker worker)
    {
        public ExcelStaWorker Worker { get; } = worker;
        public SeqTracker Seq { get; } = new();
        public bool Greeted { get; set; }
        public Dictionary<string, TopicState> Topics { get; } = new(StringComparer.Ordinal);
    }

    private readonly Dictionary<IntPtr, Instance> _items = [];
    private readonly RunLog _log;
    private HunsuSettings _settings;

    public InstanceRegistry(RunLog log, HunsuSettings settings)
    {
        _log = log;
        _settings = settings;
    }

    /// <summary>조회가 이만큼(ms) 넘게 걸리면 query_slow를 기록하고 Slow를 알린다(재시도 최악 대기 250ms와 같게 둠). 화면을 흐리게 하는 기준은 settings.staleAfterMs.</summary>
    public const int SlowCallMs = 250;

    public event Action<SummaryResult>? SummaryReady;
    public event Action<TopicResult>? TopicReady;

    /// <summary>조회가 SlowCallMs를 넘겨 아직 기다리는 중(엑셀이 바쁨). 기록용.</summary>
    public event Action<IntPtr>? Slow;

    /// <summary>조회가 settings.staleAfterMs를 넘겨 아직 기다리는 중: 이때만 패널을 흐리게 한다.</summary>
    public event Action<IntPtr, string>? Stale;

    /// <summary>신호 번호 → 다시 조회할 주제들(켜진 모듈의 구독). 정하지 않으면 sel → selection.summary만.</summary>
    public Func<int, IReadOnlyList<string>> TopicsFor { get; set; } = code => code == SignalCodec.Sel ? new[] { "selection.summary" } : Array.Empty<string>();

    /// <summary>켜진 모듈이 구독한 모든 주제. 엑셀에서 처음 신호가 왔을 때 아직 한 번도 못 채운 주제들도 함께 채운다(예: 시트 목록).</summary>
    public Func<IReadOnlyList<string>> AllTopics { get; set; } = () => Array.Empty<string>();

    /// <summary>주 마우스 단추가 눌려 있는가(범위 끌기 판단, 왼손잡이 설정 반영). 시험에서 바꿔 끼울 수 있다.</summary>
    public Func<bool> MouseDown { get; set; } = User32.PrimaryButtonDown;

    public int Count => _items.Count;

    public void UpdateSettings(HunsuSettings settings) => _settings = settings;

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
        var topics = TopicsFor(code);
        foreach (var topic in topics) Request(inst, hwnd, topic, seq, at);
        if (!inst.Greeted)
        {
            inst.Greeted = true;
            foreach (var topic in AllTopics().Where(t => !topics.Contains(t))) Request(inst, hwnd, topic, seq, at);
        }
    }

    /// <summary>신호 없이 지금 값을 한 번 채운다(모듈을 켰을 때).</summary>
    public void Refresh(IntPtr hwnd, IEnumerable<string> topics)
    {
        var inst = Get(hwnd);
        foreach (var topic in topics) Request(inst, hwnd, topic, 0, Stopwatch.GetTimestamp());
    }

    private async void Request(Instance inst, IntPtr hwnd, string topic, int seq, long at)
    {
        if (!inst.Topics.TryGetValue(topic, out var st))
        {
            st = new TopicState();
            inst.Topics[topic] = st;
        }
        st.Pending = (seq, at);
        if (st.Active)
        {
            _log.Write("signal_coalesced", ("excel", hwnd.ToInt64()), ("seq", seq), ("topic", topic));
            return;
        }
        st.Active = true;
        try
        {
            while (st.Pending != null)
            {
                int wait = QueryThrottle.DelayMs(NowMs(), st.LastEndMs, st.LastDurMs, MouseDown(), _settings);
                if (wait > 0)
                {
                    _log.Write("query_deferred", ("excel", hwnd.ToInt64()), ("topic", topic), ("wait", wait));
                    await Task.Delay(wait); // 기다리는 동안 온 신호는 Pending을 덮어쓴다(가장 새 것만 남음)
                }
                if (st.Pending is not { } n) break;
                st.Pending = null;
                long qs = Stopwatch.GetTimestamp();
                var task = inst.Worker.CallAsync("CoHunsu_Query", topic, "{}");
                // 엑셀이 매크로를 도는 중이면 호출은 거절되지 않고 끝날 때까지 기다린다(0단계 확인). 오래 걸리면 먼저 "바쁨"을 알린다.
                if (await Task.WhenAny(task, Task.Delay(SlowCallMs)) != task)
                {
                    _log.Write("query_slow", ("excel", hwnd.ToInt64()), ("seq", n.Seq), ("after", SlowCallMs), ("topic", topic));
                    Slow?.Invoke(hwnd);
                    int rest = Math.Max(0, _settings.StaleAfterMs - SlowCallMs);
                    if (await Task.WhenAny(task, Task.Delay(rest)) != task) Stale?.Invoke(hwnd, topic);
                }
                var call = await task;
                long qd = Stopwatch.GetTimestamp();
                var resp = call.Ok ? BridgeResponse.Parse(call.Json) : BridgeResponse.Failed(call.ErrorCode, "엑셀 호출 실패");
                st.LastEndMs = NowMs();
                st.LastDurMs = (qd - qs) * 1000.0 / Stopwatch.Frequency;
                TopicReady?.Invoke(new TopicResult(hwnd, topic, n.Seq, n.At, qs, qd, call, resp));
                if (topic == "selection.summary")
                {
                    var summary = resp.Ok ? SelectionSummary.From(resp.Data) : null;
                    SummaryReady?.Invoke(new SummaryResult(hwnd, n.Seq, n.At, qs, qd, call, resp, summary));
                }
            }
        }
        finally
        {
            st.Active = false;
        }
    }

    private static double NowMs() => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;

    /// <summary>다리 명령 하나(예: goto, sheet.activate)를 보낸다. 명령 이름이 허용된 것인지는 부른 쪽이 확인한다.</summary>
    public Task<BridgeCall> DoAsync(IntPtr hwnd, string command, string argsJson) => Get(hwnd).Worker.CallAsync("CoHunsu_Do", command, argsJson);

    /// <summary>hello를 불러 거절·재시도 결과를 본다(측정 도구와 연결 확인용).</summary>
    public Task<BridgeCall> ProbeAsync(IntPtr hwnd) => Get(hwnd).Worker.CallAsync("CoHunsu_Hello");

    public void Dispose()
    {
        foreach (var inst in _items.Values) inst.Worker.Dispose();
        _items.Clear();
    }
}
