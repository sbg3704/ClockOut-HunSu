// 기능: 화면에 보이는 엑셀 주 창(클래스 XLMAIN)을 창 목록에서 찾는다. COM을 쓰지 않으므로 엑셀에 아무 영향이 없다.
// 사용법: IntPtr hwnd = ExcelWindows.FindVisibleMain();  없으면 IntPtr.Zero
// 의존: Native/User32
using ClockOut.Hunsu.Native;

namespace ClockOut.Hunsu.Excel;

internal static class ExcelWindows
{
    public static IntPtr FindVisibleMain()
    {
        IntPtr found = IntPtr.Zero;
        User32.EnumWindows((hwnd, _) =>
        {
            if (User32.IsWindowVisible(hwnd) && User32.ClassOf(hwnd) == "XLMAIN")
            {
                found = hwnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
