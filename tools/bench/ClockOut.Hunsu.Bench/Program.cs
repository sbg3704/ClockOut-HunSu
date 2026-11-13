// 기능: 0단계 측정 도구 시작점. 보이지 않는 엑셀 + 시험 창 + 훈수꾼 앱을 띄워 차례로 재고, 결과를 out\bench에 저장한 뒤 모두 정리한다.
// 사용법: ClockOut.Hunsu.Bench.exe --app <ClockOut.Hunsu.exe> --xlam <clockout-hunsu.xlam> --out <폴더> [--n 100]
//         보통은 tools\bench\run_bench.ps1이 빌드부터 한 번에 한다.
// 의존: HiddenExcel, TargetWindow, AppUnderTest, WindowChecks, ExcelChecks, FrameChecks, Report. 키보드·마우스 조작 없음. 사용자 엑셀은 건드리지 않음.
using System.Diagnostics;
using ClockOut.Hunsu.Bench;
using ClockOut.Hunsu.Core;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch (IOException) { }
        string? exe = Arg(args, "--app"), xlam = Arg(args, "--xlam"), outDir = Arg(args, "--out");
        int n = int.TryParse(Arg(args, "--n"), out var nn) ? nn : 100;
        if (exe == null || xlam == null || outDir == null || !File.Exists(exe) || !File.Exists(xlam))
        {
            Console.Error.WriteLine("사용법: ClockOut.Hunsu.Bench --app <exe> --xlam <xlam> --out <폴더> [--n 100]");
            return 2;
        }
        Directory.CreateDirectory(outDir);
        Watchdog.ArmTimeout(TimeSpan.FromMinutes(6));
        Win32.timeBeginPeriod(1);
        var rep = new Report();
        IntPtr fg = Win32.GetForegroundWindow();
        IntPtr lastFg = fg;
        var fgChanges = new List<string>();
        void Stage(string name)
        {
            var now = Win32.GetForegroundWindow();
            if (now != lastFg) fgChanges.Add($"{name} 뒤 앞 창이 {Win32.ClassOf(now)}(으)로 바뀜");
            lastFg = now;
        }
        HiddenExcel? xl = null; TargetWindow? target = null; AppUnderTest? app = null;
        try
        {
            xl = HiddenExcel.Start(Path.GetFullPath(xlam));
            Stage("엑셀 시작");
            rep.Check("보이지 않는 엑셀 시작, 다리 열기", true, $"엑셀 창 보임={Win32.IsWindowVisible(xl.Hwnd)}");
            target = TargetWindow.Create(new PixelRect(120, 120, 1000, 640));
            string layout = Path.Combine(outDir, "layout.json");
            if (File.Exists(layout)) File.Delete(layout); // 측정용 배치 파일만 지운다(사용자 배치는 %LOCALAPPDATA%에 따로 있음)
            var targetHwnd = target.Hwnd;
            AppUnderTest Launch(string logName) => AppUnderTest.Launch(Path.GetFullPath(exe), targetHwnd, Path.Combine(outDir, logName), Path.Combine(outDir, "snap"), layout);
            app = Launch("app.log");
            var ready = app.WaitFor(l => l.Name == "ready", 15000) ?? throw new InvalidOperationException("앱이 15초 안에 준비되지 않았습니다.");
            Stage("시험 창과 앱 시작");
            app.SignalHwnd = new IntPtr(ready.Long("signal"));
            rep.Check("앱 신호 창 찾기(FindWindow ClockOutHunsuMsg)", Win32.FindWindow(SignalCodec.WindowClass, null) == app.SignalHwnd);
            var baseline = app.WaitFor(l => l.Name == "frame_baseline", 3000);
            if (baseline != null)
                rep.Note($"기준선(아무것도 안 움직일 때 1초): 프레임 {baseline.Get("frames")}개, 평균 간격 {baseline.Get("meanGap")}ms, 가장 긴 간격 {baseline.Get("maxGap")}ms, 25ms 넘은 간격 {baseline.Get("skipped")}번");
            WindowChecks.Run(rep, target, app, ready, xl.Hwnd);
            Stage("오버레이 시험");
            ExcelChecks.Selection(rep, xl, app, n);
            Stage("선택 시험");
            FrameChecks.Panel(rep, app, 30);
            Stage("패널 시험");
            var (pinHwnd, pinRect) = FrameChecks.Pin(rep, app, 20);
            Stage("핀 창 시험");
            FrameChecks.Hotkeys(rep, app, new IntPtr(ready.Long("overlay")), pinHwnd);
            Stage("단축키 시험");
            app = FrameChecks.Restart(rep, app, () => Launch("app2.log"), pinRect);
            Stage("다시 켜기 시험");
            app.Post(BenchMessages.TogglePin, IntPtr.Zero, 0); // 아래 시험을 위해 도크로 되돌린다
            app.WaitFor(l => l.Name == "unpin", 3000);
            ExcelChecks.Busy(rep, xl, app);
            Stage("바쁨 시험");
            ExcelChecks.Instances(rep, xl, app, Path.GetFullPath(xlam));
            Stage("엑셀 여러 개 시험");
            DragChecks.Run(rep, xl, app);
            Stage("큰 범위 끌기 시험");
            rep.Check("측정 중 앱이 죽지 않음", app.Alive);
        }
        catch (Exception ex)
        {
            rep.Check("측정 도구 오류 없이 끝남", false, ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            app?.Dispose();
            target?.Dispose();
            xl?.Dispose();
            Watchdog.KillAll();
            Win32.timeEndPeriod(1);
        }
        Stage("정리");
        rep.Check("측정 도구가 사용자 앞 창(포커스)을 바꾸지 않음", fgChanges.Count == 0, string.Join("; ", fgChanges));
        rep.Note($"측정 도구 자신의 엑셀 호출이 바쁨(VBA_E_IGNORE 등)으로 거절되어 다시 시도한 횟수: {HiddenExcel.BusyRetries}");
        rep.Save(outDir);
        Console.WriteLine();
        Console.WriteLine(rep.Markdown());
        return rep.Failed == 0 ? 0 : 1;
    }

    private static string? Arg(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
