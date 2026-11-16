// 기능: 다리 자동 갱신 판정 시험: 안 깔림, 같음, 높은 판, 같은 판 다른 내용, 낮은 판 건너뜀, 선언 읽기.
// 사용법: dotnet test
// 의존: ClockOut.Hunsu.Core(Bridge/BridgeUpdatePlan)
using ClockOut.Hunsu.Core.Bridge;

namespace ClockOut.Hunsu.Tests;

public class BridgeUpdatePlanTests
{
    [Fact]
    public void Decide_CoversTheCases()
    {
        Assert.Equal(BridgeAction.NotInstalled, BridgeUpdatePlan.Decide(false, null, null, "0.0.014", "AA"));
        Assert.Equal(BridgeAction.UpToDate, BridgeUpdatePlan.Decide(true, "0.0.014", "aa", "0.0.014", "AA"));
        Assert.Equal(BridgeAction.Update, BridgeUpdatePlan.Decide(true, "0.0.011", "AA", "0.0.014", "BB"));
        Assert.Equal(BridgeAction.Update, BridgeUpdatePlan.Decide(true, "0.0.014", "AA", "0.0.014", "BB")); // 번호를 안 올렸어도 내용이 다르면 바꾼다
        Assert.Equal(BridgeAction.SkipDowngrade, BridgeUpdatePlan.Decide(true, "0.0.020", "AA", "0.0.014", "BB"));
        Assert.Equal(BridgeAction.Update, BridgeUpdatePlan.Decide(true, null, null, "0.0.014", "BB")); // 설치된 판을 모르면 바꾼다
    }

    [Theory]
    [InlineData("0.0.014", "0.0.011", 1)]
    [InlineData("0.0.9", "0.0.10", -1)]
    [InlineData("0.1.000", "0.0.999", 1)]
    [InlineData("0.0.014", "0.0.014", 0)]
    [InlineData("abc", "0.0.014", 0)]
    [InlineData(null, "0.0.014", 0)]
    public void Compare_IsNumeric(string? a, string? b, int sign) => Assert.Equal(sign, Math.Sign(BridgeUpdatePlan.Compare(a, b)));

    [Fact]
    public void ReadManifestVersion_ReadsOrNull()
    {
        Assert.Equal("0.0.014", BridgeUpdatePlan.ReadManifestVersion("""{"id":"x","version":"0.0.014"}"""));
        Assert.Null(BridgeUpdatePlan.ReadManifestVersion("{"));
        Assert.Null(BridgeUpdatePlan.ReadManifestVersion("""{"id":"x"}"""));
    }
}
