// 기능: "엑셀 대신" 따라갈 시험 창. 측정 도구 프로세스의 별도 스레드에서 메시지 루프를 돌린다(다른 프로세스 창 따라가기를 재현).
//       활성화되지 않는 맨 위 창(WS_EX_NOACTIVATE | TOPMOST)이라 사용자의 포커스를 뺏지 않는다. 엑셀은 보이지 않게 두는 규칙 때문에 쓴다.
// 사용법: using var t = TargetWindow.Create(new PixelRect(100,100,1000,640)); t.Move(rect); t.Minimize(); t.Restore();
// 의존: Win32
using System.Runtime.InteropServices;
using ClockOut.Hunsu.Core;

namespace ClockOut.Hunsu.Bench;

internal sealed class TargetWindow : IDisposable
{
    private const uint WS_POPUP = 0x80000000, WS_EX_TOPMOST = 0x00000008, WS_EX_TOOLWINDOW = 0x00000080, WS_EX_NOACTIVATE = 0x08000000;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new();
    private readonly Win32.WndProc _proc;
    private readonly PixelRect _start;

    private TargetWindow(PixelRect start)
    {
        _start = start;
        _proc = WndProc;
        _thread = new Thread(Loop) { IsBackground = true, Name = "bench-target" };
        _thread.Start();
        _ready.Wait(TimeSpan.FromSeconds(5));
    }

    public IntPtr Hwnd { get; private set; }

    public static TargetWindow Create(PixelRect start) => new(start);

    private void Loop()
    {
        var wc = new Win32.WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<Win32.WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_proc),
            hInstance = Win32.GetModuleHandle(null),
            hbrBackground = Win32.CreateSolidBrush(0x00403A33), // 짙은 회색(엑셀 대신이라는 표시)
            lpszClassName = "ClockOutHunsuBenchTarget",
        };
        Win32.RegisterClassEx(ref wc);
        Hwnd = Win32.CreateWindowEx(WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE, wc.lpszClassName, "훈수꾼 시험 창", WS_POPUP,
            _start.X, _start.Y, _start.Width, _start.Height, IntPtr.Zero, IntPtr.Zero, wc.hInstance, IntPtr.Zero);
        Win32.ShowWindow(Hwnd, Win32.SW_SHOWNOACTIVATE);
        _ready.Set();
        while (Win32.GetMessage(out var m, IntPtr.Zero, 0, 0) > 0)
        {
            Win32.TranslateMessage(ref m);
            Win32.DispatchMessage(ref m);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == Win32.WM_DESTROY) { Win32.PostQuitMessage(0); return IntPtr.Zero; }
        return Win32.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    public void Move(PixelRect r) => Win32.SetWindowPos(Hwnd, IntPtr.Zero, r.X, r.Y, r.Width, r.Height, Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);

    public void Minimize() => Win32.ShowWindow(Hwnd, Win32.SW_MINIMIZE);

    public void Restore() => Win32.ShowWindow(Hwnd, Win32.SW_SHOWNOACTIVATE);

    public void Dispose()
    {
        if (Hwnd != IntPtr.Zero) Win32.PostMessage(Hwnd, Win32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(TimeSpan.FromSeconds(2));
        _ready.Dispose();
    }
}
