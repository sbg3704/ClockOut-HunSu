// 기능: 윈도우 앱 바(SHAppBarMessage) 등록. 화면 가장자리에 폭을 예약해 최대화된 창(엑셀 포함)이 남은 영역으로 저절로 줄어들게 한다(VS Code·비주얼 스튜디오의 사이드바처럼 창 밖에 붙는다).
// 사용법: AppBar.Register(hwnd, callbackMsg) → AppBar.Place(hwnd, left, monitorBounds, workArea, widthPx)로 자리를 받아 SetWindowPos → 끝낼 때 AppBar.Remove(hwnd). 다른 앱 바가 바뀌면 callbackMsg(ABN_POSCHANGED)로 알려 주니 다시 Place.
// 의존: Native/User32.RECT. 등록한 채 프로세스가 죽으면 윈도우가 예약을 풀어 준다(작업 표시줄이 정리). 화면 좌표는 실제 픽셀.
using System.Runtime.InteropServices;

namespace ClockOut.Hunsu.Native;

internal static class AppBar
{
    private const uint ABM_NEW = 0, ABM_REMOVE = 1, ABM_QUERYPOS = 2, ABM_SETPOS = 3;
    private const uint ABE_LEFT = 0, ABE_RIGHT = 2;

    /// <summary>앱 바 알림(callbackMsg의 wParam): 다른 앱 바·작업 표시줄이 바뀌어 다시 자리를 잡아야 한다.</summary>
    public const int ABN_POSCHANGED = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public User32.RECT rc;
        public IntPtr lParam;
    }

    [DllImport("shell32.dll")] private static extern UIntPtr SHAppBarMessage(uint msg, ref APPBARDATA data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterWindowMessage(string name);

    private static APPBARDATA Data(IntPtr hwnd) => new() { cbSize = Marshal.SizeOf<APPBARDATA>(), hWnd = hwnd };

    public static bool Register(IntPtr hwnd, uint callbackMsg)
    {
        var d = Data(hwnd);
        d.uCallbackMessage = callbackMsg;
        return SHAppBarMessage(ABM_NEW, ref d) != UIntPtr.Zero;
    }

    public static void Remove(IntPtr hwnd)
    {
        var d = Data(hwnd);
        SHAppBarMessage(ABM_REMOVE, ref d);
    }

    /// <summary>가장자리(left/right)에 widthPx 폭을 예약하고 실제로 받은 사각형(화면 픽셀)을 돌려준다. 위아래는 작업 영역(작업 표시줄 제외), 좌우는 모니터 전체 기준.</summary>
    public static User32.RECT Place(IntPtr hwnd, bool left, System.Drawing.Rectangle monitor, System.Drawing.Rectangle work, int widthPx)
    {
        var d = Data(hwnd);
        d.uEdge = left ? ABE_LEFT : ABE_RIGHT;
        d.rc = new User32.RECT { Left = monitor.Left, Top = work.Top, Right = monitor.Right, Bottom = work.Bottom };
        SHAppBarMessage(ABM_QUERYPOS, ref d);
        if (left) d.rc.Right = d.rc.Left + widthPx; else d.rc.Left = d.rc.Right - widthPx;
        SHAppBarMessage(ABM_SETPOS, ref d);
        return d.rc;
    }
}
