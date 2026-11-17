// 기능: 실행 중인 EXCEL.EXE 목록(화면에 보이는 창이 있는지 포함)과 끝내기. 엑셀에 COM으로 말을 걸지 않는다(프로세스 정보와 창 목록만).
//       닫은 엑셀이 작업 관리자에 남는 찌꺼기를 자동으로 끝내는 일(OrphanTracker)과 [남은 엑셀 정리] 창이 같이 쓴다.
// 사용법: List<ExcelProc> list = ExcelProcesses.Snapshot();  bool ok = ExcelProcesses.End(pid, startTicks);
// 의존: Core/Procs/OrphanTracker(ExcelProc), Native/User32. 이 PC의 같은 사용자 세션 것만 본다. 접근이 막힌 프로세스는 목록에서 뺀다.
using System.ComponentModel;
using System.Diagnostics;
using ClockOut.Hunsu.Core.Procs;
using ClockOut.Hunsu.Native;

namespace ClockOut.Hunsu.Excel;

internal static class ExcelProcesses
{
    public static List<ExcelProc> Snapshot()
    {
        var visible = new HashSet<int>();
        User32.EnumWindows((hwnd, _) =>
        {
            if (User32.IsWindowVisible(hwnd) && User32.ClassOf(hwnd) == "XLMAIN")
            {
                User32.GetWindowThreadProcessId(hwnd, out uint pid);
                visible.Add((int)pid);
            }
            return true;
        }, IntPtr.Zero);

        var list = new List<ExcelProc>();
        int mySession = Process.GetCurrentProcess().SessionId;
        foreach (var p in Process.GetProcessesByName("EXCEL"))
        {
            using (p)
            {
                try
                {
                    if (p.SessionId != mySession || p.HasExited) continue;
                    bool shown = visible.Contains(p.Id);
                    list.Add(new ExcelProc(p.Id, p.StartTime.ToUniversalTime().Ticks, shown, shown ? p.MainWindowTitle : ""));
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                {
                    // 끝났거나 접근할 수 없는 프로세스는 건너뛴다.
                }
            }
        }
        return list;
    }

    /// <summary>그 엑셀 프로세스를 끝낸다. 번호가 다른 프로세스에 다시 쓰이지 않았는지 시작 시각으로 확인한다. 끝냈거나 이미 없으면 true.</summary>
    public static bool End(int pid, long startTicks)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            if (!string.Equals(p.ProcessName, "EXCEL", StringComparison.OrdinalIgnoreCase) || p.StartTime.ToUniversalTime().Ticks != startTicks) return true;
            p.Kill();
            return p.WaitForExit(3000);
        }
        catch (ArgumentException)
        {
            return true; // 이미 없다
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}
