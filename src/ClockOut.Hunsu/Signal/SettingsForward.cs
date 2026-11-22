// 기능: 이미 켜진 훈수꾼에 "설정 창 열기"(AppMessages.Settings)를 넘긴다. `ClockOut.Hunsu.exe --settings`를 켰는데 단일 실행 잠금에 걸렸을 때 쓴다(2026-11-22).
// 사용법: if (!SettingsForward.Send()) { /* 켜진 훈수꾼의 신호 창을 못 찾음 */ }
// 의존: Native/User32, Core/SignalCodec(신호 창 클래스 이름), Core/AppMessages. 켜진 쪽이 창을 앞으로 가져올 수 있게 AllowSetForegroundWindow를 먼저 부른다.
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Native;

namespace ClockOut.Hunsu.Signal;

internal static class SettingsForward
{
    public static bool Send()
    {
        IntPtr hwnd = User32.FindWindowEx(IntPtr.Zero, IntPtr.Zero, SignalCodec.WindowClass, null);
        if (hwnd == IntPtr.Zero) return false;
        User32.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid != 0) User32.AllowSetForegroundWindow(pid);
        return User32.PostMessage(hwnd, User32.RegisterWindowMessage(AppMessages.Settings), IntPtr.Zero, IntPtr.Zero);
    }
}
