// 기능: 선언의 글 틀 "합 {selection.summary.sum:n}"을 실제 글로 채운다. 선언에 쓸 수 있는 표현은 데이터 연결과 조건 한 줄뿐이다(docs/01 4.1장).
// 사용법: Template.Render("개수 {a.count:n0}", scope) → "개수 1,234"
//   형식: 없음(그대로) | n(천 단위, 소수 둘째까지) | n0(천 단위, 정수) | p(백분율). 값이 없으면 "-".
// 의존: DataScope. 계산은 하지 않는다.
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ClockOut.Hunsu.Core.View;

public static class Template
{
    private static readonly CultureInfo Ko = CultureInfo.GetCultureInfo("ko-KR");

    public static string Render(string? template, DataScope scope)
    {
        if (string.IsNullOrEmpty(template)) return "";
        var sb = new StringBuilder();
        int i = 0;
        while (i < template.Length)
        {
            int open = template.IndexOf('{', i);
            if (open < 0) { sb.Append(template, i, template.Length - i); break; }
            int close = template.IndexOf('}', open + 1);
            if (close < 0) { sb.Append(template, i, template.Length - i); break; }
            sb.Append(template, i, open - i);
            sb.Append(Resolve(template[(open + 1)..close], scope));
            i = close + 1;
        }
        return sb.ToString();
    }

    /// <summary>"경로:형식" 하나를 글로.</summary>
    public static string Resolve(string expr, DataScope scope)
    {
        int colon = expr.IndexOf(':');
        string path = (colon < 0 ? expr : expr[..colon]).Trim();
        string fmt = colon < 0 ? "" : expr[(colon + 1)..].Trim();
        return scope.TryGet(path, out var v) ? Format(v, fmt) : "-";
    }

    public static string Format(JsonElement v, string fmt) => v.ValueKind switch
    {
        JsonValueKind.Number => FormatNumber(v.GetDouble(), fmt),
        JsonValueKind.String => v.GetString() ?? "",
        JsonValueKind.True => "예",
        JsonValueKind.False => "아니오",
        JsonValueKind.Null or JsonValueKind.Undefined => "-",
        _ => v.GetRawText(),
    };

    public static string FormatNumber(double d, string fmt) => fmt switch
    {
        "n" => d.ToString("#,##0.##", Ko),
        "n0" => d.ToString("#,##0", Ko),
        "p" => d.ToString("0.#%", Ko),
        _ => d.ToString(CultureInfo.InvariantCulture),
    };
}
