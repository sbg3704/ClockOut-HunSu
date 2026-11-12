// 기능: 다리가 PostMessage로 신호를 보내는 숨은 창(클래스 ClockOutHunsuMsg)을 만들고, 받은 신호를 이벤트로 넘긴다(docs/02 4장 1번).
//       측정 도구의 등록 메시지(Core/BenchMessages)도 같은 창으로 받는다.
// 사용법: var w = new SignalWindow(log); w.Signal += (excelHwnd, code, seq, at) => ...; w.Bench += (kind, wp, lp, at) => ...
//         화면 스레드에서 만든다. 그 스레드의 메시지 루프(WPF Dispatcher)가 이 창의 메시지도 돌린다.
// 의존: Native/User32, Core/SignalCodec, Core/BenchMessages. 다리는 FindWindow(클래스 이름)로 이 창을 찾는다.
using System.Diagnostics;
using System.Runtime.InteropServices;
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Diagnostics;
using ClockOut.Hunsu.Native;

namespace ClockOut.Hunsu.Signal;

internal sealed class SignalWindow : IDisposable
{
    private const uint MSGFLT_ALLOW = 1;
    private readonly User32.WndProc _proc; // GC가 거두지 않게 붙잡아 둔다
    private readonly RunLog _log;
    private readonly uint _signalMsg;
    private readonly Dictionary<uint, string> _benchMsgs = [];

    public SignalWindow(RunLog log)
    {
        _log = log;
        _proc = WndProc;
        _signalMsg = User32.RegisterWindowMessage(SignalCodec.MessageName);
        foreach (var name in new[] { BenchMessages.OpenPanel, BenchMessages.ClosePanel, BenchMessages.Probe, BenchMessages.Quit })
            _benchMsgs[User32.RegisterWindowMessage(name)] = name;

        var wc = new User32.WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<User32.WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_proc),
            hInstance = User32.GetModuleHandle(null),
            lpszClassName = SignalCodec.WindowClass,
        };
        User32.RegisterClassEx(ref wc); // 이미 등록되어 있으면 실패하지만 창 만들기는 된다
        // 보이지 않는 최상위 창(WS_POPUP, 크기 0). 메시지 전용 창(HWND_MESSAGE)은 FindWindow로 찾을 수 없어 쓰지 않는다.
        Handle = User32.CreateWindowEx((uint)User32.WS_EX_TOOLWINDOW, SignalCodec.WindowClass, "ClockOut Hunsu", 0x80000000, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, wc.hInstance, IntPtr.Zero);
        // 엑셀이 다른 권한 수준으로 돌 때도 받도록 이 메시지들만 허용한다.
        User32.ChangeWindowMessageFilterEx(Handle, _signalMsg, MSGFLT_ALLOW, IntPtr.Zero);
        foreach (var m in _benchMsgs.Keys) User32.ChangeWindowMessageFilterEx(Handle, m, MSGFLT_ALLOW, IntPtr.Zero);
        _log.Write("signal_window", ("hwnd", Handle.ToInt64()), ("ok", Handle != IntPtr.Zero));
    }

    public IntPtr Handle { get; }

    /// <summary>다리 신호: 엑셀 창 핸들, 신호 번호, 순번, 받은 시각(Stopwatch 눈금).</summary>
    public event Action<IntPtr, int, int, long>? Signal;

    /// <summary>측정 도구 메시지: 이름, wParam, lParam, 받은 시각.</summary>
    public event Action<string, IntPtr, IntPtr, long>? Bench;

    private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == _signalMsg)
        {
            long at = Stopwatch.GetTimestamp();
            var (code, seq) = SignalCodec.Decode(lParam.ToInt64());
            Signal?.Invoke(wParam, code, seq, at);
            return IntPtr.Zero;
        }
        if (_benchMsgs.TryGetValue(msg, out var name))
        {
            Bench?.Invoke(name, wParam, lParam, Stopwatch.GetTimestamp());
            return IntPtr.Zero;
        }
        return User32.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (Handle != IntPtr.Zero) User32.DestroyWindow(Handle);
    }
}
