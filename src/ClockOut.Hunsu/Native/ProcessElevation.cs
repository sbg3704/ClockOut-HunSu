// 기능: 프로세스가 "관리자 권한(상승)"으로 떠 있는지 본다. 훈수꾼이 일반 권한인데 엑셀이 관리자 권한이면 윈도가 COM 접근을 막아 패널이 비기 때문에, 그 이유를 알려 주려고 쓴다(2026-11-17: 관리자로 칼퇴를 켜니 풀림).
// 사용법: ProcessElevation.Compare(excelPid) → TargetHigher(엑셀이 더 높음: 안내가 필요)·Same·Unknown. IsSelfElevated()는 이 프로그램 자신.
// 의존: advapi32/kernel32만. 읽기 전용(프로세스 토큰 조회). 접근이 거부되면 우리보다 높은 권한으로 보고 TargetHigher로 돌려준다.
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ClockOut.Hunsu.Native;

internal enum ElevationRelation { Unknown, Same, TargetHigher }

internal static class ProcessElevation
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenQuery = 0x0008;
    private const int TokenElevationClass = 20;

    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool GetTokenInformation(IntPtr token, int cls, out int info, int length, out int returned);

    /// <summary>이 프로그램 자신이 관리자 권한인가.</summary>
    public static bool IsSelfElevated() => TryIsElevated(Process.GetCurrentProcess().Id) == true;

    /// <summary>엑셀 프로세스가 우리보다 높은 권한인가. 일반 → 관리자 엑셀이면 TargetHigher.</summary>
    public static ElevationRelation Compare(int pid)
    {
        bool? self = TryIsElevated(Process.GetCurrentProcess().Id);
        bool? target = TryIsElevated(pid);
        if (self == null) return ElevationRelation.Unknown;
        if (target == null) return self == false ? ElevationRelation.TargetHigher : ElevationRelation.Unknown; // 일반인 우리가 못 여는 프로세스는 더 높은 쪽이다
        return target == true && self == false ? ElevationRelation.TargetHigher : ElevationRelation.Same;
    }

    private static bool? TryIsElevated(int pid)
    {
        IntPtr proc = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (proc == IntPtr.Zero) return null;
        try
        {
            if (!OpenProcessToken(proc, TokenQuery, out IntPtr token)) return null;
            try { return GetTokenInformation(token, TokenElevationClass, out int e, sizeof(int), out _) ? e != 0 : null; }
            finally { CloseHandle(token); }
        }
        finally { CloseHandle(proc); }
    }
}
