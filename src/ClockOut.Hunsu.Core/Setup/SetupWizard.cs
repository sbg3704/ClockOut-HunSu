// 기능: 처음 쓰는 사람용 설정 마법사의 순수 계산. 단계 목록(반가워요 → 하는 일 → 자리 → 모양 → 쓰는 법 → 준비 끝), 처음 켤 때 보일지,
//       단계 이동(앞·뒤, 범위 밖은 막음), 마지막 쪽에 보일 고른 것 요약, 쓰는 법 안내 줄을 정한다. 화면(Shell/SetupWizardWindow)은 이 값을 그리기만 한다.
// 사용법: if (SetupWizard.ShouldShowOnStart(settings)) …;  SetupWizard.Steps[i].Title;  SetupWizard.Move(i, +1);  SetupWizard.Summary(settings)
//   다 보거나 닫으면 settings의 setupSeen에 SetupWizard.Version을 적는다. 마법사 내용을 크게 바꿔 다시 보이고 싶으면 Version을 올린다.
// 의존: Core/Settings(HunsuSettings, SkinChoice), Core/Layout(Profiles, 여러 개·내 프로필), Core/Marks(SketchLook). 화면과 엑셀 없이 시험된다(ClockOut.Hunsu.Tests/SetupWizardTests).
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Core.Settings;

namespace ClockOut.Hunsu.Core.Setup;

/// <summary>마법사 한 쪽: 이름표(점 아래), 큰 제목, 한 줄 안내.</summary>
public sealed record SetupStep(string Id, string Tab, string Title, string Lead);

/// <summary>쓰는 법 안내 한 줄: 무엇을, 어떻게. SettingKey가 있으면 화면에 켜고 끄는 스위치를 붙인다.</summary>
public sealed record SetupTip(string Title, string How, string? SettingKey = null);

public static class SetupWizard
{
    /// <summary>마법사 판. settings의 setupSeen이 이보다 작으면 다음 실행 때 한 번 보인다.</summary>
    public const int Version = 1;

    public static readonly IReadOnlyList<SetupStep> Steps =
    [
        new("hello", "시작", "반가워요, 훈수꾼입니다", "엑셀 옆에서 조용히 지켜보다가 필요할 때만 한 수 거듭니다. 1분이면 내 일에 맞게 맞춰 드릴게요. 고르는 대로 바로 바뀌고, 나중에 설정 창에서 언제든 다시 바꿀 수 있어요."),
        new("work", "하는 일", "주로 어떤 일을 하세요?", "여러 개 고를 수 있어요. 고른 일에 맞는 모듈을 합쳐 켜고 순서를 맞춥니다. 나머지는 꺼질 뿐 지워지지 않고, 지금 상태를 내 프로필로 저장해 둘 수도 있어요."),
        new("place", "자리", "어디에 둘까요?", "사이드바는 화면 가장자리에 붙고 엑셀 창이 남은 곳으로 줄어듭니다. 엑셀 위에 띄우고 싶으면 '엑셀 안 도크'를 고르세요."),
        new("look", "모양", "어떤 느낌이 좋아요?", "패널 룩, 아이콘, 수식 흐름 그림을 고르면 바로 바뀝니다. 오른쪽 미리보기에서 수식 흐름이 어떻게 그려지는지 볼 수 있어요."),
        new("tour", "쓰는 법", "이것만 알면 됩니다", "자주 쓰는 길과 단축키입니다. 오른쪽 스위치로 바로 켜고 끌 수 있어요."),
        new("done", "끝", "준비 끝!", "이렇게 맞췄습니다. 더 자세한 것은 설정 창에서 바꿀 수 있어요."),
    ];

