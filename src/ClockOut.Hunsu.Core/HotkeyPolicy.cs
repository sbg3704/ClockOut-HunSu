// 기능: 전역 단축키(Ctrl+Alt+H, Ctrl+Alt+Shift+H)를 지금 등록해 둘지 정한다. 엑셀이나 훈수꾼 창이 앞에 있을 때만 등록한다(docs/01 12-1장).
//       등록해 둔 동안 다른 프로그램은 그 키를 못 받기 때문이다.
// 사용법: bool want = HotkeyPolicy.ShouldRegister(foregroundClass, foregroundPid, ownPid);
// 의존: 없음(순수 계산). 앞 창 바뀜은 앱이 SetWinEventHook(EVENT_SYSTEM_FOREGROUND)으로 알려 준다.
namespace ClockOut.Hunsu.Core;

public static class HotkeyPolicy
{
    public const string ExcelMainClass = "XLMAIN";

    /// <summary>키: 'H'. 수식키: Ctrl+Alt(부르기·닫기), Ctrl+Alt+Shift(전부 숨김).</summary>
    public const int Key = 0x48;
    public const int ModAlt = 0x1, ModControl = 0x2, ModShift = 0x4, ModNoRepeat = 0x4000;
    public const int SummonMods = ModControl | ModAlt | ModNoRepeat;
    public const int HideAllMods = ModControl | ModAlt | ModShift | ModNoRepeat;

    public static bool ShouldRegister(string? foregroundClass, int foregroundPid, int ownPid) =>
        foregroundPid == ownPid || string.Equals(foregroundClass, ExcelMainClass, StringComparison.Ordinal);
}
