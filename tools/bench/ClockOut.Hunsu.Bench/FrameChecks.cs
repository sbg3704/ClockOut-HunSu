// 기능: 1단계 틀 시험 — 패널 펼치기(접힌 손잡이에서), 도크 ↔ 핀 창 이동 시간과 프레임 간격, 핀 창 클릭 통과·부르기 중 클릭 받기,
//       전부 숨김(WM_HOTKEY 메시지로, 키보드 없이), 다시 켰을 때 같은 자리(배치 저장).
// 사용법: FrameChecks.Panel(rep, app, 30); FrameChecks.Pin(rep, app, 20); FrameChecks.Hotkeys(rep, app); app = FrameChecks.Restart(rep, app, relaunch)
// 의존: AppUnderTest, Report, Win32, Core/BenchMessages, Core/LogLine
using System.Diagnostics;
using ClockOut.Hunsu.Core;

namespace ClockOut.Hunsu.Bench;

internal static class FrameChecks
{
    private const uint WM_HOTKEY = 0x0312;
    private const long WS_EX_TRANSPARENT = 0x20;

    public static void Panel(Report rep, AppUnderTest app, int n)
    {
        Console.WriteLine("패널 펼치기(손잡이 → 패널)");
        var first = new List<double>(); var done = new List<double>(); var gaps = new List<double>(); var steady = new List<double>(); var mean = new List<double>(); int skipped = 0;
        for (int i = 0; i < n; i++)
        {
            app.Post(BenchMessages.ClosePanel, IntPtr.Zero, 0);
            Thread.Sleep(150);
            app.Drain();
            long t0 = Stopwatch.GetTimestamp();
            app.Post(BenchMessages.OpenPanel, IntPtr.Zero, t0);
            var l = app.WaitFor(x => x.Name == "panel", 3000);
            if (l == null) continue;
            first.Add(Report.Ms(t0, l.Long("first")));
            done.Add(Report.Ms(t0, l.Long("done")));
            gaps.Add(l.Double("maxGap"));
            steady.Add(l.Double("steadyGap"));
            mean.Add(l.Double("meanGap"));
            skipped += (int)l.Long("skipped");
        }
        rep.Time("패널 열기: 첫 프레임(반응 시작)", first, 100);
        rep.Time("패널 열기: 마지막 프레임(fast 120ms 움직임 포함)", done, 100);
        rep.Time("패널 열기: 첫 프레임 간격(투명도 0, 배치·첫 그리기)", gaps, 1000.0 / 60 * 2);
        rep.Time("패널 열기: 움직이는 동안 가장 긴 프레임 간격", steady, 1000.0 / 60 * 1.5);
        rep.Time("패널 열기: 움직이는 동안 평균 프레임 간격(60fps면 16.7)", mean, 1000.0 / 60 * 1.1);
        rep.Note($"패널 열기 30회 중 한 프레임 반(25ms)을 넘은 간격 {skipped}번. 아무것도 안 움직일 때의 흔들림은 아래 '기준선' 관찰과 비교");
    }

    /// <summary>도크 → 핀 창 → 도크를 n번. 핀 창이 뜨는 시간, 되돌아오는 시간, 그동안 프레임 간격.</summary>
    public static (IntPtr Hwnd, string Rect) Pin(Report rep, AppUnderTest app, int n)
    {
        Console.WriteLine("도크 ↔ 핀 창");
        var pin = new List<double>(); var unpin = new List<double>(); var gaps = new List<double>(); var steady = new List<double>(); var mean = new List<double>(); int skipped = 0; IntPtr pinHwnd = IntPtr.Zero; string rect = "-";
        for (int i = 0; i < n * 2; i++)
        {
            app.Drain();
            long t0 = Stopwatch.GetTimestamp();
            app.Post(BenchMessages.TogglePin, IntPtr.Zero, t0);
            var l = app.WaitFor(x => x.Name is "pin" or "unpin", 3000);
            if (l == null) continue;
            (l.Name == "pin" ? pin : unpin).Add(Report.Ms(t0, l.Long("done")));
            gaps.Add(l.Double("maxGap"));
            steady.Add(l.Double("steadyGap"));
            mean.Add(l.Double("meanGap"));
            skipped += (int)l.Long("skipped");
            if (l.Name == "pin") { pinHwnd = new IntPtr(l.Long("pinHwnd")); rect = l.Get("rect") ?? "-"; }
            Thread.Sleep(60);
        }
        rep.Time("도크 → 핀 창(움직임 끝 프레임까지)", pin, 200);
        rep.Time("핀 창 → 도크(움직임 끝 프레임까지)", unpin, 200);
        rep.Time("도크 ↔ 핀 창: 첫 프레임 간격 포함 최대", gaps, 1000.0 / 60 * 2);
        rep.Time("도크 ↔ 핀 창: 움직이는 동안 가장 긴 프레임 간격", steady, 1000.0 / 60 * 1.5);
        rep.Time("도크 ↔ 핀 창: 움직이는 동안 평균 프레임 간격", mean, 1000.0 / 60 * 1.1);
        rep.Check($"도크 ↔ 핀 창 {n}번 왕복 모두 끝남", pin.Count == n && unpin.Count == n, $"핀 {pin.Count}, 되돌림 {unpin.Count}, 25ms 넘은 간격 {skipped}번");
        // 마지막에 핀 창 상태로 한 번 더 두고 아래 시험에 쓴다.
        app.Drain();
        app.Post(BenchMessages.TogglePin, IntPtr.Zero, Stopwatch.GetTimestamp());
        var last = app.WaitFor(x => x.Name == "pin", 3000);
        if (last != null) { pinHwnd = new IntPtr(last.Long("pinHwnd")); rect = last.Get("rect") ?? rect; }
        rep.Note($"핀 창 자리(실제 픽셀): {rect}");
        return (pinHwnd, rect);
    }

