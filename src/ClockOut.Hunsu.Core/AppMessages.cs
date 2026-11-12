// 기능: 칼퇴 매니저가 하위 프로그램에 보내는 등록 창 메시지 이름. 레일 [끄기]가 이 메시지를 훈수꾼의 최상위 창에 보내면 훈수꾼은 정리하고 끝난다.
// 사용법: 묶음 선언 hunsu.app.json의 "quitMessage"에 같은 이름을 적는다(tools/pack_app.ps1). 앱은 SignalWindow가 받아 끝낸다.
// 의존: 없음. 이름을 바꾸면 이미 깔린 칼퇴가 끄지 못하므로 바꾸지 않는다(새 이름이 필요하면 더한다).
namespace ClockOut.Hunsu.Core;

public static class AppMessages
{
    /// <summary>칼퇴 [끄기]: 정리하고 끝내기.</summary>
    public const string Quit = "ClockOut.App.Quit";
}
