// 기능: ChartMath(숫자 읽기, 비율, 추세선 정규화)의 가장자리 경우 시험.
// 사용법: dotnet test
// 의존: ClockOut.Hunsu.Core(View)
using ClockOut.Hunsu.Core.View;

namespace ClockOut.Hunsu.Tests;

public class ChartMathTests
{
    [Theory]
    [InlineData("1,234", 1234.0)]
    [InlineData(" 3.5 ", 3.5)]
    [InlineData("-7", -7.0)]
    public void TryParse_Ok(string s, double expect)
    {
        Assert.True(ChartMath.TryParse(s, out var d));
        Assert.Equal(expect, d);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-")]
    [InlineData("abc")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData(null)]
    public void TryParse_Bad(string? s) => Assert.False(ChartMath.TryParse(s, out _));

    [Theory]
    [InlineData(3, 8, 0.375)]
    [InlineData(10, 8, 1.0)]
    [InlineData(-2, 8, 0.0)]
    [InlineData(5, 0, 0.0)]
    [InlineData(5, -1, 0.0)]
    [InlineData(double.NaN, 8, 0.0)]
    [InlineData(5, double.NaN, 0.0)]
    public void Ratio_Edges(double v, double t, double expect) => Assert.Equal(expect, ChartMath.Ratio(v, t), 9);

    [Theory]
    [InlineData(0.4, 0.4)]
    [InlineData(2, 1.0)]
    [InlineData(-1, 0.0)]
    [InlineData(double.NaN, 0.0)]
    public void Clamp01_Edges(double v, double expect) => Assert.Equal(expect, ChartMath.Clamp01(v));

    [Fact]
    public void MaxOf_DeclaredOrShown()
    {
        Assert.Equal(8, ChartMath.MaxOf([1, 2], 8));
        Assert.Equal(5, ChartMath.MaxOf([1, 5, double.NaN]));
        Assert.Equal(0, ChartMath.MaxOf([]));
        Assert.Equal(0, ChartMath.MaxOf([-3, -1]));
        Assert.Equal(2, ChartMath.MaxOf([1, 2], 0)); // 0 이하 선언은 무시
    }

    [Fact]
    public void Normalize_Basic() => Assert.Equal([0.0, 0.5, 1.0], ChartMath.Normalize([1, 2, 3]));

    [Fact]
    public void Normalize_Negative() => Assert.Equal([0.0, 0.5, 1.0], ChartMath.Normalize([-10, -5, 0]));

    [Fact]
    public void Normalize_Empty() => Assert.Empty(ChartMath.Normalize([]));

    [Fact]
    public void Normalize_AllEqual() => Assert.Equal([0.5, 0.5, 0.5], ChartMath.Normalize([4, 4, 4]));

    [Fact]
    public void Normalize_SkipsNaN() => Assert.Equal([0.0, 1.0], ChartMath.Normalize([1, double.NaN, 3]));

    [Fact]
    public void Normalize_AllNaN() => Assert.Empty(ChartMath.Normalize([double.NaN, double.PositiveInfinity]));
}
