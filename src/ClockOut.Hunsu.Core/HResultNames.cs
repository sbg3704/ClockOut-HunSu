// 기능: 엑셀 COM 호출이 실패했을 때의 HRESULT를 이름과 "바쁨(다시 시도할 만함)" 여부로 바꾼다.
// 사용법: HResultNames.Name(ex.HResult) → "RPC_E_CALL_REJECTED", HResultNames.IsBusy(hr)
// 의존: 없음(순수 계산). 0단계에서 실제로 본 코드는 docs/02 3장에 적는다.
namespace ClockOut.Hunsu.Core;

public static class HResultNames
{
    public const int RpcCallRejected = unchecked((int)0x80010001);
    public const int RpcServerCallRetryLater = unchecked((int)0x8001010A);
    public const int RpcServerUnavailable = unchecked((int)0x800706BA);
    public const int RpcCallFailed = unchecked((int)0x800706BE);
    public const int RpcDisconnected = unchecked((int)0x80010108);
    public const int ExcelVbaIgnore = unchecked((int)0x800AC472);
    public const int DispException = unchecked((int)0x80020009);

    public static string Name(int hr) => hr switch
    {
        RpcCallRejected => "RPC_E_CALL_REJECTED",
        RpcServerCallRetryLater => "RPC_E_SERVERCALL_RETRYLATER",
        RpcServerUnavailable => "RPC_S_SERVER_UNAVAILABLE",
        RpcCallFailed => "RPC_S_CALL_FAILED",
        RpcDisconnected => "RPC_E_DISCONNECTED",
        ExcelVbaIgnore => "VBA_E_IGNORE",
        DispException => "DISP_E_EXCEPTION",
        0 => "S_OK",
        _ => "0x" + hr.ToString("X8"),
    };

    /// <summary>엑셀이 잠시 바빠서 거절한 것(다시 시도하면 될 수 있음).</summary>
    public static bool IsBusy(int hr) => hr is RpcCallRejected or RpcServerCallRetryLater or ExcelVbaIgnore;

    /// <summary>엑셀 프로세스가 사라졌거나 연결이 끊긴 것(다시 붙어야 함).</summary>
    public static bool IsGone(int hr) => hr is RpcServerUnavailable or RpcCallFailed or RpcDisconnected;

    /// <summary>틀이 보고하는 고정 오류 코드(docs/01 7.4장).</summary>
    public static string ToErrorCode(int hr) => IsBusy(hr) ? "excel_busy" : IsGone(hr) ? "excel_missing" : "query_failed";
}
