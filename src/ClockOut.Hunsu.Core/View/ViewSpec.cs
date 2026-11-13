// 기능: 화면 선언(view.json)을 읽는다(docs/01 4.1장). 모양별(tab, panel, chip)로 부품 목록과 연결 글 틀을 담는다.
// 사용법: var (spec, errors) = ViewSpec.Parse(json); spec.Panel → 부품 목록, spec.Tab → 손잡이 제목·배지
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

public sealed record ViewSpec(ShapeSpec? Tab, ShapeSpec? Chip, IReadOnlyList<ViewItem> Panel)
{
    public static readonly IReadOnlySet<string> KnownItems = new HashSet<string>
    {
        "stat", "list", "text", "badge", "button", "toggle", "input", "bar", "spark", "flow", "tabs", "divider",
    };

    /// <summary>실제로 그리는 부품(나머지는 이름만 받아 두고 "준비 중"으로 그린다). list·button은 2단계에서 더했다(docs/07).</summary>
    public static readonly IReadOnlySet<string> RenderedItems = new HashSet<string> { "stat", "text", "badge", "divider", "list", "button" };

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
            return (new ViewSpec(tab, chip, items), errors);
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
