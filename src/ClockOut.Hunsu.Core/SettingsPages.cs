// 기능: 칼퇴 버튼이 훈수꾼 설정 창의 어느 쪽을 열지 정하는 이름표(2026-11-22, 칼퇴 docs/11 4장 layout 계약 2).
//   `--settings <쪽>`으로 켜거나, 이미 켜져 있으면 등록 메시지 ClockOut.App.Settings의 wParam에 번호를 실어 넘긴다.
//   번호 0은 "general"(옛 칼퇴가 wParam 0으로 보내던 것과 같다). wizard는 설정 마법사를 연다.
// 사용법: SettingsPages.IsKnown("look"); int code = SettingsPages.Code("look"); string page = SettingsPages.Name(code)
// 의존: 없음(순수 계산). 번호는 이미 나간 칼퇴 선언이 쓰므로 순서를 바꾸지 않고 뒤에만 더한다.
namespace ClockOut.Hunsu.Core;

public static class SettingsPages
{
    public const string General = "general";

    private static readonly string[] Pages = [General, "modules", "look", "options", "profiles", "wizard"];

    public static IReadOnlyList<string> All => Pages;

    public static bool IsKnown(string? page) => page != null && Array.IndexOf(Pages, page) >= 0;

    /// <summary>쪽 이름 → 메시지 번호. 모르는 이름은 0(general).</summary>
    public static int Code(string? page) => Math.Max(0, page == null ? 0 : Array.IndexOf(Pages, page));

    /// <summary>메시지 번호 → 쪽 이름. 범위 밖은 general.</summary>
    public static string Name(long code) => code >= 0 && code < Pages.Length ? Pages[code] : General;
}
