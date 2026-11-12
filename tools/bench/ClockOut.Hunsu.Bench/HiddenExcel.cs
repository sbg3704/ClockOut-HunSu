// 기능: 측정용 보이지 않는 엑셀(Visible=False) 하나. 다리 xlam을 열고, 시험용 새 통합 문서(저장 안 함)에 값을 채운다.
//       끝나면 반드시 닫고(Quit) COM을 놓고, 그래도 남으면 이 도구가 띄운 그 프로세스만 강제로 끝낸다.
// 사용법: using var xl = HiddenExcel.Start(xlamPath); xl.Select("B5"); xl.Run("BenchBusy.HunsuBenchBusy", 150); xl.Hwnd
// 의존: 엑셀 데스크톱판 COM, Win32. 사용자가 열어 둔 엑셀은 건드리지 않는다(새 인스턴스를 만든다).
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ClockOut.Hunsu.Bench;

internal sealed class HiddenExcel : IDisposable
{
    private object? _app;
    private object? _sheet;
    private object? _book;

    private HiddenExcel(object app) => _app = app;

    /// <summary>측정 도구 자신의 엑셀 호출이 바쁨으로 거절되어 다시 시도한 횟수(모든 인스턴스 합).</summary>
    public static int BusyRetries { get; private set; }

    public IntPtr Hwnd { get; private set; }
    public int Pid { get; private set; }

    public static HiddenExcel Start(string xlamPath)
    {
        var type = Type.GetTypeFromProgID("Excel.Application") ?? throw new InvalidOperationException("엑셀(데스크톱판)이 설치되어 있지 않습니다.");
        var app = Activator.CreateInstance(type) ?? throw new InvalidOperationException("엑셀을 시작하지 못했습니다.");
        var x = new HiddenExcel(app);
        try
        {
            x.Hwnd = new IntPtr(Convert.ToInt64(Get(app, "Hwnd"), CultureInfo.InvariantCulture));
            Win32.GetWindowThreadProcessId(x.Hwnd, out uint pid);
            x.Pid = (int)pid;
            Watchdog.Track(x.Pid);
            Set(app, "Visible", false);
            Set(app, "DisplayAlerts", false);
            var books = Get(app, "Workbooks")!;
            Call(books, "Open", xlamPath);
            x._book = Call(books, "Add");
            x._sheet = Get(Get(x._book!, "Worksheets")!, "Item", 1);
            x.FillSample();
            Call(app, "Run", "'clockout-hunsu.xlam'!CoHunsu_Start");
            return x;
        }
        catch
        {
            x.Dispose(); // 시작 중 실패해도 띄운 엑셀은 닫는다
            throw;
        }
    }

    /// <summary>숫자, 글자, 수식이 섞인 20x10 표(1천 칸 미만, docs/04 5장의 작은 시트).</summary>
    private void FillSample()
    {
        var data = new object[20, 10];
        for (int r = 0; r < 20; r++)
            for (int c = 0; c < 10; c++)
                data[r, c] = (r + c) % 7 == 0 ? "글" + r : (object)(r * 10 + c + 0.5);
        Set(Get(_sheet!, "Range", "A1:J20")!, "Value2", data);
        Set(Get(_sheet!, "Range", "L1")!, "Formula", "=SUM(A1:J20)");
    }

    /// <summary>시험 문서에 바쁨을 만드는 매크로를 넣는다(VBA 프로젝트 접근 신뢰가 켜져 있어야 함).</summary>
    public void AddBusyMacro()
    {
        var comps = Get(Get(_book!, "VBProject")!, "VBComponents")!;
        var mod = Call(comps, "Add", 1)!;
        Set(mod, "Name", "BenchBusy");
        Call(Get(mod, "CodeModule")!, "AddFromString",
            "Public Sub HunsuBenchBusy(ByVal ms As Long)\r\n    Dim t As Double\r\n    t = Timer\r\n    Do While (Timer - t) * 1000 < ms\r\n    Loop\r\nEnd Sub\r\n" +
            "Public Sub HunsuBenchSelectBusy(ByVal addr As String, ByVal ms As Long)\r\n    ActiveSheet.Range(addr).Select\r\n    HunsuBenchBusy ms\r\nEnd Sub\r\n");
    }

