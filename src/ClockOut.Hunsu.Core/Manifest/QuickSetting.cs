// 기능: 모듈이 선언하는 "옵션 선언표". 우클릭 빠른 메뉴와 설정 창 [옵션] 쪽이 같은 선언을 읽어 알아서 그린다(모듈마다 메뉴·창 코드 없음). 순서(order), 묶음(group), 하위 메뉴(menu)로 계층을 만든다.
// 사용법: manifest에 "quick": [ {항목}, ... ]. 항목 하나:
//   {"kind":"toggle","key":"pencilDraw","label":"그리기"}                          켜기·끄기(체크 표시)
//   {"kind":"choice","key":"pencilLook","label":"모양","options":[{"value":"pencil","label":"색연필"}, ...]}   여러 값 중 하나(드롭박스 · 하위 메뉴)
//   {"kind":"menu","label":"고급","items":[ {항목}, ... ]}                          하위 메뉴(안에 항목을 또 넣을 수 있다)
//   공통(선택): "apply": "redraw"|"refresh"(값이 바뀐 뒤 셸이 할 일, 기본 저장만), "order": 숫자(작을수록 위, 없으면 선언 순서), "group": "묶음 이름"(같은 이름끼리 모아 구분선·작은 제목으로 나눔). key는 settings.json의 키다.
//   현재 값은 QuickSettings.Current로 읽고, 바꿀 값은 Typed로 만든다. 줄 배치는 QuickSchema.Arrange.
// 의존: 없음(순수 계산, 화면 없이 시험된다).
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClockOut.Hunsu.Core.Manifest;

public sealed record QuickOption(string Value, string Label);

public sealed record QuickSetting(string Key, string Label, string Kind, IReadOnlyList<QuickOption> Options, int Order = 0, string Group = "", IReadOnlyList<QuickSetting>? Items = null, string Apply = "")
{
    /// <summary>값이 바뀐 뒤 셸이 할 일: ""(저장만) | "redraw"(셀 위 그림을 다시 그림) | "refresh"(켜진 주제를 엑셀에 다시 묻고 다시 그림).</summary>
    public static readonly IReadOnlySet<string> Applies = new HashSet<string> { "", "redraw", "refresh" };

    public const string Toggle = "toggle", Choice = "choice", Menu = "menu";
    public IReadOnlyList<QuickSetting> Children => Items ?? [];
}

/// <summary>같은 묶음 이름의 항목들(이름이 빈 글이면 제목 없음).</summary>
public sealed record QuickGroup(string Name, IReadOnlyList<QuickSetting> Items);

public static class QuickSchema
{
    /// <summary>항목을 order, 같으면 선언 순서로 늘어놓고 같은 group끼리 모은다. 묶음 순서는 그 묶음에서 가장 앞선 항목을 따른다.</summary>
    public static IReadOnlyList<QuickGroup> Arrange(IReadOnlyList<QuickSetting> items)
    {
        var sorted = items.Select((q, i) => (q, i)).OrderBy(x => x.q.Order).ThenBy(x => x.i).Select(x => x.q).ToList();
        var groups = new List<QuickGroup>();
        var names = new List<string>();
        foreach (var q in sorted) if (!names.Contains(q.Group)) names.Add(q.Group);
        foreach (var n in names) groups.Add(new QuickGroup(n, sorted.Where(q => q.Group == n).ToList()));
        return groups;
    }

    /// <summary>선언을 읽는다. 잘못된 항목은 errors에 적고 건너뛴다.</summary>
    public static List<QuickSetting> Read(JsonElement array, List<string> errors)
    {
        var list = new List<QuickSetting>();
        foreach (var e in array.EnumerateArray())
        {
            string key = Str(e, "key"), label = Str(e, "label"), kind = Str(e, "kind"), group = Str(e, "group"), apply = Str(e, "apply");
            if (!QuickSetting.Applies.Contains(apply)) { errors.Add($"quick '{label}' 모르는 apply: {apply}"); continue; }
            int order = e.ValueKind == JsonValueKind.Object && e.TryGetProperty("order", out var o) && o.TryGetInt32(out var ov) ? ov : 0;
            var opts = new List<QuickOption>();
            if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("options", out var oa) && oa.ValueKind == JsonValueKind.Array)
                foreach (var x in oa.EnumerateArray()) opts.Add(new QuickOption(Str(x, "value"), Str(x, "label")));
            if (label.Length == 0) errors.Add("quick에 label 필요");
            else if (kind == QuickSetting.Menu)
            {
                var sub = e.TryGetProperty("items", out var ia) && ia.ValueKind == JsonValueKind.Array ? Read(ia, errors) : [];
                if (sub.Count == 0) errors.Add($"quick 메뉴 '{label}'에 items 필요");
                else list.Add(new QuickSetting("", label, kind, [], order, group, sub));
            }
            else if (key.Length == 0) errors.Add($"quick '{label}'에 key 필요");
            else if (kind != QuickSetting.Toggle && kind != QuickSetting.Choice) errors.Add($"quick 모르는 kind: {kind}");
            else if (kind == QuickSetting.Choice && opts.Count < 2) errors.Add($"quick {key}: choice는 options 2개 이상");
            else list.Add(new QuickSetting(key, label, kind, opts, order, group, null, apply));
        }
        return list;
    }

    private static string Str(JsonElement o, string name) =>
        o.ValueKind == JsonValueKind.Object && o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}

public static class QuickSettings
{
    /// <summary>설정 JSON(HunsuSettings.ToJson)에서 키의 현재 값을 글자로. 없으면 빈 글.</summary>
    public static string Current(string settingsJson, string key)
    {
        try { return JsonNode.Parse(settingsJson)?[key]?.ToString().ToLowerInvariant() ?? ""; }
        catch (JsonException) { return ""; }
    }

    /// <summary>선택지 글자를 설정에 쓸 값으로: 숫자면 숫자, true/false면 불리언, 아니면 글.</summary>
    public static object Typed(string value) =>
        long.TryParse(value, out var n) ? n : bool.TryParse(value, out var b) ? b : value;

    /// <summary>지금 값이 이 선택지인가(대소문자 무시).</summary>
    public static bool IsCurrent(string current, QuickOption o) => string.Equals(current, o.Value, StringComparison.OrdinalIgnoreCase);

    public static bool IsOn(string current) => current == "true";
}
