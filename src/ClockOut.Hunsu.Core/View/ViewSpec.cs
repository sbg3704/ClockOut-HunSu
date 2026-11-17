// 기능: 화면 선언(view.json)을 읽는다(docs/01 4.1장). 모양별(tab, panel, chip)로 부품 목록과 연결 글 틀을 담는다.
// 사용법: var (spec, errors) = ViewSpec.Parse(json); spec.Panel → 부품 목록, spec.Tab → 손잡이 제목·배지
//   marks[]는 셀 위 표시층 선언(MarkSpec).
// 의존: System.Text.Json. 부품 종류 목록은 KnownItems 한 곳. 모르는 부품은 오류로 모아 그 줄만 건너뛴다.
using System.Text.Json;

namespace ClockOut.Hunsu.Core.View;

/// <summary>부품 하나: 종류와 속성(글 틀 그대로). 속성 값은 화면이 Template/Condition으로 채운다.</summary>
public sealed record ViewItem(string Type, IReadOnlyDictionary<string, string> Props)
{
    public string Get(string key, string fallback = "") => Props.TryGetValue(key, out var v) ? v : fallback;
}

/// <summary>손잡이(tab)와 조각(chip)처럼 부품 하나로 된 모양.</summary>
public sealed record ShapeSpec(IReadOnlyDictionary<string, string> Props)
{
    public string Get(string key, string fallback = "") => Props.TryGetValue(key, out var v) ? v : fallback;
}

/// <summary>셀 위 표시층 선언 하나(view.json의 marks[]): items는 칸 자리(x,y,w,h 화면 픽셀) 배열 경로, tone은 색, style은 outline(테두리)·fill(옅은 칠)·underline(밑줄)·pencil(손으로 그린 색연필 수식 흐름).
/// enabledKey(선택)는 이 그림을 켜고 끄는 설정 키(true일 때만 그림, 일하는 점도 같이 끔). pencil은 role이 필요하다: active(고른 칸을 두름) · pre(쓰는 칸, 화살표가 고른 칸으로) · dep(쓰이는 칸, 화살표가 그 칸으로) · pre2/dep2(한 단계 더 앞/뒤, 칸 자리에 to가 있음) · crossPre/crossDep(다른 시트에서 오는/가는 칸, flow.graph의 items) · tabs(시트 탭 이름 순서, sheets의 items). 칸 자리에 addr가 있어야 한다.</summary>
public sealed record MarkSpec(string Items, string Tone, string Style, string Role = "", string EnabledKey = "")
{
    public static readonly IReadOnlySet<string> Styles = new HashSet<string> { "outline", "fill", "underline", "pencil" };
    public static readonly IReadOnlySet<string> Roles = new HashSet<string> { "active", "pre", "dep", "pre2", "dep2", "crossPre", "crossDep", "tabs" };
}

public sealed record ViewSpec(ShapeSpec? Tab, ShapeSpec? Chip, IReadOnlyList<ViewItem> Panel)
{
    /// <summary>셀 위 표시층(docs/08). 없으면 빈 목록.</summary>
    public IReadOnlyList<MarkSpec> Marks { get; init; } = [];

    public static readonly IReadOnlySet<string> KnownItems = new HashSet<string>
    {
        "stat", "list", "text", "badge", "button", "toggle", "input", "bar", "spark", "progress", "flow", "tabs", "divider",
    };

    /// <summary>실제로 그리는 부품(나머지는 이름만 받아 두고 "준비 중"으로 그린다). list·button은 2단계에서 더했다(docs/07).</summary>
    public static readonly IReadOnlySet<string> RenderedItems = new HashSet<string> { "stat", "text", "badge", "divider", "list", "button", "bar", "spark", "progress" };

    public static readonly IReadOnlySet<string> Tones = new HashSet<string> { "info", "good", "warn", "bad", "muted" };

    public static (ViewSpec? Spec, IReadOnlyList<string> Errors) Parse(string json)
    {
        var errors = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object) return (null, ["view_failed: 선언이 객체가 아님"]);
            var tab = r.TryGetProperty("tab", out var t) ? Shape(t) : null;
            var chip = r.TryGetProperty("chip", out var c) ? Shape(c) : null;
            var items = new List<ViewItem>();
            if (r.TryGetProperty("panel", out var p) && p.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in p.EnumerateArray())
                {
                    var props = Props(e);
                    string type = props.TryGetValue("type", out var ty) ? ty : "";
                    if (!KnownItems.Contains(type)) { errors.Add($"모르는 부품: '{type}'"); continue; }
                    if (props.TryGetValue("tone", out var tone) && !tone.Contains('{') && !Tones.Contains(tone)) errors.Add($"모르는 톤: '{tone}'");
                    items.Add(new ViewItem(type, props));
                }
            }
            var marks = new List<MarkSpec>();
            if (r.TryGetProperty("marks", out var mk) && mk.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in mk.EnumerateArray())
                {
                    var props = Props(e);
                    string path = props.TryGetValue("items", out var it) ? it : "";
                    string tone = props.TryGetValue("tone", out var tn) ? tn : "info";
                    string style = props.TryGetValue("style", out var st) ? st : "outline";
                    if (path.Length == 0) { errors.Add("marks에 items 없음"); continue; }
                    if (!Tones.Contains(tone)) errors.Add($"모르는 톤: '{tone}'");
                    if (!MarkSpec.Styles.Contains(style)) { errors.Add($"모르는 표시 모양: '{style}'"); continue; }
                    string role = props.TryGetValue("role", out var rl) ? rl : "";
                    if (style == "pencil" && !MarkSpec.Roles.Contains(role)) { errors.Add($"pencil에는 role(active, pre, dep)이 필요함: '{role}'"); continue; }
                    marks.Add(new MarkSpec(path, tone, style, style == "pencil" ? role : "", props.TryGetValue("enabledKey", out var ek) ? ek : ""));
                }
            }
            return (new ViewSpec(tab, chip, items) { Marks = marks }, errors);
        }
        catch (JsonException ex)
        {
            return (null, ["view_failed: JSON을 읽지 못함 " + ex.Message]);
        }
    }

    private static ShapeSpec? Shape(JsonElement e) => e.ValueKind == JsonValueKind.Object ? new ShapeSpec(Props(e)) : null;

    /// <summary>객체의 글자·숫자·참거짓 속성만 글로 모은다(중첩 객체는 1단계에서 쓰지 않음).</summary>
    private static Dictionary<string, string> Props(JsonElement e)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        if (e.ValueKind != JsonValueKind.Object) return d;
        foreach (var p in e.EnumerateObject())
        {
            if (p.Value.ValueKind == JsonValueKind.String) d[p.Name] = p.Value.GetString() ?? "";
            else if (p.Value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False) d[p.Name] = p.Value.GetRawText();
        }
        return d;
    }
}
