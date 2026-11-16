// 기능: 다리가 알리는 모든 조회(hello 응답의 data.queries)를 n번씩 불러 왕복 시간을 재고, 응답 모양과 "조회가 신호를 만들지 않음"을 확인한다.
// 사용법: QueryLoadChecks.Run(rep, xl, n); 예시 통합 문서 상태를 바꾸지 않는다(빈 인자 "{}"). 첫 호출은 예열이라 뺀다.
// 의존: HiddenExcel(Query), Report(Time, Check). 셀 값은 기록하지 않는다(이름, 시간, 코드만).
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace ClockOut.Hunsu.Bench;

internal static class QueryLoadChecks
{
    // 무게별 P95 한도(ms, docs/04 2장). 실제 기준은 각 모듈 선언(manifest)의 "weight"이고, 이 표는 측정용 임시 사본이다.
    private static readonly Dictionary<string, double> Budgets = new()
    {
        ["selection.summary"] = 20, ["sheets"] = 20, ["book.info"] = 20, ["names"] = 20, ["cell.info"] = 20, ["sheet.info"] = 20,
        ["errors"] = 60, ["flow.neighbors"] = 60, ["comments"] = 60, ["tables"] = 60, ["links"] = 60, ["book.audit"] = 60, ["mark"] = 60,
        ["flow.graph"] = 200,
    };
    private const double UnknownBudget = 60;

    // 선택이 없을 때처럼 정상인 실패 코드
    private static readonly HashSet<string> AcceptableCodes = ["range_bad"];

    public static void Run(Report rep, HiddenExcel xl, int n)
    {
        Console.WriteLine("조회 부하");
        if (n < 2) n = 20;
        var names = ListQueries(rep, xl);
        if (names.Count == 0) return;
        xl.Query("bridge.stats", "{\"reset\":true}");
        foreach (var name in names)
        {
            var samples = new List<double>(n);
            var bad = new SortedSet<string>();
            for (int i = 0; i <= n; i++)
            {
                long t0 = Stopwatch.GetTimestamp();
                string json = xl.Query(name);
                double ms = Report.Ms(t0, Stopwatch.GetTimestamp());
                if (i > 0) samples.Add(ms); // 첫 호출은 예열
                string? problem = Judge(json);
                if (problem != null) bad.Add(problem);
            }
            rep.Time($"조회 {name}", samples, Budgets.GetValueOrDefault(name, UnknownBudget));
            rep.Check($"조회 {name} 응답 정상", bad.Count == 0, string.Join(", ", bad));
        }
        SignalCheck(rep, xl);
    }

    private static List<string> ListQueries(Report rep, HiddenExcel xl)
    {
        var list = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(xl.Query("hello"));
            foreach (var e in doc.RootElement.GetProperty("data").GetProperty("queries").EnumerateArray())
            {
                string? s = e.GetString();
                if (s != null && s is not ("hello" or "bridge.stats")) list.Add(s);
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            rep.Check("hello에서 조회 목록 읽기", false, ex.GetType().Name);
        }
        return list;
    }

    /// <summary>문제가 없으면 null, 있으면 짧은 설명(코드 또는 이유).</summary>
    private static string? Judge(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True) return null;
            string code = root.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() ?? "" : "";
            return AcceptableCodes.Contains(code) ? null : (code.Length > 0 ? code : "ok_false");
        }
        catch (JsonException) { return "json_bad"; }
    }

    /// <summary>조회만 했는데 다리가 신호를 보냈다면 조회→선택 이벤트→조회 되먹임 고리가 생긴 것이다.</summary>
    private static void SignalCheck(Report rep, HiddenExcel xl)
    {
        try
        {
            using var doc = JsonDocument.Parse(xl.Query("bridge.stats"));
            var data = doc.RootElement.GetProperty("data");
            string Field(string f) => data.TryGetProperty(f, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64().ToString(CultureInfo.InvariantCulture) : "없음";
            bool has = data.TryGetProperty("sent", out var sent) && sent.ValueKind == JsonValueKind.Number;
            rep.Check("조회가 신호를 만들지 않음", has && sent.GetInt64() == 0, $"보냄 {Field("sent")}, 막음 {Field("muted")}, 이벤트 {Field("events")}");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            rep.Check("조회가 신호를 만들지 않음", false, "bridge.stats 읽기 실패: " + ex.GetType().Name);
        }
    }
}
