// 기능: 막대(bar)·추세선(spark)·진행(progress) 부품의 순수 계산. 숫자 읽기, 비율(0..1), 추세선 점 정규화. 화면 없이 시험된다.
// 사용법: ChartMath.TryParse("1,234", out var d); ChartMath.Ratio(3, 8) → 0.375; ChartMath.Normalize([1,2,3]) → [0,0.5,1]
// 의존: 없음(System만). 나쁜 값(NaN, 무한대, 0으로 나눔)은 예외 없이 0 또는 건너뜀으로 처리한다.
using System.Globalization;

namespace ClockOut.Hunsu.Core.View;

public static class ChartMath
{
    /// <summary>글을 숫자로: 쉼표와 앞뒤 공백을 지우고 InvariantCulture로 읽는다. NaN·무한대는 실패.</summary>
    public static bool TryParse(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        string s = text.Replace(",", "").Trim();
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) || !double.IsFinite(d)) return false;
        value = d;
        return true;
    }

    /// <summary>value/total을 0..1로 자른다. total이 0 이하이거나 값이 NaN이면 0.</summary>
    public static double Ratio(double value, double total)
    {
        if (!double.IsFinite(value) || !double.IsFinite(total) || total <= 0) return 0;
        return Math.Clamp(value / total, 0, 1);
    }

    /// <summary>total이 없을 때: 값 자체를 0..1 비율로 본다.</summary>
    public static double Clamp01(double value) => double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;

    /// <summary>막대 줄들의 기준 최대값: 선언 max가 있으면 그것, 없으면 보이는 값 중 최대(양수만). 없으면 0.</summary>
    public static double MaxOf(IEnumerable<double> values, double? declared = null)
    {
        if (declared is double d && double.IsFinite(d) && d > 0) return d;
        double max = 0;
        foreach (var v in values) if (double.IsFinite(v) && v > max) max = v;
        return max;
    }

    /// <summary>추세선 점 높이를 0..1로(0이 최소, 1이 최대). 전부 같으면 0.5, NaN·무한대는 건너뛴다.</summary>
    public static IReadOnlyList<double> Normalize(IReadOnlyList<double> values)
    {
        var good = new List<double>(values.Count);
        foreach (var v in values) if (double.IsFinite(v)) good.Add(v);
        if (good.Count == 0) return [];
        double min = good.Min(), max = good.Max();
        if (max - min < 1e-12) return good.Select(_ => 0.5).ToArray();
        return good.Select(v => (v - min) / (max - min)).ToArray();
    }
}
