// 기능: 엑셀 쪽 시험 — 선택→화면(신호, 조회, 그리기 구간), 패널 열기, 다리 이벤트 시간, 호출 거절과 재시도, 엑셀 두 개와 재시작.
// 사용법: ExcelChecks.Selection(rep, xl, app); ExcelChecks.Panel(rep, app); ExcelChecks.Busy(rep, xl, app); ExcelChecks.Instances(rep, xl, app, xlam)
// 의존: HiddenExcel, AppUnderTest, Report, Core/LogLine, Core/BenchMessages
using System.Diagnostics;
using System.Text.Json;
using ClockOut.Hunsu.Core;

namespace ClockOut.Hunsu.Bench;

internal static class ExcelChecks
{
    private static readonly string[] Ranges = ["B2", "C3:E8", "A1:J20", "F5", "B2:B20", "D4:H4", "A:A", "J20"];

    private static bool IsRender(LogLine l, IntPtr xl) => l.Name == "render" && l.Long("excel") == xl.ToInt64();

    public static void Selection(Report rep, HiddenExcel xl, AppUnderTest app, int n)
    {
        Console.WriteLine("선택 → 화면");
        for (int i = 0; i < 5; i++) { xl.Select(Ranges[i % Ranges.Length]); app.WaitFor(l => IsRender(l, xl.Hwnd), 3000); } // 연결 준비(처음 붙기)
        app.Drain();
        xl.Query("bridge.stats", "{\"reset\":true}");
        var signal = new List<double>(); var query = new List<double>(); var draw = new List<double>(); var total = new List<double>();
        int missing = 0, wrong = 0, failed = 0;
        for (int i = 0; i < n; i++)
        {
            string addr = Ranges[i % Ranges.Length];
            long t0 = Stopwatch.GetTimestamp();
            xl.Select(addr);
            var r = app.WaitFor(l => IsRender(l, xl.Hwnd), 2000);
            if (r == null) { missing++; continue; }
            if (r.Get("ok") != "True") { failed++; continue; }
            if (!SameAddress(r.Get("addr"), addr)) wrong++;
            signal.Add(Report.Ms(t0, r.Long("recv")));
            query.Add(Report.Ms(r.Long("qs"), r.Long("qd")));
            draw.Add(Report.Ms(r.Long("qd"), r.Long("frame")));
            total.Add(Report.Ms(t0, r.Long("frame")));
            Thread.Sleep(5);
        }
        rep.Time("선택→화면 합계", total, 50);
        rep.Time("  ㄴ 신호(엑셀 이벤트→앱 도착)", signal, 10);
        rep.Time("  ㄴ light 조회 selection.summary 왕복", query, 20);
        rep.Time("  ㄴ 그리기(결과→다음 프레임)", draw, 16.7);
        rep.Check($"선택 {n}번 모두 화면 반영", missing == 0 && failed == 0, $"안 옴 {missing}, 실패 {failed}");
        rep.Check("받은 주소가 고른 주소와 같음", wrong == 0, $"다름 {wrong}");
        BridgeTime(rep, xl);
    }

    private static bool SameAddress(string? got, string asked) => (got ?? "").Replace("$", "") == asked;

    private static void BridgeTime(Report rep, HiddenExcel xl)
    {
        using var doc = JsonDocument.Parse(xl.Query("bridge.stats"));
        var data = doc.RootElement.GetProperty("data");
        var us = data.GetProperty("samplesUs").EnumerateArray().Select(e => e.GetDouble() / 1000.0).ToList();
        rep.Time("다리 이벤트 처리(엑셀 안)", us, 5);
        rep.Check("다리가 틀 창을 찾아 신호를 보냄", data.GetProperty("target").GetBoolean() && data.GetProperty("sent").GetInt32() > 0, $"이벤트 {data.GetProperty("events").GetInt32()}, 보냄 {data.GetProperty("sent").GetInt32()}");
    }

