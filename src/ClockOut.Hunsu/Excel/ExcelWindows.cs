// 기능: 엑셀 주 창(클래스 XLMAIN)을 창 목록에서 찾는다. COM을 쓰지 않으므로 엑셀에 아무 영향이 없다. 화면에 보이는 첫 창 찾기, 모든 주 창 목록(연결 창용), 한 프로세스의 주 창, 화면 한 점 아래의 엑셀 창(창 눌러 고르기용).
// 사용법: IntPtr hwnd = ExcelWindows.FindVisibleMain();  List<ExcelWin> all = ExcelWindows.List();  IntPtr h = ExcelWindows.MainOfProcess(pid);  IntPtr p = ExcelWindows.MainAtPoint(x, y);  없으면 IntPtr.Zero
// 의존: Native/User32
using System.Diagnostics;
using ClockOut.Hunsu.Native;

namespace ClockOut.Hunsu.Excel;

/// <summary>엑셀 주 창 하나: 창 핸들, 프로세스 번호와 시작 시각(틱), 화면에 보이는지, 제목(보통 "통합문서 - Excel").</summary>
internal sealed record ExcelWin(IntPtr Hwnd, int Pid, long StartTicks, bool Visible, string Title);

internal static class ExcelWindows
{
    public const string MainClass = "XLMAIN";

    public static IntPtr FindVisibleMain()
    {
        IntPtr found = IntPtr.Zero;
        User32.EnumWindows((hwnd, _) =>
        {
            if (User32.IsWindowVisible(hwnd) && User32.ClassOf(hwnd) == MainClass)
            {
                found = hwnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    /// <summary>모든 엑셀 주 창(숨은 것 포함). 접근할 수 없는 프로세스의 창은 시작 시각을 0으로 둔다.</summary>
    public static List<ExcelWin> List()
    {
        var wins = new List<ExcelWin>();
        User32.EnumWindows((hwnd, _) =>
        {
            if (User32.ClassOf(hwnd) != MainClass) return true;
            User32.GetWindowThreadProcessId(hwnd, out uint pid);
            var sb = new System.Text.StringBuilder(256);
            User32.GetWindowText(hwnd, sb, sb.Capacity);
            wins.Add(new ExcelWin(hwnd, (int)pid, StartTicks((int)pid), User32.IsWindowVisible(hwnd), sb.ToString()));
            return true;
        }, IntPtr.Zero);
        return wins;
    }

    /// <summary>그 프로세스의 엑셀 주 창(보이는 것 우선). 시작 시각이 주어지면 같은 프로세스인지도 확인한다(번호 재사용 방지).</summary>
    public static IntPtr MainOfProcess(int pid, long startTicks = 0)
    {
        var mine = List().Where(w => w.Pid == pid && (startTicks == 0 || w.StartTicks == startTicks)).ToList();
        return (mine.FirstOrDefault(w => w.Visible) ?? mine.FirstOrDefault())?.Hwnd ?? IntPtr.Zero;
    }

    /// <summary>화면 한 점(실제 픽셀) 아래에 있는 창의 맨 위 창이 엑셀 주 창이면 그 핸들, 아니면 IntPtr.Zero.</summary>
    public static IntPtr MainAtPoint(int x, int y)
    {
        var h = User32.WindowFromPoint(new User32.POINT { X = x, Y = y });
        if (h == IntPtr.Zero) return IntPtr.Zero;
        var root = User32.GetAncestor(h, User32.GA_ROOT);
        return root != IntPtr.Zero && User32.ClassOf(root) == MainClass ? root : IntPtr.Zero;
    }

    private static long StartTicks(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return p.StartTime.ToUniversalTime().Ticks;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return 0;
        }
    }
}
