// 기능: 모듈 선언(<모듈id>.hunsu.json)을 읽고 검증한다(docs/01 4장). 틀리면 예외 대신 이유 목록을 돌려준다.
// 사용법: var r = ModuleManifest.Parse(jsonText); if (r.Manifest is { } m) ... else 로그(r.Errors)
// 의존: System.Text.Json, Core/Layout/Places. 공통 필드(id, version, dependencies, params)는 원래 ClockOut.Core/Manifest가 읽어야 하나
//       그 패키지가 아직 없어 최소한만 여기서 읽는다(docs/01 3.4장 6번, HANDOFF 남은 일).
using System.Text.Json;
using System.Text.RegularExpressions;
using ClockOut.Hunsu.Core.Layout;

namespace ClockOut.Hunsu.Core.Manifest;

/// <summary>구독 한 줄: 주제, 다시 받을 신호, 무게.</summary>
public sealed record DataSubscription(string Topic, IReadOnlyList<string> On, string Weight);

public sealed record ModuleManifest(
    int Spec, string Id, string Name, string Version, string Category, string Description,
    IReadOnlyList<string> Shapes, string Place, int Order, IReadOnlyList<DataSubscription> Data, IReadOnlyList<string> Commands)
{
    /// <summary>틀이 아는 가장 높은 선언 규격 번호.</summary>
    public const int SupportedSpec = 0;

    public static readonly IReadOnlySet<string> KnownShapes = new HashSet<string> { "mark", "chip", "tab", "panel", "window" };
    public static readonly IReadOnlySet<string> KnownWeights = new HashSet<string> { "light", "normal", "heavy" };
    private static readonly Regex IdPattern = new("^hunsu-[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant);

    public static (ModuleManifest? Manifest, IReadOnlyList<string> Errors) Parse(string json)
    {
        var errors = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object) return (null, ["선언이 객체가 아님"]);
            int spec = r.TryGetProperty("spec", out var s) && s.TryGetInt32(out var sv) ? sv : -1;
            string id = Str(r, "id"), name = Str(r, "name"), version = Str(r, "version"), host = Str(r, "host");
            if (spec < 0) errors.Add("spec 없음");
            else if (spec > SupportedSpec) errors.Add($"spec_newer: 선언 규격 {spec}이 틀({SupportedSpec})보다 새것");
            if (!IdPattern.IsMatch(id)) errors.Add($"id 형식 틀림(hunsu-<영문 소문자·숫자>): '{id}'");
            if (name.Length == 0) errors.Add("name 없음");
            if (version.Length == 0) errors.Add("version 없음");
            if (host != "excel") errors.Add($"host는 excel이어야 함: '{host}'");
            foreach (var f in new[] { "category", "author", "description" }) if (Str(r, f).Length == 0) errors.Add(f + " 없음");
            if (!r.TryGetProperty("dependencies", out var deps) || deps.ValueKind != JsonValueKind.Array) errors.Add("dependencies 없음");

            var shapes = Strings(r, "shapes");
            if (shapes.Count == 0) errors.Add("shapes 없음");
            foreach (var sh in shapes) if (!KnownShapes.Contains(sh)) errors.Add($"모르는 모양: {sh}");
            if (!shapes.Contains("panel") && !shapes.Contains("window")) errors.Add("panel과 window 중 하나는 있어야 함(docs/01 5장)");
            string place = Str(r, "place");
            if (!Places.CanPlace(place, shapes)) errors.Add($"place '{place}'에 놓을 모양이 shapes에 없음");
            int order = r.TryGetProperty("order", out var o) && o.TryGetInt32(out var ov) ? ov : 500;

            var data = new List<DataSubscription>();
            if (r.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in d.EnumerateArray())
                {
                    string topic = Str(e, "topic"), weight = e.TryGetProperty("weight", out _) ? Str(e, "weight") : "light";
                    if (topic.Length == 0) errors.Add("data에 topic 없음");
                    if (!KnownWeights.Contains(weight)) errors.Add($"모르는 무게: {weight}");
                    data.Add(new DataSubscription(topic, Strings(e, "on"), weight));
                }
            }
            if (errors.Count > 0) return (null, errors);
            return (new ModuleManifest(spec, id, name, version, Str(r, "category"), Str(r, "description"), shapes, place, order, data, Strings(r, "commands")), errors);
        }
        catch (JsonException ex)
        {
            return (null, ["manifest_bad: JSON을 읽지 못함 " + ex.Message]);
        }
    }

    private static string Str(JsonElement o, string name) =>
        o.ValueKind == JsonValueKind.Object && o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static List<string> Strings(JsonElement o, string name)
    {
        var list = new List<string>();
        if (o.ValueKind == JsonValueKind.Object && o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array)
            foreach (var e in v.EnumerateArray()) if (e.ValueKind == JsonValueKind.String) list.Add(e.GetString()!);
        return list;
    }
}