    /// <summary>엑셀이 매크로를 도는 동안 앱이 hello를 부르게 해 거절 코드와 재시도 효과를 본다.</summary>
    public static void Busy(Report rep, HiddenExcel xl, AppUnderTest app)
    {
        Console.WriteLine("호출 거절과 재시도");
        xl.AddBusyMacro();
        // 1) 매크로가 도는 동안 앱이 hello를 부른다: 거절 코드가 나오는지, 기다리는지.
        foreach (int busyMs in new[] { 0, 120, 1000 })
        {
            app.Drain();
            using var probe = new Timer(_ => app.Post(BenchMessages.Probe, xl.Hwnd, Stopwatch.GetTimestamp()), null, 20, Timeout.Infinite);
            using var panel = new Timer(_ => app.Post(BenchMessages.OpenPanel, IntPtr.Zero, Stopwatch.GetTimestamp()), null, 60, Timeout.Infinite);
            long t0 = Stopwatch.GetTimestamp();
            xl.Run("BenchBusy.HunsuBenchBusy", busyMs);
            // 패널 줄이 probe 줄보다 먼저 올 수 있으므로 둘 다 모을 때까지 읽는다(맞지 않는 줄은 WaitFor가 버린다).
            LogLine? p = null, pn = null;
            for (int k = 0; k < 2; k++)
            {
                var l = app.WaitFor(x => x.Name is "probe" or "panel", 4000);
                if (l?.Name == "probe") p = l;
                else if (l?.Name == "panel") pn = l;
            }
            app.Post(BenchMessages.ClosePanel, IntPtr.Zero, 0);
            string detail = p == null ? "응답 없음" : $"ok={p.Get("ok")} hr={p.Get("hr")} 거절종류={p.Get("reject")} 재시도={p.Get("retries")} 걸림 {p.Get("ms")}ms";
            rep.Note($"엑셀 매크로 {busyMs}ms 도는 중 hello: {detail}");
            if (busyMs == 1000)
            {
                rep.Check("매크로 중 호출: 거절 없이 매크로가 끝날 때까지 기다림", p != null && p.Get("ok") == "True" && p.Get("retries") == "0", detail);
                double first = pn == null ? double.NaN : Report.Ms(pn.Long("sent"), pn.Long("first"));
                rep.Check("엑셀이 바쁜 동안에도 화면은 바로 반응(패널 첫 프레임 100ms 이내)", first < 100, double.IsNaN(first) ? "패널 기록 없음" : $"{first:0.0}ms");
            }
            Thread.Sleep(100);
        }
        // 2) 매크로 안에서 칸을 고르고 1200ms 바쁨: 신호는 오지만 조회는 기다린다 → 앱이 600ms(SlowCallMs) 뒤 "바쁨"으로 흐리게, 끝나면 갱신.
        app.Drain();
        long s0 = Stopwatch.GetTimestamp();
        xl.Run("BenchBusy.HunsuBenchSelectBusy", "G7", 1200);
        var slow = app.WaitFor(l => l.Name == "query_slow", 2500);
        var done = app.WaitFor(l => l.Name == "render", 3000);
        rep.Check("바쁜 엑셀: 600ms 뒤 '바쁨' 표시, 끝나면 바로 갱신", slow != null && done != null && done.Get("ok") == "True" && SameAddress(done.Get("addr"), "G7"),
            slow == null ? "바쁨 표시 없음" : $"바쁨 표시 {Report.Ms(s0, slow.Long("at")):0}ms, 갱신 {(done == null ? "없음" : Report.Ms(s0, done.Long("frame")).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "ms")}");
    }

    /// <summary>엑셀 두 개를 번갈아 고르고, 하나를 끄고, 새로 띄워도 섞이거나 끊기지 않는지 본다.</summary>
    public static void Instances(Report rep, HiddenExcel xl1, AppUnderTest app, string xlam)
    {
        Console.WriteLine("엑셀 두 개와 재시작");
        int mixed = 0, missing = 0;
        using (var xl2 = HiddenExcel.Start(xlam))
        {
            for (int i = 0; i < 10; i++)
            {
                // 같은 칸을 다시 고르면 엑셀이 선택 이벤트를 내지 않으므로(0단계에서 봄) 매번 다른 칸을 고른다.
                foreach (var (x, addr) in new[] { (xl2, "D" + (i + 2)), (xl1, "B" + (i + 2)) })
                {
                    app.Drain();
                    x.Select(addr);
                    var r = app.WaitFor(l => l.Name == "render", 3000);
                    if (r == null) missing++;
                    else if (r.Long("excel") != x.Hwnd.ToInt64() || !SameAddress(r.Get("addr"), addr)) mixed++;
                }
            }
            rep.Check("엑셀 두 개: 인스턴스·주소가 섞이지 않음(20번)", mixed == 0 && missing == 0, $"섞임 {mixed}, 안 옴 {missing}");
            var gone = xl2.Hwnd;
            xl2.Dispose();
            app.Drain();
            app.Post(BenchMessages.Probe, gone, Stopwatch.GetTimestamp());
            var p = app.WaitFor(l => l.Name == "probe", 5000);
            rep.Check("꺼진 엑셀 호출 → 오류 코드로 끝남(앱은 살아 있음)", p != null && p.Get("ok") == "False" && app.Alive, p == null ? "응답 없음" : $"hr={p.Get("hr")} code={p.Get("code")}");
        }
        using var xl3 = HiddenExcel.Start(xlam);
        app.Drain();
        xl3.Select("C7");
        var r3 = app.WaitFor(l => IsRender(l, xl3.Hwnd), 5000);
        rep.Check("엑셀 새로 띄움 → 새 인스턴스로 바로 연결", r3 != null && r3.Get("ok") == "True" && SameAddress(r3.Get("addr"), "C7"), r3 == null ? "응답 없음" : $"attach={r3.Get("attach")}");
        app.Drain();
        xl1.Select("E6");
        var r1 = app.WaitFor(l => IsRender(l, xl1.Hwnd), 3000);
        rep.Check("처음 엑셀은 그대로 동작", r1 != null && r1.Get("ok") == "True");
    }
}
