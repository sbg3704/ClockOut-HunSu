// 기능: 칼퇴가 받아 둔 모듈 폴더에서 어느 판을 읽을지(Core/Manifest/ModuleSources) 시험. 가장 높은 판 하나, 위험한 이름은 건너뜀, 같은 id는 높은 판이 이김.
// 사용법: dotnet test
// 의존: ClockOut.Hunsu.Core
using ClockOut.Hunsu.Core.Manifest;

namespace ClockOut.Hunsu.Tests;

public class ModuleSourcesTests
{
    [Fact]
    public void PicksHighestVersion_Numerically()
    {
        var picked = ModuleSources.PickDownloaded([("hunsu-extra", ["1.2.0", "1.10.0", "1.9.9"])]);
        Assert.Equal([("hunsu-extra", "1.10.0")], picked);
    }

    [Fact]
    public void SkipsUnsafeNames_AndEmptyModules()
    {
        var picked = ModuleSources.PickDownloaded([
            ("hunsu-a", ["..", "1.0.0/../x", "0.1.0"]),
            ("../evil", ["1.0.0"]),
            ("hunsu-empty", Array.Empty<string>())]);
        Assert.Equal([("hunsu-a", "0.1.0")], picked);
    }

    [Theory]
    [InlineData("0.2.0", "0.1.0", true)]
    [InlineData("0.1.0", "0.1.0", false)]  // 같으면 먼저 읽은 묶음 기본 모듈을 둔다
    [InlineData("0.0.9", "0.1.0", false)]
    [InlineData("이상한판", "0.1.0", false)]
    public void Replaces_OnlyWhenNewer(string next, string old, bool expected) =>
        Assert.Equal(expected, ModuleSources.Replaces(next, old));

    [Fact]
    public void EnvVar_MatchesClockOut() => Assert.Equal("CLOCKOUT_MODULES_DIR", ModuleSources.EnvVar);
}
