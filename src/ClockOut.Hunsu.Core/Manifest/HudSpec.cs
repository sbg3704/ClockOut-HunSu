// 기능: 모듈이 선언하는 "따라다니는 글 HUD"(manifest의 hud) 읽기와 글 만들기. 모듈은 어디에 붙을지(커서·고른 칸)와 무슨 글을 보일지만 적고, 창·따라다니기·숨기기는 셸이 공용으로 한다.
// 사용법: manifest에 "hud": {"anchor":"cursor", "when":"selection.summary.count > 1", "enabledKey":"cursorHud", "anchorKey":"hudAnchor",
//           "parts":[{"text":"합계 {selection.summary.sum:n}", "when":"selection.summary.sum != null"}, {"text":"오류 {selection.summary.errors:n0}", "when":"selection.summary.errors > 0"}], "join":" · "}
//   anchor: cursor(범위를 끄는 동안 커서 옆) | cell(고른 칸 옆에 계속). anchorKey가 있으면 그 설정 값(cursor|cell)이 먼저다.
//   enabledKey: 켜고 끄는 설정 키(없으면 늘 켜짐). when: 보일 조건(Core/View/Condition). parts: 글 조각들(조각마다 when 선택), join으로 이음.
//   Render(scope)가 글을 돌려준다(보일 때만). 화면을 모르는 순수 계산이라 시험된다.
// 의존: Core/View(Template, Condition, DataScope).
using System.Text.Json;
using ClockOut.Hunsu.Core.View;

namespace ClockOut.Hunsu.Core.Manifest;

public sealed record HudPart(string Text, string When);

public sealed record HudSpec(string Anchor, string When, IReadOnlyList<HudPart> Parts, string Join, string EnabledKey, string AnchorKey)
{
    public const string Cursor = "cursor", Cell = "cell";
    public static readonly IReadOnlySet<string> Anchors = new HashSet<string> { Cursor, Cell };

    /// <summary>설정에서 읽은 붙는 곳(없거나 모르면 선언의 기본).</summary>
    public string EffectiveAnchor(string? setting) => setting != null && Anchors.Contains(setting) ? setting : Anchor;

    /// <summary>조건이 맞을 때만 글을 돌려준다. 조각이 모두 비면 null.</summary>
    public string? Render(DataScope scope)
    {
        if (When.Length > 0 && !Condition.Evaluate(When, scope)) return null;
        var texts = Parts.Where(p => p.When.Length == 0 || Condition.Evaluate(p.When, scope)).Select(p => Template.Render(p.Text, scope)).Where(t => t.Length > 0).ToList();
        return texts.Count == 0 ? null : string.Join(Join, texts);
    }

    /// <summary>선언을 읽는다. 틀린 곳은 errors에 적고 null.</summary>
    public static HudSpec? Read(JsonElement e, List<string> errors)
    {
        if (e.ValueKind != JsonValueKind.Object) { errors.Add("hud가 객체가 아님"); return null; }
        string anchor = Str(e, "anchor");
        if (anchor.Length == 0) anchor = Cursor;
        if (!Anchors.Contains(anchor)) { errors.Add($"hud 모르는 anchor: {anchor}"); return null; }
        var parts = new List<HudPart>();
        if (e.TryGetProperty("parts", out var pa) && pa.ValueKind == JsonValueKind.Array)
            foreach (var p in pa.EnumerateArray())
            {
                string text = Str(p, "text");
                if (text.Length == 0) { errors.Add("hud parts에 text 필요"); return null; }
                parts.Add(new HudPart(text, Str(p, "when")));
            }
        if (parts.Count == 0) { errors.Add("hud에 parts 필요"); return null; }
        string join = e.TryGetProperty("join", out _) ? Str(e, "join") : " · ";
        return new HudSpec(anchor, Str(e, "when"), parts, join, Str(e, "enabledKey"), Str(e, "anchorKey"));
    }

    private static string Str(JsonElement o, string name) =>
        o.ValueKind == JsonValueKind.Object && o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}

/// <summary>떼어 낸 반투명 창(form hud)의 처음 크기(논리 px). 선언이 없으면 테마의 HUD 기본 크기.</summary>
public sealed record FormSize(int Width, int Height)
{
    public static FormSize? Read(JsonElement e, List<string> errors)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("width", out var w) || !w.TryGetInt32(out var wi) || !e.TryGetProperty("height", out var h) || !h.TryGetInt32(out var hi))
        { errors.Add("size는 {\"width\":숫자,\"height\":숫자}"); return null; }
        if (wi is < 120 or > 800 || hi is < 60 or > 800) { errors.Add("size 범위 밖(너비 120~800, 높이 60~800)"); return null; }
        return new FormSize(wi, hi);
    }
}