    public void Select(string addr) => Call(Get(_sheet!, "Range", addr)!, "Select");

    public object? Run(string macro, params object[] args)
    {
        var all = new object[args.Length + 1];
        all[0] = macro;
        Array.Copy(args, 0, all, 1, args.Length);
        return Call(_app!, "Run", all);
    }

    public string Query(string name, string args = "{}") => Run("'clockout-hunsu.xlam'!CoHunsu_Query", name, args) as string ?? "";

    public void Dispose()
    {
        if (_app == null) return;
        try
        {
            if (_book != null) { Set(_book, "Saved", true); Call(_book, "Close", false); }
            Call(_app, "Quit");
        }
        catch (COMException) { /* 이미 끊김: 아래에서 프로세스를 확인한다 */ }
        foreach (var o in new[] { _sheet, _book, _app }) if (o != null) Marshal.FinalReleaseComObject(o);
        _sheet = _book = _app = null;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Watchdog.EndProcess(Pid, TimeSpan.FromSeconds(8));
    }

    // 엑셀 COM은 LCID가 맞지 않으면 TYPE_E_INVDATAREAD(0x80028018)로 실패한다(0단계에서 실제로 봄). 늘 지원되는 en-US로 고정.
    private static readonly CultureInfo ExcelCulture = CultureInfo.GetCultureInfo(1033);

    public static object? Get(object o, string name, params object[] args) => Invoke(o, name, BindingFlags.GetProperty, args);
    public static void Set(object o, string name, object value) => Invoke(o, name, BindingFlags.SetProperty, [value]);
    public static object? Call(object o, string name, params object[] args) => Invoke(o, name, BindingFlags.InvokeMethod, args);

    private static object? Invoke(object o, string name, BindingFlags flags, object[] args)
    {
        // 엑셀이 막 켜졌거나 바쁠 때 VBA_E_IGNORE(0x800AC472) 등으로 거절한다. 측정 도구 자신의 호출은 2초까지 다시 시도한다.
        for (int attempt = 0; ; attempt++)
        {
            try { return o.GetType().InvokeMember(name, flags, null, o, args, ExcelCulture); }
            catch (TargetInvocationException ex) when (ex.InnerException is COMException ce && Core.HResultNames.IsBusy(ce.HResult) && attempt < 40)
            {
                BusyRetries++;
                Thread.Sleep(50);
            }
            catch (COMException ce) when (Core.HResultNames.IsBusy(ce.HResult) && attempt < 40)
            {
                BusyRetries++;
                Thread.Sleep(50);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }
    }
}

/// <summary>이 도구가 띄운 프로세스(엑셀, 앱)만 기억했다가, 멈추거나 끝날 때 정리한다.</summary>
internal static class Watchdog
{
    private static readonly List<int> Pids = [];

    public static void Track(int pid) { lock (Pids) Pids.Add(pid); }

    public static void EndProcess(int pid, TimeSpan wait)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            if (!p.WaitForExit(wait)) p.Kill();
        }
        catch (ArgumentException) { /* 이미 끝남 */ }
        catch (InvalidOperationException) { /* 이미 끝남 */ }
        lock (Pids) Pids.Remove(pid);
    }

    /// <summary>비상 정리: 남아 있는 것만 강제로 끝낸다.</summary>
    public static void KillAll()
    {
        int[] left;
        lock (Pids) left = [.. Pids];
        foreach (var pid in left) EndProcess(pid, TimeSpan.Zero);
    }

    /// <summary>limit이 지나도 끝나지 않으면 전부 정리하고 프로세스를 끝낸다(측정 도구가 멈춘 경우).</summary>
    public static void ArmTimeout(TimeSpan limit)
    {
        var t = new Thread(() =>
        {
            Thread.Sleep(limit);
            Console.Error.WriteLine("[비상] 시간 초과: 이 도구가 띄운 엑셀과 앱을 정리하고 끝냅니다.");
            KillAll();
            Environment.Exit(3);
        }) { IsBackground = true };
        t.Start();
    }
}
