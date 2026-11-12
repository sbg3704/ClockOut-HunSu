// 기능: 앱과 측정 도구가 주고받는 한 줄 기록 "이름 키=값 키=값"을 만들고 읽는다.
// 사용법: LogLine.Format("render", ("seq", 3), ("t", ts)); var l = LogLine.Parse(text); l.Long("t")
// 의존: 없음(순수 계산). 값에 빈칸과 '='을 넣지 않는다. 셀 값, 시트 이름, 파일 경로는 적지 않는다(docs/04 7장).
using System.Globalization;

namespace ClockOut.Hunsu.Core;

public sealed class LogLine
{
    private readonly Dictionary<string, string> _values;

    private LogLine(string name, Dictionary<string, string> values)
    {
        Name = name;
        _values = values;
    }

    public string Name { get; }

    public static string Format(string name, params (string Key, object? Value)[] pairs)
    {
        var sb = new System.Text.StringBuilder(name);
        foreach (var (key, value) in pairs)
        {
            string text = value switch
            {
                null => "-",
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? "-",
            };
            sb.Append(' ').Append(key).Append('=').Append(Clean(text));
        }
        return sb.ToString();
    }

    public static LogLine? Parse(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 1; i < parts.Length; i++)
        {
            int eq = parts[i].IndexOf('=');
            if (eq > 0) dict[parts[i][..eq]] = parts[i][(eq + 1)..];
        }
        return new LogLine(parts[0], dict);
    }

    public string? Get(string key) => _values.TryGetValue(key, out var v) ? v : null;

    public long Long(string key, long fallback = 0) =>
        long.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    public double Double(string key, double fallback = double.NaN) =>
        double.TryParse(Get(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static string Clean(string text) => text.Length == 0 ? "-" : text.Replace(' ', '_').Replace('=', ':').Replace('\n', '_').Replace('\r', '_');
}
