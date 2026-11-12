// 기능: 오버레이 창 시험 — 클릭 통과(점 아래 창 찾기로, 마우스 없이), 따라가기 어긋남 시간, 최소화·복원, 숨은 엑셀 창의 이동 이벤트.
// 사용법: WindowChecks.Run(report, target, app, ready, excelHwnd)
// 의존: Win32, TargetWindow, AppUnderTest, Report, Core/OverlayPlacement
using System.Diagnostics;
using ClockOut.Hunsu.Core;

namespace ClockOut.Hunsu.Bench;

internal static class WindowChecks
{
    private const long WS_EX_NOACTIVATE = 0x08000000;

    public static void Run(Report rep, TargetWindow target, AppUnderTest app, LogLine ready, IntPtr excelHwnd)
    {
        var overlay = new IntPtr(ready.Long("overlay"));
        PixelRect.TryParse(ready.Get("zone"), out var zone);
        Console.WriteLine("오버레이 창");
        CheckPlacementAndClicks(rep, target, overlay, zone);
        MeasureFollow(rep, target, overlay, app);
        CheckMinimize(rep, target, overlay);
        CheckExcelWindowEvents(rep, excelHwnd);
    }

    private static PixelRect Expected(TargetWindow t, IntPtr overlay)
    {
        var o = Win32.Rect(overlay);
        var tr = Win32.Rect(t.Hwnd);
        return OverlayPlacement.TopRightInside(tr, o.Width, OverlayPlacement.DockHeight(tr, 200)); // 도크 창은 높이도 엑셀 창을 따라 바뀐다(1단계)
    }

    private static void CheckPlacementAndClicks(Report rep, TargetWindow target, IntPtr overlay, PixelRect zone)
    {
        var o = Win32.Rect(overlay);
        rep.Check("오버레이가 뜸(보임)", Win32.IsWindowVisible(overlay), $"크기 {o.Width}x{o.Height}");
        rep.Check("시험 창 오른쪽 위에 붙음", o == Expected(target, overlay), $"오버레이 {o}, 기대 {Expected(target, overlay)}");
        long ex = Win32.GetWindowLongPtr(overlay, -20).ToInt64();
        rep.Check("활성화 안 됨(WS_EX_NOACTIVATE)", (ex & WS_EX_NOACTIVATE) != 0);
        // 투명 구역: 오버레이 왼쪽 아래 근처 → 아래 창(시험 창)이 잡혀야 한다.
        var under = Win32.RootAt(o.X + 12, o.Bottom - 12);
        rep.Check("투명 구역 클릭 통과", under == target.Hwnd, $"점 아래 창 = {(under == target.Hwnd ? "시험 창" : under == overlay ? "오버레이" : "다른 창 " + under)}");
        var zx = o.X + zone.X + zone.Width / 2;
        var zy = o.Y + zone.Y + zone.Height / 2;
        var onZone = Win32.RootAt(zx, zy);
        rep.Check("알약 구역은 클릭 받음", onZone == overlay, $"구역 {zone}, 점 아래 창 = {(onZone == overlay ? "오버레이" : onZone == target.Hwnd ? "시험 창" : "다른 창")}");
        // 진단: 오버레이 전체를 8px 간격으로 훑어 WindowFromPoint가 오버레이를 잡는 점이 몇 개인지 센다.
        int hits = 0, inZone = 0, total = 0;
        for (int y = o.Y + 2; y < o.Bottom; y += 8)
            for (int x = o.X + 2; x < o.Right; x += 8)
            {
                total++;
                if (Win32.RootAt(x, y) != overlay) continue;
                hits++;
                if (zone.Contains(x - o.X, y - o.Y)) inZone++;
            }
        rep.Note($"WindowFromPoint 훑기: 오버레이 {total}점 중 오버레이가 잡힌 점 {hits}개(그중 알약 안 {inZone}개). 확장 스타일 0x{ex:X8}");
    }

