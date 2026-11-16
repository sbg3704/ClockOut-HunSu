// 기능: 판 표시 계산 시험: 글자(a, b, ... z, aa), 꼬리표·커밋 붙이기, git이 없을 때.
// 사용법: dotnet test
// 의존: ClockOut.Hunsu.Core(BuildLabel)
using ClockOut.Hunsu.Core;

namespace ClockOut.Hunsu.Tests;

public class BuildLabelTests
{
    [Theory]
    [InlineData(0, "")]
    [InlineData(1, "a")]
    [InlineData(3, "c")]
    [InlineData(26, "z")]
    [InlineData(27, "aa")]
    [InlineData(52, "az")]
    [InlineData(53, "ba")]
    public void Letters_CountToLetters(int n, string expected) => Assert.Equal(expected, BuildLabel.Letters(n));

    [Fact]
    public void Format_JoinsVersionLetterTagAndCommit()
    {
        Assert.Equal("v0.0.020c pr20 · abc1234", BuildLabel.Format("0.0.020+pr20.3.abc1234"));
        Assert.Equal("v0.0.020c · abc1234", BuildLabel.Format("0.0.020+3.abc1234"));
        Assert.Equal("v0.0.020 · abc1234", BuildLabel.Format("0.0.020+0.abc1234"));
        Assert.Equal("v0.0.020", BuildLabel.Format("0.0.020"));
    }
}
