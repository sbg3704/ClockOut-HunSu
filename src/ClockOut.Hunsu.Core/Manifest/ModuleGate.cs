// 기능: 칼퇴가 켜 둔 하위 모듈만 불러오게 거르는 문(2026-11-22, 칼퇴 docs/10 1장). 꺼진 모듈은 폴더에 있어도 읽지 않는다(메모리·시작 시간 절감).
//   칼퇴 파일(modules.enabled.json, {"spec":0,"enabled":["hunsu-selection",...]})이 있으면 그 목록이 이긴다.
//   파일이 없거나 깨졌거나 spec이 0이 아니면 묶음 선언(hunsu.app.json)의 modules[].default를 따르고, 선언도 없으면(개발 실행) 모두 불러온다.
//   선언에 없는 모듈(사용자가 직접 넣은 것)은 칼퇴가 모르는 모듈이라 늘 불러온다(선언을 읽은 경우만).
// 사용법: var gate = ModuleGate.Decide(enabledJson, appJson); if (gate.Allows("hunsu-errors")) ...; gate.Source  // "clockout" | "default" | "all"
//   파일 읽기는 앱(Shell/ModuleGateFile)이 하고, 여기는 글만 받는다(시험 가능).
// 의존: System.Text.Json. 실패는 던지지 않고 안전한 쪽(default)으로 간다.
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ClockOut.Hunsu.Core.Manifest;

public sealed partial class ModuleGate
{
    /// <summary>칼퇴가 넘기는 켜진 모듈 파일 경로의 환경변수 이름.</summary>
    public const string EnvVar = "CLOCKOUT_MODULES_FILE";

    /// <summary>칼퇴 규격의 모듈 수 한도(docs/10 1장).</summary>
    public const int MaxModules = 64;

    private readonly HashSet<string>? _enabled;               // 칼퇴 목록(없으면 null)
    private readonly Dictionary<string, bool>? _declared;     // 선언 id → default(없으면 null)

    private ModuleGate(HashSet<string>? enabled, Dictionary<string, bool>? declared, string source, string? problem)
    {
        _enabled = enabled;
        _declared = declared;
        Source = source;
        Problem = problem;
    }

    /// <summary>어디서 정했는지: "clockout"(칼퇴 파일), "default"(선언 기본값), "all"(아무것도 없어 모두).</summary>
    public string Source { get; }

    /// <summary>칼퇴 파일이 있었는데 못 쓴 이유(기록용). 없으면 null.</summary>
    public string? Problem { get; }

    /// <summary>모두 불러오는 문(시험·개발용).</summary>
    public static ModuleGate All { get; } = new(null, null, "all", null);

    public bool Allows(string moduleId)
    {
        if (_declared != null && !_declared.ContainsKey(moduleId)) return true; // 칼퇴가 모르는 모듈
        if (_enabled != null) return _enabled.Contains(moduleId);
        if (_declared != null) return _declared[moduleId];
        return true;
    }

    /// <summary>enabledJson: 칼퇴 파일 내용(없으면 null). appJson: 묶음 선언 내용(없으면 null).</summary>
    public static ModuleGate Decide(string? enabledJson, string? appJson)
    {
        var declared = ParseDeclared(appJson);
        string? problem = null;
        if (enabledJson != null)
        {
            var enabled = ParseEnabled(enabledJson, out problem);
            if (enabled != null) return new ModuleGate(enabled, declared, "clockout", null);
        }
        return declared != null ? new ModuleGate(null, declared, "default", problem) : new ModuleGate(null, null, "all", problem);
    }

    /// <summary>칼퇴 규격의 모듈 id(영문·숫자·-·_, 1~64자).</summary>
    public static bool IsValidId(string? id) => id != null && IdPattern().IsMatch(id);

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex IdPattern();

    private static HashSet<string>? ParseEnabled(string text, out string? problem)
    {
        problem = null;
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) { problem = "not_object"; return null; }
            if (root.TryGetProperty("spec", out var spec) && !(spec.ValueKind == JsonValueKind.Number && spec.TryGetInt32(out int s) && s == 0)) { problem = "spec"; return null; }
            if (!root.TryGetProperty("enabled", out var arr) || arr.ValueKind != JsonValueKind.Array) { problem = "no_enabled"; return null; }
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in arr.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.String) { problem = "bad_item"; return null; }
                set.Add(e.GetString()!); // 모르는 id는 그냥 무시된다(Allows가 묻지 않음)
            }
            return set;
        }
        catch (JsonException) { problem = "json"; return null; }
    }

    private static Dictionary<string, bool>? ParseDeclared(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("modules", out var arr) || arr.ValueKind != JsonValueKind.Array) return null;
            var map = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var m in arr.EnumerateArray())
            {
                if (m.ValueKind != JsonValueKind.Object || !m.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String) continue;
                bool def = !m.TryGetProperty("default", out var d) || d.ValueKind != JsonValueKind.False; // 생략하면 true
                map.TryAdd(id.GetString()!, def);
            }
            return map;
        }
        catch (JsonException) { return null; }
    }
}