    private static void MeasureFollow(Report rep, TargetWindow target, IntPtr overlay, AppUnderTest app)
    {
        var start = Win32.Rect(target.Hwnd);
        var samples = new List<double>();
        var delivery = new List<double>();
        var apply = new List<double>();
        int misses = 0;
        app.Drain();
        for (int i = 0; i < 200; i++)
        {
            bool resize = i % 25 == 24;
            var r = new PixelRect(start.X + (i % 20) * 7, start.Y + (i % 10) * 5, start.Width - (resize ? 120 : 0), start.Height - (resize ? 60 : 0));
            long t0 = Stopwatch.GetTimestamp();
            target.Move(r);
            var want = Expected(target, overlay);
            long deadline = t0 + Stopwatch.Frequency / 5; // 200ms
            long t1 = 0;
            while (Stopwatch.GetTimestamp() < deadline)
            {
                if (Win32.Rect(overlay) == want) { t1 = Stopwatch.GetTimestamp(); break; }
                Thread.SpinWait(50);
            }
            if (t1 == 0) misses++;
            else samples.Add(Report.Ms(t0, t1));
            // 앱 기록으로 구간을 나눈다: 이벤트 도착(t0) - 옮긴 시각, 앱이 자리 옮기기를 끝낸 시각(at) - 도착.
            var f = app.WaitFor(l => l.Name == "follow" && l.Get("rect") == want.ToString(), 300);
            if (f != null)
            {
                delivery.Add(Report.Ms(t0, f.Long("t0")));
                apply.Add(Report.Ms(f.Long("t0"), f.Long("at")));
            }
            if (resize) target.Move(start);
            app.Drain();
        }
        target.Move(start);
        rep.Time("창 이동 → 오버레이 따라옴", samples, 1000.0 / 60);
        rep.Time("  ㄴ 창 이벤트 도착(옮김→앱 콜백)", delivery, 1000.0 / 60);
        rep.Time("  ㄴ 앱이 오버레이 옮기기(SetWindowPos)", apply, 1000.0 / 60);
        rep.Check("따라가기 200번 모두 제자리", misses == 0, $"놓침 {misses}");
    }

    private static void CheckMinimize(Report rep, TargetWindow target, IntPtr overlay)
    {
        target.Minimize();
        double hideMs = WaitUntil(() => !Win32.IsWindowVisible(overlay), 1000);
        rep.Check("시험 창 최소화 → 오버레이 숨음", hideMs >= 0, hideMs >= 0 ? $"{hideMs:0.0}ms" : "1초 안에 안 숨음");
        target.Restore();
        double showMs = WaitUntil(() => Win32.IsWindowVisible(overlay) && Win32.Rect(overlay) == Expected(target, overlay), 1000);
        rep.Check("복원 → 오버레이 다시 제자리", showMs >= 0, showMs >= 0 ? $"{showMs:0.0}ms" : "1초 안에 안 돌아옴");
    }

    /// <summary>숨은 엑셀의 주 창을 직접 옮겨, 다른 프로세스(엑셀) 창의 이동 이벤트가 오는지 본다(보이는 엑셀을 쓰지 않는 대신).</summary>
    private static void CheckExcelWindowEvents(Report rep, IntPtr xl)
    {
        Win32.GetWindowThreadProcessId(xl, out uint pid);
        int count = 0;
        Win32.WinEventProc proc = (_, _, hwnd, obj, child, _, _) => { if (hwnd == xl && obj == 0 && child == 0) count++; };
        var hook = Win32.SetWinEventHook(0x800B, 0x800B, IntPtr.Zero, proc, pid, 0, 0);
        var r = Win32.Rect(xl);
        Win32.SetWindowPos(xl, IntPtr.Zero, r.X + 10, r.Y + 10, r.Width, r.Height, Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
        Win32.Pump(150);
        Win32.SetWindowPos(xl, IntPtr.Zero, r.X, r.Y, r.Width, r.Height, Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
        Win32.Pump(150);
        Win32.UnhookWinEvent(hook);
        GC.KeepAlive(proc);
        rep.Check("엑셀 주 창(숨김) 이동 이벤트 받음", count >= 2, $"이벤트 {count}개(옮김 2번)");
    }

    public static double WaitUntil(Func<bool> cond, int timeoutMs)
    {
        long t0 = Stopwatch.GetTimestamp();
        long deadline = t0 + Stopwatch.Frequency * timeoutMs / 1000;
        while (Stopwatch.GetTimestamp() < deadline)
        {
            if (cond()) return Report.Ms(t0, Stopwatch.GetTimestamp());
            Thread.SpinWait(50);
        }
        return -1;
    }
}
