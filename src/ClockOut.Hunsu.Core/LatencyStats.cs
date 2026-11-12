// 기능: 측정값(ms) 목록에서 개수, 평균, P95(100번 중 95번), 최대, 최소를 낸다(docs/04 1장: 평균이 아니라 P95로 판정).
// 사용법: var s = LatencyStats.From(samples); s.P95; s.ToRow("선택→화면", 50)
// 의존: 없음(순수 계산). P95는 가장 가까운 순위 방식(정렬 뒤 ceil(0.95n)번째).
using System.Globalization;

namespace ClockOut.Hunsu.Core;

public sealed record LatencyStats(int Count, double Mean, double P95, double Max, double Min)
{
    public static LatencyStats From(IEnumerable<double> samples)
    {
        var list = samples.Where(v => !double.IsNaN(v)).OrderBy(v => v).ToArray();
        if (list.Length == 0) return new LatencyStats(0, double.NaN, double.NaN, double.NaN, double.NaN);
        return new LatencyStats(list.Length, list.Average(), Percentile(list, 0.95), list[^1], list[0]);
    }

    /// <summary>정렬된 배열에서 가장 가까운 순위 백분위.</summary>
    public static double Percentile(double[] sorted, double p)
    {
        if (sorted.Length == 0) return double.NaN;
        int rank = (int)Math.Ceiling(p * sorted.Length);
        return sorted[Math.Clamp(rank, 1, sorted.Length) - 1];
    }

    public bool Meets(double targetMs) => Count > 0 && P95 <= targetMs;

    /// <summary>마크다운 표 한 줄: | 항목 | 목표 | n | 평균 | P95 | 최대 | 판정 |</summary>
    public string ToRow(string label, double targetMs)
    {
        var c = CultureInfo.InvariantCulture;
        string verdict = Count == 0 ? "측정 없음" : Meets(targetMs) ? "통과" : "넘음";
        return string.Create(c, $"| {label} | {targetMs:0.#}ms | {Count} | {Mean:0.00} | {P95:0.00} | {Max:0.00} | {verdict} |");
    }
}
