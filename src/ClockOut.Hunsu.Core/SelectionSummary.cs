// 기능: selection.summary 조회의 data를 값 객체로 읽고, 오버레이에 쓸 짧은 글로 만든다.
// 사용법: var s = SelectionSummary.From(resp.Data); label.Text = s.ShortText();
// 의존: System.Text.Json. 값은 메모리에서만 쓰고 기록(로그)에 남기지 않는다(docs/04 7장). 로그에는 Addr, Count만.
using System.Globalization;
using System.Text.Json;

namespace ClockOut.Hunsu.Core;

public sealed record SelectionSummary(string Addr, long Count, long NonEmpty, double? Sum, double? Avg, double? Min, double? Max, long Errors, long Numbers, long Texts, long Formulas, bool Truncated)
{
    public static SelectionSummary? From(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object) return null;
        var types = data.TryGetProperty("types", out var t) && t.ValueKind == JsonValueKind.Object ? t : default;
        return new SelectionSummary(
            Str(data, "addr"),
            Long(data, "count"),
            Long(data, "nonEmpty"),
            Num(data, "sum"),
            Num(data, "avg"),
            Num(data, "min"),
            Num(data, "max"),
            Long(data, "errors"),
            types.ValueKind == JsonValueKind.Object ? Long(types, "num") : 0,
            types.ValueKind == JsonValueKind.Object ? Long(types, "text") : 0,
            types.ValueKind == JsonValueKind.Object ? Long(types, "formula") : 0,
            data.TryGetProperty("truncated", out var tr) && tr.ValueKind == JsonValueKind.True);
    }

    /// <summary>오버레이 알약에 넣는 한 줄. 숫자가 없으면 개수만.</summary>
    public string ShortText()
    {
        var ko = CultureInfo.GetCultureInfo("ko-KR");
        if (Numbers > 0 && Sum is double s)
        {
            string avg = Avg is double a ? a.ToString("#,##0.##", ko) : "-";
            return $"합 {s.ToString("#,##0.##", ko)} · 평균 {avg} · 개수 {NonEmpty.ToString("#,##0", ko)}";
        }
        return $"개수 {NonEmpty.ToString("#,##0", ko)}";
    }

    private static string Str(JsonElement o, string name) => o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static long Long(JsonElement o, string name) => o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : 0;

    private static double? Num(JsonElement o, string name) => o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
}
