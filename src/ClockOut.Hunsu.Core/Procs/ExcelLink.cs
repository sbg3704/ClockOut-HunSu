// 기능: 사용자가 직접 고른 엑셀(연결)을 설정에 적는 글 모양과 읽기. 창 번호(HWND)는 엑셀을 다시 켜면 바뀌므로 프로세스 번호와 시작 시각으로 기억한다("번호:시작틱").
// 사용법: string s = ExcelLink.Format(pid, startTicks); bool ok = ExcelLink.TryParse(s, out pid, out ticks); ExcelLink.Normalize(text)로 설정 값을 다듬는다(틀리면 "" = 자동).
// 의존: 없음(순수 계산, 화면·엑셀 없이 시험됨).
using System.Globalization;

namespace ClockOut.Hunsu.Core.Procs;

public static class ExcelLink
{
    public static string Format(int pid, long startTicks) => pid.ToString(CultureInfo.InvariantCulture) + ":" + startTicks.ToString(CultureInfo.InvariantCulture);

    public static bool TryParse(string? text, out int pid, out long startTicks)
    {
        pid = 0;
        startTicks = 0;
        var parts = (text ?? "").Trim().Split(':');
        return parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out pid) && pid > 0
            && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out startTicks) && startTicks > 0;
    }

    /// <summary>설정 값 정리: 모양이 맞으면 그대로(공백 제거), 아니면 "" (자동 찾기).</summary>
    public static string Normalize(string? text) => TryParse(text, out var p, out var t) ? Format(p, t) : "";
}
