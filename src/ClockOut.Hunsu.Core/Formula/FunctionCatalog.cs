// 기능: 한 수(hunsu-hansu, docs/23)의 함수 사전 파일(functions.ko.json)을 읽고 검사한다. 함수마다 뜻, 찾기 낱말, 빈칸(인자) 목록, 예시, 쓸 수 있는 엑셀 판.
// 사용법: var (cat, errors) = FunctionCatalog.Parse(json);  cat.Find("SUMIFS") → FunctionDef,  def.Args → 빈칸 목록
//   파일 모양: {"functions":[{"name":"SUMIFS","group":"더하기·세기","say":"…","words":["조건 합"],"args":[{"key":"sum","label":"더할 범위","kind":"range"}],"example":"=SUMIFS(…)","needs":"2007","popular":true}]}
//   kind: range(끌어서 범위) · value(숫자·칸) · text(따옴표를 붙이는 글자) · criteria(조건, 칸 주소가 아니면 따옴표) · bool(참·거짓) · any(그대로).
//   optional: 비워도 되는 빈칸(끝쪽 빈 선택 칸은 수식에서 빠진다). repeat: 같은 이름끼리 한 묶음으로 [＋ 더하기]로 늘린다(SUMIFS 조건 쌍).
// 의존: System.Text.Json. 화면·엑셀을 모른다. 함수를 더하려면 코드가 아니라 사전 파일에 한 덩어리를 더한다.
using System.Text.Json;

namespace ClockOut.Hunsu.Core.Formula;

public sealed record ArgDef(string Key, string Label, string Kind, bool Optional = false, string Repeat = "", string Hint = "");

public sealed record FunctionDef(string Name, string Group, string Say, IReadOnlyList<string> Words, IReadOnlyList<ArgDef> Args, string Example, string Needs, bool Popular)
{
    /// <summary>365 전용(동적 배열 등) 함수인지.</summary>
    public bool Needs365 => Needs == "365";

    /// <summary>반복 묶음 이름들(나온 순서).</summary>
    public IReadOnlyList<string> RepeatGroups => [.. Args.Where(a => a.Repeat.Length > 0).Select(a => a.Repeat).Distinct()];
}

public sealed class FunctionCatalog
{
    public static readonly IReadOnlySet<string> Kinds = new HashSet<string> { "range", "value", "text", "criteria", "bool", "any" };

    private readonly Dictionary<string, FunctionDef> _byName;

    private FunctionCatalog(IReadOnlyList<FunctionDef> functions)
    {
        Functions = functions;
        _byName = functions.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<FunctionDef> Functions { get; }

    public static FunctionCatalog Empty { get; } = new([]);

    public FunctionDef? Find(string name) => _byName.TryGetValue(name.Trim(), out var f) ? f : null;

    public static (FunctionCatalog Catalog, IReadOnlyList<string> Errors) Parse(string json)
    {
        var errors = new List<string>();
        var list = new List<FunctionDef>();
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (!doc.RootElement.TryGetProperty("functions", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return (Empty, ["함수 사전에 functions 배열이 없음"]);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in arr.EnumerateArray())
            {
                var f = ReadFunction(e, errors);
                if (f == null) continue;
                if (!seen.Add(f.Name)) { errors.Add($"같은 함수가 두 번: {f.Name}"); continue; }
                list.Add(f);
            }
        }
        catch (JsonException ex)
        {
            return (Empty, ["함수 사전을 읽지 못함: " + ex.Message]);
        }
        return (new FunctionCatalog(list), errors);
    }

    private static FunctionDef? ReadFunction(JsonElement e, List<string> errors)
    {
        string name = Str(e, "name").Trim().ToUpperInvariant();
        if (name.Length == 0 || !name.All(c => char.IsAsciiLetterOrDigit(c) || c == '.' || c == '_'))
        {
            errors.Add($"함수 이름이 없거나 이상함: '{name}'");
            return null;
        }
        var args = new List<ArgDef>();
        if (e.TryGetProperty("args", out var a) && a.ValueKind == JsonValueKind.Array)
        {
            foreach (var x in a.EnumerateArray())
            {
                string key = Str(x, "key"), kind = Str(x, "kind", "any");
                if (key.Length == 0) { errors.Add($"{name}: 빈칸에 key 없음"); return null; }
                if (!Kinds.Contains(kind)) { errors.Add($"{name}.{key}: 모르는 kind '{kind}'"); return null; }
                args.Add(new ArgDef(key, Str(x, "label", key), kind, Bool(x, "optional"), Str(x, "repeat"), Str(x, "hint")));
            }
        }
        if (args.Select(x => x.Key).Distinct().Count() != args.Count) { errors.Add($"{name}: 빈칸 key가 겹침"); return null; }
        var words = e.TryGetProperty("words", out var w) && w.ValueKind == JsonValueKind.Array
            ? w.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString() ?? "").Where(s => s.Length > 0).ToList()
            : [];
        return new FunctionDef(name, Str(e, "group", "그 밖"), Str(e, "say"), words, args, Str(e, "example"), Str(e, "needs"), Bool(e, "popular"));
    }

    private static string Str(JsonElement e, string key, string fallback = "") =>
        e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? fallback : fallback;

    private static bool Bool(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.True;
}