    /// <summary>핀 창: 평소 클릭 통과, 부르기(WM_HOTKEY 1) 중 클릭 받음. 전부 숨김(WM_HOTKEY 2) → 숨고 다시 보임.</summary>
    public static void Hotkeys(Report rep, AppUnderTest app, IntPtr dock, IntPtr pin)
    {
        Console.WriteLine("부르기와 전부 숨김(단축키 메시지)");
        if (pin == IntPtr.Zero || !Win32.IsWindow(pin)) { rep.Check("핀 창 있음", false); return; }
        var r = Win32.Rect(pin);
        int cx = r.X + r.Width / 2, ty = r.Y + 12 + 18; // 그림자 여백 12 + 제목줄 가운데
        rep.Check("핀 창 평소: 클릭 통과(WS_EX_TRANSPARENT)", (Win32.GetWindowLongPtr(pin, -20).ToInt64() & WS_EX_TRANSPARENT) != 0 && Win32.RootAt(cx, ty) != pin);
        app.Drain();
        app.PostRaw(WM_HOTKEY, new IntPtr(1), IntPtr.Zero);
        var on = app.WaitFor(l => l.Name == "summon", 2000);
        double ms = WindowChecks.WaitUntil(() => Win32.RootAt(cx, ty) == pin, 500);
        rep.Check("부르기(Ctrl+Alt+H 메시지): 핀 창이 클릭을 받음", on?.Get("on") == "True" && ms >= 0, ms >= 0 ? $"{ms:0.0}ms" : "클릭 못 받음");
        app.PostRaw(WM_HOTKEY, new IntPtr(1), IntPtr.Zero);
        app.WaitFor(l => l.Name == "summon", 2000);
        rep.Check("부르기 닫기: 다시 클릭 통과", WindowChecks.WaitUntil(() => Win32.RootAt(cx, ty) != pin, 500) >= 0);

        app.PostRaw(WM_HOTKEY, new IntPtr(2), IntPtr.Zero);
        double hide = WindowChecks.WaitUntil(() => !Win32.IsWindowVisible(dock) && !Win32.IsWindowVisible(pin), 1000);
        rep.Check("전부 숨김(Ctrl+Alt+Shift+H 메시지): 도크와 핀 창이 숨음", hide >= 0, hide >= 0 ? $"{hide:0.0}ms" : "안 숨음");
        app.PostRaw(WM_HOTKEY, new IntPtr(2), IntPtr.Zero);
        double show = WindowChecks.WaitUntil(() => Win32.IsWindowVisible(dock) && Win32.IsWindowVisible(pin), 1000);
        rep.Check("다시 누르면 다시 보임", show >= 0, show >= 0 ? $"{show:0.0}ms" : "안 보임");
    }

    /// <summary>핀 창 상태로 앱을 끝내고 다시 켜면 같은 자리에 핀 창으로 나오는지(배치 저장). 새 앱을 돌려준다.</summary>
    public static AppUnderTest Restart(Report rep, AppUnderTest app, Func<AppUnderTest> relaunch, string expectedRect)
    {
        Console.WriteLine("다시 켜도 같은 자리");
        app.Drain();
        app.Post(BenchMessages.Quit, IntPtr.Zero, 0);
        bool exited = app.WaitExit(5000);
        app.Dispose();
        var next = relaunch();
        var ready = next.WaitFor(l => l.Name == "ready", 15000);
        next.SignalHwnd = new IntPtr(ready?.Long("signal") ?? 0);
        // ready 앞에 module_placed가 쓰이지 않도록 ShellController는 ready 뒤에 배치를 기록한다.
        var placed = next.WaitFor(l => l.Name == "module_placed" && l.Get("id") == "hunsu-selection", 3000);
        rep.Check("끝내기 메시지로 깨끗이 끝남(배치 저장)", exited);
        rep.Check("다시 켜면 핀 창이 저장된 자리에", placed?.Get("place") == "pin" && placed.Get("rect") == expectedRect, placed == null ? "기록 없음" : $"place={placed.Get("place")} rect={placed.Get("rect")}, 끝내기 전 {expectedRect}");
        return next;
    }
}
