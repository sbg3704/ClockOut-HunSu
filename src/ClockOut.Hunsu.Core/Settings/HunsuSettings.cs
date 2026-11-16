// 기능: 사용자가 손으로 고칠 수 있는 설정 한 벌(settings.json). 결정이 열린 곳을 코드를 고치지 않고 이 파일의 숫자·목록 하나로 바꾸게 한다.
// 사용법: var s = HunsuSettings.FromJson(text); s.QueryDragGapMs ...;  s.IsEnabledByDefault("hunsu-errors");  s.ToJson()
//   도크 모양 5칸은 s.Look으로 묶어 읽고, 설정 창 [모양]에서 고른 값은 s.WithLook(look)으로 나머지 칸을 그대로 둔 새 설정을 만든다.
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
    public List<string> EnabledByDefault { get; init; } = ["hunsu-selection", "hunsu-pencilflow"];

    /// <summary>시작할 때 모듈 관리자 창을 바로 연다(처음 써 볼 때 편함).</summary>
    public bool ManagerOnStart { get; init; }

    /// <summary>화면 테마: "dark"(기본), "light", "auto"(Windows 앱 모드를 따름). 켤 때 한 번 정한다(바꾸면 껐다 켬).</summary>
    public string Theme { get; init; } = ThemeChoice.Dark;

    /// <summary>도크 패널 너비(DIP). 범위 200~480. 핀 창은 끌어서 크기를 바꾼다(부르기 중).</summary>
    public int PanelWidth { get; init; } = 220;

    /// <summary>도크 패널 내용의 최대 높이(DIP). 넘치면 패널 안에서 휠로 내린다. 범위 60~600(600이면 사실상 제한 없음). FHD 한쪽에 5~6개 올리려고 작게 둔다.</summary>
    public int PanelMaxHeight { get; init; } = 120;

    /// <summary>셀 위 표시층(mark)을 쓰는 모듈이 켜져 있을 때, 엑셀이 앞에 있으면 이 간격(ms)으로 자리를 다시 묻는다(스크롤에는 엑셀 이벤트가 없음). 0이면 신호 때만. 범위 0 또는 150~5000.</summary>
    public int MarkPollMs { get; init; } = 400;

    /// <summary>엑셀 옆 아이콘 배치 모양: crescent(기본)·fan·cluster·rail·pills (docs/09 2.1장).</summary>
    public string DockShape { get; init; } = Layout.DockShapes.Rail;

    /// <summary>모듈을 기능 묶음(order 범위)으로 묶어 2단계로 보인다. false면 모두 1단계.</summary>
    public bool DockGroups { get; init; } = true;

    /// <summary>아이콘 크기: s(0.86배)·m·l(1.16배).</summary>
    public string DockSize { get; init; } = "m";

    /// <summary>아이콘 세트 프로필: fluent(기본)·lucide·phosphor·tabler·material·iconoir (docs/09 3.1장).</summary>
    public string IconProfile { get; init; } = "fluent";

    /// <summary>마우스가 멀 때 아이콘 진하기. 범위 0.15~1.</summary>
    public double IdleOpacity { get; init; } = 0.3;

    /// <summary>빠른 띠(프로필의 주요 모듈 아이콘 한 줄)를 엑셀 위에 띄운다(true, 기본). 편집 모드에서 끌어 옮긴다.</summary>
    public bool QuickBar { get; init; } = true;

    /// <summary>범위를 끄는 동안 합계를 커서 옆에 작게 보인다(true, 기본).</summary>
    public bool CursorHud { get; init; } = true;

    /// <summary>마지막으로 적용한 작업 프로필 id(Core/Layout/Profiles). 비어 있으면 직접 고른 상태.</summary>
    public string Profile { get; init; } = "";

    /// <summary>패널에 마우스를 올리면 투명도와 상관없이 선명해진다(true, 기본). false면 정한 투명도를 올려도 그대로 둔다.</summary>
    public bool HoverSharpen { get; init; } = true;

    /// <summary>쓰기 명령(실험 기능) 공지를 확인한 공지 판. 현재 공지 판(WritePolicy.NoticeVersion)과 다르면 쓰기 전에 공지를 다시 보인다(docs/11 2장).</summary>
    public string WriteAck { get; init; } = "";

    /// <summary>공지 확인만 바꾼 새 설정(다른 칸은 그대로).</summary>
    public HunsuSettings WithWriteAck(string version)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(ToJson())!;
        node["writeAck"] = version;
        return FromJson(node.ToJsonString());
    }

    /// <summary>배치 잠금: "locked"(기본, 떼어 낸 패널은 Shift를 누른 채 끌어야 옮겨짐) 또는 "free"(그냥 끌어서 옮김). 부르기(Ctrl+Alt+H) 중에는 늘 옮길 수 있다(편집 모드).</summary>
    public string LayoutLock { get; init; } = "locked";

    /// <summary>색연필 수식 흐름의 보이는 형태: "pencil"(색연필, 기본)·"marker"(형광 마커)·"ink"(펜). 모르는 이름은 색연필. 바로 반영(docs/12).</summary>
    public string PencilLook { get; init; } = "pencil";

    public static readonly IReadOnlyList<string> IconProfiles = ["fluent", "lucide", "phosphor", "tabler", "material", "iconoir"];

    /// <summary>도크 모양 5칸(설정 창 [모양]이 바꾸는 값).</summary>
    [JsonIgnore]
    public DockLook Look => new(DockShape, DockGroups, DockSize, IconProfile, IdleOpacity);

    /// <summary>모양 5칸만 바꾼 새 설정(다른 칸은 그대로, 범위 밖 값은 기본값).</summary>
    public HunsuSettings WithLook(DockLook look)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(ToJson())!;
        node["dockShape"] = look.Shape;
        node["dockGroups"] = look.Groups;
        node["dockSize"] = look.Size;
        node["iconProfile"] = look.IconProfile;
        node["idleOpacity"] = look.IdleOpacity;
        return FromJson(node.ToJsonString());
    }

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
            Theme = ThemeChoice.Normalize(Theme),
            PanelWidth = InRange(PanelWidth, 200, 480) ? PanelWidth : d.PanelWidth,
            PanelMaxHeight = InRange(PanelMaxHeight, 60, 600) ? PanelMaxHeight : d.PanelMaxHeight,
            MarkPollMs = MarkPollMs == 0 || InRange(MarkPollMs, 150, 5000) ? MarkPollMs : d.MarkPollMs,
            DockShape = Layout.DockShapes.Normalize(DockShape),
            DockGroups = DockGroups,
            DockSize = (DockSize ?? "").Trim().ToLowerInvariant() is "s" or "m" or "l" ? DockSize!.Trim().ToLowerInvariant() : d.DockSize,
            IconProfile = IconProfiles.Contains((IconProfile ?? "").Trim().ToLowerInvariant()) ? IconProfile!.Trim().ToLowerInvariant() : d.IconProfile,
            IdleOpacity = IdleOpacity is >= 0.15 and <= 1 ? IdleOpacity : d.IdleOpacity,
            WriteAck = WriteAck ?? "",
            HoverSharpen = HoverSharpen,
            Profile = Profile ?? "",
            QuickBar = QuickBar,
            CursorHud = CursorHud,

            PencilLook = Marks.SketchLook.ByName(PencilLook).Name,
            LayoutLock = (LayoutLock ?? "").Trim().ToLowerInvariant() == "free" ? "free" : "locked",
        };
    }

    private static bool InRange(int v, int lo, int hi) => v >= lo && v <= hi;
}

/// <summary>도크 모양 한 벌: 배치 모양, 기능 묶음, 크기(s·m·l), 아이콘 세트, 쉴 때 진하기.</summary>
public sealed record DockLook(string Shape, bool Groups, string Size, string IconProfile, double IdleOpacity)
{
    public static DockLook Default { get; } = new HunsuSettings().Look;
}
