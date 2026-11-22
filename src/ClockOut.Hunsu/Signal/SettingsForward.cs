// 기능: 이미 켜진 훈수꾼에 메시지를 넘긴다. `--settings`가 잠금에 걸리면 "설정 창 열기"(AppMessages.Settings),
//   보통 실행이 잠금에 걸리면 "켜기"(AppMessages.Start, 설정만 띄운 훈수꾼이 이어서 켜짐)를 보낸다(2026-11-22).
// 사용법: if (!SettingsForward.Send()) { /* 켜진 훈수꾼의 신호 창을 못 찾음 */ }   SettingsForward.Send(AppMessages.Start)
// 의존: Native/User32, Core/SignalCodec(신호 창 클래스 이름), Core/AppMessages. 켜진 쪽이 창을 앞으로 가져올 수 있게 AllowSetForegroundWindow를 먼저 부른다.
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Native;

namespace ClockOut.Hunsu.Signal;

internal static class SettingsForward
{
    public static bool Send(string message = AppMessages.Settings)
    {
        IntPtr hwnd = User32.FindWindowEx(IntPtr.Zero, IntPtr.Zero, SignalCodec.WindowClass, null);
        if (hwnd == IntPtr.Zero) return false;
        User32.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid != 0) User32.AllowSetForegroundWindow(pid);
        return User32.PostMessage(hwnd, User32.RegisterWindowMessage(message), IntPtr.Zero, IntPtr.Zero);
    }
}
