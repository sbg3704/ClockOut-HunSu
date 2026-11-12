// 기능: 사용자가 손으로 고칠 수 있는 설정 한 벌(settings.json). 결정이 열린 곳을 코드를 고치지 않고 이 파일의 숫자·목록 하나로 바꾸게 한다.
// 사용법: var s = HunsuSettings.FromJson(text); s.QueryDragGapMs ...;  s.IsEnabledByDefault("hunsu-errors");  s.ToJson()
//   모르는 키는 무시하고, 없거나 범위를 벗어난 값은 기본값으로 되돌린다(앱이 설정 때문에 못 켜지는 일이 없다).
// 의존: System.Text.Json. 파일 읽기·쓰기는 앱(Shell/SettingsStore)이 맡는다. 셀 값이나 경로는 담지 않는다.
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClockOut.Hunsu.Core.Settings;

public sealed class HunsuSettings
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>마우스 왼쪽 버튼을 누른 채(범위 끌기) 조회 사이에 두는 최소 간격(ms). 클수록 엑셀이 덜 바쁘고 숫자가 늦게 바뀐다. 범위 70~2000.</summary>
    public int QueryDragGapMs { get; init; } = 150;

    /// <summary>평소 조회 사이 간격 = 직전 조회 시간 × 이 값(ms 상한은 QueryMaxGapMs). 0이면 간격 없음. 범위 0~10.</summary>
    public double QueryGapFactor { get; init; } = 2.0;

    /// <summary>평소 조회 사이 간격의 상한(ms). 범위 0~2000.</summary>
    public int QueryMaxGapMs { get; init; } = 300;

    /// <summary>조회가 이만큼(ms) 넘게 걸릴 때만 패널을 반투명(바쁨 표시)으로 바꾼다. 범위 250~10000.</summary>
    public int StaleAfterMs { get; init; } = 600;

    /// <summary>list 부품이 한 번에 그리는 줄 수 상한(선언의 max가 더 작으면 그쪽). 범위 5~200.</summary>
    public int ListMaxRows { get; init; } = 50;

    /// <summary>처음 쓸 때(사용자가 관리자에서 아직 켜거나 끄지 않은 모듈) 켜 둘 모듈 id. "*"를 넣으면 전부 켠다.</summary>
    public List<string> EnabledByDefault { get; init; } = ["hunsu-selection"];

    /// <summary>시작할 때 모듈 관리자 창을 바로 연다(처음 써 볼 때 편함).</summary>
    public bool ManagerOnStart { get; init; }

    public bool IsEnabledByDefault(string moduleId) => EnabledByDefault.Contains("*") || EnabledByDefault.Contains(moduleId);

    public static HunsuSettings FromJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new HunsuSettings();
        try
        {
            var s = JsonSerializer.Deserialize<HunsuSettings>(text, Json) ?? new HunsuSettings();
            return s.Clamped();
        }
        catch (JsonException)
        {
            return new HunsuSettings();
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    /// <summary>범위를 벗어난 값을 기본값으로 되돌린 새 설정.</summary>
    public HunsuSettings Clamped()
    {
        var d = new HunsuSettings();
        return new HunsuSettings
        {
            QueryDragGapMs = InRange(QueryDragGapMs, 70, 2000) ? QueryDragGapMs : d.QueryDragGapMs,
            QueryGapFactor = QueryGapFactor is >= 0 and <= 10 ? QueryGapFactor : d.QueryGapFactor,
            QueryMaxGapMs = InRange(QueryMaxGapMs, 0, 2000) ? QueryMaxGapMs : d.QueryMaxGapMs,
            StaleAfterMs = InRange(StaleAfterMs, 250, 10000) ? StaleAfterMs : d.StaleAfterMs,
            ListMaxRows = InRange(ListMaxRows, 5, 200) ? ListMaxRows : d.ListMaxRows,
            EnabledByDefault = EnabledByDefault ?? d.EnabledByDefault,
            ManagerOnStart = ManagerOnStart,
        };
    }

    private static bool InRange(int v, int lo, int hi) => v >= lo && v <= hi;
}