    public static readonly IReadOnlyList<SetupTip> Tips =
    [
        new("부르기 · 닫기", "Ctrl+Alt+H. 패널을 한꺼번에 부르고, 부른 동안은 패널을 끌어 옮길 수 있습니다."),
        new("전부 숨기기", "Ctrl+Alt+Shift+H. 화면 공유할 때처럼 잠깐 다 감출 때. 한 번 더 누르면 다시 보입니다."),
        new("훈 원 우클릭", "모듈 관리, 모양, 사이드바, 엑셀 연결, 끄기가 모두 여기 있습니다. 알림 영역 고양이 아이콘도 같은 메뉴예요."),
        new("범위를 끌면 합계", "범위를 끄는 동안 커서 옆에 합계가 작게 뜹니다.", "cursorHud"),
        new("빠른 띠", "주요 모듈 아이콘을 엑셀 위에 한 줄로 띄웁니다. 부르기 중에 끌어 옮깁니다.", "quickBar"),
        new("색연필 수식 흐름", "수식 셀을 고르면 어디서 값이 오는지 색연필로 그어 줍니다. Ctrl+Alt+D로 잠깐 끄고 켭니다.", "pencilDraw"),
    ];

    /// <summary>처음 켤 때 마법사를 보일지: 아직 이 판 마법사를 끝내거나 닫은 적이 없으면.</summary>
    public static bool ShouldShowOnStart(HunsuSettings s) => s.SetupSeen < Version;

    /// <summary>단계 이동. 처음 앞, 마지막 뒤로는 가지 않는다.</summary>
    public static int Move(int index, int delta) => Math.Clamp(index + delta, 0, Steps.Count - 1);

    /// <summary>"wizard" 쪽 이름이면 마법사를 연다(설정 창 열기 신호를 함께 쓴다).</summary>
    public static bool IsWizardPage(string? page) => string.Equals(page?.Trim(), "wizard", StringComparison.OrdinalIgnoreCase);

    /// <summary>자리 이름(사이드바 설정값 → 사람 말).</summary>
    public static string PlaceName(string sidebar) => sidebar switch
    {
        "left" => "왼쪽 사이드바",
        "right" => "오른쪽 사이드바",
        _ => "엑셀 안 도크",
    };

    /// <summary>마지막 쪽 요약: (칸 이름, 고른 값) 줄들.</summary>
    public static IReadOnlyList<(string Label, string Value)> Summary(HunsuSettings s)
    {
        var chosen = Profiles.FindMany(s.Profile, Profiles.Mine(s.CustomProfiles));
        var skin = SkinChoice.All.FirstOrDefault(k => k.Name == s.Skin) ?? SkinChoice.All[0];
        string place = PlaceName(s.Sidebar);
        if (s.Sidebar == "off") place += " · " + DockShapeName(s.DockShape);
        var extras = Tips.Where(t => t.SettingKey != null && IsOn(s, t.SettingKey)).Select(t => t.Title).ToList();
        return
        [
            ("하는 일", chosen.Count == 0 ? "직접 고름 (지금 켜진 모듈 그대로)" : string.Join(" + ", chosen.Select(p => p.Name))),
            ("자리", place),
            ("패널 룩", skin.Label),
            ("아이콘", IconSetName(s.IconProfile)),
            ("수식 흐름 그림", Marks.SketchLook.ByName(s.PencilLook).Label),
            ("켜 둔 도움", extras.Count == 0 ? "없음" : string.Join(", ", extras)),
        ];
    }

    /// <summary>안내 줄 스위치가 지금 켜져 있는지.</summary>
    public static bool IsOn(HunsuSettings s, string key) => key switch
    {
        "cursorHud" => s.CursorHud,
        "quickBar" => s.QuickBar,
        "pencilDraw" => s.PencilDraw,
        _ => false,
    };

    public static string DockShapeName(string shape) => shape switch
    {
        DockShapes.Crescent => "초승달",
        "fan" => "부채",
        "cluster" => "꽃송이",
        "rail" => "자석 줄",
        "pills" => "알약 목록",
        _ => shape,
    };

    public static string IconSetName(string key) => key switch
    {
        "fluent" => "Fluent",
        "lucide" => "Lucide",
        "phosphor" => "Phosphor",
        "tabler" => "Tabler",
        "material" => "Material",
        "iconoir" => "Iconoir",
        _ => key,
    };
}
