// 기능: COM 메시지 필터. 엑셀이 호출을 거절하면(편집 중, 바쁨) 50ms 기다렸다 다시, 최대 5회(docs/02 3장). 거절 종류와 횟수를 기록한다.
// 사용법: 전용 STA 스레드에서 OleMessageFilter.Register(filter); 호출 전에 filter.Reset(), 뒤에 filter.Retries 읽기.
// 의존: Core/RetryPolicy, ole32 CoRegisterMessageFilter. 필터는 그 스레드에만 걸린다(화면 스레드와 무관).
//   주의: RetryRejectedCall의 반환값 0~99는 "바로 다시"라서, 50ms 간격은 이 안에서 직접 기다려 만든다.
using System.Runtime.InteropServices;
using ClockOut.Hunsu.Core;

namespace ClockOut.Hunsu.Excel;

[ComImport]
[Guid("00000016-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IOleMessageFilter
{
    [PreserveSig] int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo);
    [PreserveSig] int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType);
    [PreserveSig] int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType);
}

internal sealed class OleMessageFilter : IOleMessageFilter
{
    private const int ServerCallIsHandled = 0;
    private const int PendingMsgWaitDefProcess = 2;
    private readonly RetryPolicy _policy;

    public OleMessageFilter(RetryPolicy policy) => _policy = policy;

    /// <summary>이번 호출에서 다시 시도한 횟수.</summary>
    public int Retries { get; private set; }

    /// <summary>마지막 거절 종류: 0 없음, 1 SERVERCALL_REJECTED, 2 SERVERCALL_RETRYLATER.</summary>
    public int LastRejectType { get; private set; }

    public void Reset()
    {
        Retries = 0;
        LastRejectType = 0;
    }

    public int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo) => ServerCallIsHandled;

    public int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType)
    {
        LastRejectType = dwRejectType;
        int wait = _policy.NextDelayMs(Retries);
        if (wait < 0) return -1; // 포기 → 호출이 RPC_E_CALL_REJECTED로 끝난다
        Retries++;
        Thread.Sleep(wait);
        return 0;
    }

    public int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType) => PendingMsgWaitDefProcess;

    [DllImport("ole32.dll")]
    private static extern int CoRegisterMessageFilter(IOleMessageFilter? newFilter, out IOleMessageFilter? oldFilter);

    /// <summary>지금 스레드(STA)에 필터를 건다. 실패하면 HRESULT(0이 성공).</summary>
    public static int Register(IOleMessageFilter? filter) => CoRegisterMessageFilter(filter, out _);
}
