// 기능: 선언의 조건 한 줄("selection.summary.count == 0")을 판단한다. hideWhen 같은 곳에 쓴다.
// 사용법: Condition.Evaluate("a.count > 1", scope) → true/false
//   지원: <경로> <연산자> <값> (==, !=, >, >=, <, <=), 값은 숫자·"글자"·true·false·null. 경로만 쓰면 "값이 있고 0·빈 글자·false가 아님".
// 의존: DataScope, Template. 그 밖의 문법(괄호, &&, 함수)은 받지 않고 false로 본다(모듈이 코드를 돌리지 못하게).
using System.Globalization;
using System.Text.Json;

namespace ClockOut.Hunsu.Core.View;

public static class Condition
{
    private static readonly string[] Ops = ["==", "!=", ">=", "<=", ">", "<"];

    public static bool Evaluate(string? expr, DataScope scope)
    {
        if (string.IsNullOrWhiteSpace(expr)) return false;
        expr = expr.Trim();
        foreach (var op in Ops)
        {
            int at = expr.IndexOf(op, StringComparison.Ordinal);
            if (at <= 0) continue;
            string left = expr[..at].Trim(), right = expr[(at + op.Length)..].Trim();
            bool has = scope.TryGet(left, out var v);
            return Compare(has ? v : default, right, op);
        }
        return scope.TryGet(expr, out var only) && Truthy(only);
    }

    private static bool Compare(JsonElement v, string literal, string op)
    {
        if (literal == "null")
        {
            bool isNull = v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;
            return op == "==" ? isNull : op == "!=" && !isNull;
        }
        if (double.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
        {
            if (v.ValueKind != JsonValueKind.Number) return op == "!=";
            double d = v.GetDouble();
            return op switch { "==" => d == num, "!=" => d != num, ">" => d > num, ">=" => d >= num, "<" => d < num, "<=" => d <= num, _ => false };
        }
        if (literal is "true" or "false")
        {
            bool want = literal == "true";
            bool isBool = v.ValueKind is JsonValueKind.True or JsonValueKind.False;
            bool eq = isBool && (v.ValueKind == JsonValueKind.True) == want;
            return op == "==" ? eq : op == "!=" && !eq;
        }
        if (literal.Length >= 2 && literal[0] == '"' && literal[^1] == '"')
        {
            bool eq = v.ValueKind == JsonValueKind.String && v.GetString() == literal[1..^1];
            return op == "==" ? eq : op == "!=" && !eq;
        }
        return false;
    }

    private static bool Truthy(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.Number => v.GetDouble() != 0,
        JsonValueKind.String => (v.GetString() ?? "").Length > 0,
        JsonValueKind.Array => v.GetArrayLength() > 0,
        JsonValueKind.Object => true,
        _ => false,
    };
}
