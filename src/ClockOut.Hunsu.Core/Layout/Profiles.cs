// 기능: 작업 형태별 미리 짜 둔 프로필(어떤 모듈을 어떤 순서로 켤지)과 적용 계산. WoW의 직업·전문화별 프로필처럼 처음부터 바로 쓰게 한다(docs/10, 방법만 참고).
// 사용법: var plan = Profiles.Plan(Profiles.Find("accounting"), 설치된 모듈 id들); plan[id] → (켜짐, 순서 또는 null)
// 의존: 없음(순수 계산). 모듈 id는 `hunsu-<이름>`. 프로필에 있어도 설치되지 않은 모듈은 건너뛴다.
namespace ClockOut.Hunsu.Core.Layout;

/// <summary>프로필 하나: id, 이름, 설명, 켤 모듈 id(위에서부터 순서대로).</summary>
public sealed record Profile(string Id, string Name, string Description, IReadOnlyList<string> Modules);

public static class Profiles
{
    public static readonly IReadOnlyList<Profile> All =
    [
        new("basic", "기본", "선택 요약과 오류 점검, 시트 이동만. 처음 쓸 때 가볍게.",
            ["hunsu-selection", "hunsu-errors", "hunsu-sheets"]),
        new("accounting", "정산·회계 마감", "합계, 색깔별 합계, 두 열 비교, 값 검증으로 숫자를 맞춥니다.",
            ["hunsu-selection", "hunsu-colorsum", "hunsu-coldiff", "hunsu-errors", "hunsu-validation", "hunsu-valuetools"]),
        new("cleanup", "데이터 정리", "숨은 줄, 중복·차이, 값 고치기, 통합 문서 정리.",
            ["hunsu-selection", "hunsu-errors", "hunsu-hidden", "hunsu-coldiff", "hunsu-valuetools", "hunsu-booktools"]),
        new("review", "검수·감사", "오류, 점검 모음, 링크, 조건부 서식, 입력 규칙, 이름을 훑습니다.",
            ["hunsu-errors", "hunsu-audit", "hunsu-links", "hunsu-condrules", "hunsu-validation", "hunsu-names"]),
        new("formula", "수식 따라가기", "수식 흐름과 그림, 오류, 셀 표시로 계산이 어디서 오는지 봅니다.",
            ["hunsu-selection", "hunsu-flow", "hunsu-flowgraph", "hunsu-errors", "hunsu-marks"]),
        new("explore", "큰 파일 둘러보기", "시트, 이름, 표, 이력, 메모로 낯선 파일을 파악합니다.",
            ["hunsu-sheets", "hunsu-names", "hunsu-tables", "hunsu-history", "hunsu-comments", "hunsu-book"]),
    ];

    public static Profile? Find(string? id) => All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>적용 계산: 프로필 모듈은 켜고 프로필 순서대로 100, 110, 120… 순서를 준다. 그 밖의 설치된 모듈은 끈다(순서는 그대로).</summary>
    public static IReadOnlyDictionary<string, (bool Enabled, int? Order)> Plan(Profile profile, IEnumerable<string> installed)
    {
        var have = new HashSet<string>(installed, StringComparer.Ordinal);
        var result = new Dictionary<string, (bool, int?)>(StringComparer.Ordinal);
        int order = 100;
        foreach (var id in profile.Modules)
        {
            if (!have.Contains(id) || result.ContainsKey(id)) continue;
            result[id] = (true, order);
            order += 10;
        }
        foreach (var id in have) if (!result.ContainsKey(id)) result[id] = (false, null);
        return result;
    }
}
