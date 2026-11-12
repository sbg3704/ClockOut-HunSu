// 기능: 측정 도구(bench)가 앱의 숨은 창에 보내는 등록 메시지 이름. 키보드·마우스 대신 이 메시지로 앱을 움직인다.
// 사용법: RegisterWindowMessage(BenchMessages.OpenPanel) → PostMessage(신호창, msg, wParam, lParam=보낸 시각)
//   단축키는 따로 메시지를 만들지 않고 WM_HOTKEY(0x0312, wParam=1 부르기, 2 전부 숨김)를 그대로 보내 같은 길을 시험한다.
// 의존: 없음. 보통 사용 때는 아무도 보내지 않으므로 해가 없다.
namespace ClockOut.Hunsu.Core;

public static class BenchMessages
{
    /// <summary>첫 모듈 패널 펼치기(lParam = 보낸 시각, Stopwatch 눈금).</summary>
    public const string OpenPanel = "ClockOut.Hunsu.Bench.OpenPanel";

    /// <summary>첫 모듈 패널 접기(손잡이로).</summary>
    public const string ClosePanel = "ClockOut.Hunsu.Bench.ClosePanel";

    /// <summary>wParam 엑셀 창에 hello를 불러 거절·재시도를 기록(lParam = 보낸 시각).</summary>
    public const string Probe = "ClockOut.Hunsu.Bench.Probe";

    /// <summary>첫 모듈을 도크 ↔ 핀 창으로 옮기기(lParam = 보낸 시각).</summary>
    public const string TogglePin = "ClockOut.Hunsu.Bench.TogglePin";

    /// <summary>앱 끝내기(배치를 저장하고 끝낸다).</summary>
    public const string Quit = "ClockOut.Hunsu.Bench.Quit";

    public static readonly string[] All = [OpenPanel, ClosePanel, Probe, TogglePin, Quit];
}
