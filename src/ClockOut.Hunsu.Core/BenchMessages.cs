// 기능: 측정 도구(bench)가 앱의 숨은 창에 보내는 등록 메시지 이름. 키보드·마우스 대신 이 메시지로 앱을 움직인다.
// 사용법: RegisterWindowMessage(BenchMessages.OpenPanel) → PostMessage(신호창, msg, wParam, lParam=보낸 시각)
// 의존: 없음. 보통 사용 때는 아무도 보내지 않으므로 해가 없다.
namespace ClockOut.Hunsu.Core;

public static class BenchMessages
{
    /// <summary>패널 열기(lParam = 보낸 시각, Stopwatch 눈금).</summary>
    public const string OpenPanel = "ClockOut.Hunsu.Bench.OpenPanel";

    /// <summary>패널 닫기.</summary>
    public const string ClosePanel = "ClockOut.Hunsu.Bench.ClosePanel";

    /// <summary>wParam 엑셀 창에 hello를 불러 거절·재시도를 기록(lParam = 보낸 시각).</summary>
    public const string Probe = "ClockOut.Hunsu.Bench.Probe";

    /// <summary>앱 끝내기.</summary>
    public const string Quit = "ClockOut.Hunsu.Bench.Quit";
}
