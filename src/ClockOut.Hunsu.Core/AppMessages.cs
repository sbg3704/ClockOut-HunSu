// 기능: 칼퇴 매니저가 하위 프로그램에 보내는 등록 창 메시지 이름. 레일 [끄기]가 Quit을 훈수꾼의 최상위 창에 보내면 훈수꾼은 정리하고 끝난다.
//   레일 [설정]이 Settings를 보내면 훈수꾼 설정 창을 연다(2026-11-22). 꺼져 있으면 칼퇴가 SettingsArg를 붙여 켠다.
//   보통 실행이 잠금에 걸리면 Start를 켜진 쪽에 넘긴다(설정만 띄운 훈수꾼이 그대로 켜지도록, 2026-11-22).
// 사용법: 묶음 선언 hunsu.app.json의 "quitMessage"·"settingsMessage"·"settingsArgs"에 같은 이름을 적는다(tools/pack_app.ps1). 앱은 SignalWindow가 받는다.
// 의존: 없음. 이름을 바꾸면 이미 깔린 칼퇴가 끄지 못하므로 바꾸지 않는다(새 이름이 필요하면 더한다).
namespace ClockOut.Hunsu.Core;

public static class AppMessages
{
    /// <summary>칼퇴 [끄기]: 정리하고 끝내기.</summary>
    public const string Quit = "ClockOut.App.Quit";

    /// <summary>칼퇴 [설정]: 설정 창을 열고 앞으로 가져오기.</summary>
    public const string Settings = "ClockOut.App.Settings";

    /// <summary>켤 때 설정 창을 바로 여는 실행 인자(Core/AppOptions). 이미 켜져 있으면 새로 켠 쪽이 켜진 쪽에 Settings를 보내고 끝난다.</summary>
    public const string SettingsArg = "--settings";

    /// <summary>보통 실행이 단일 실행 잠금에 걸렸을 때 켜진 쪽에 보낸다. 켜진 쪽이 "설정만" 모드면 이어서 훈수꾼을 켠다(2026-11-22). 이미 켜져 있으면 무시.</summary>
    public const string Start = "ClockOut.App.Start";
}
