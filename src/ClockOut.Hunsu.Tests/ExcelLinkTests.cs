// 기능: 엑셀 직접 연결 설정 글("번호:시작틱")의 만들기·읽기·다듬기 시험.
// 사용법: dotnet test. 의존: Core/Procs/ExcelLink, Core/Settings/HunsuSettings.
using ClockOut.Hunsu.Core.Procs;
using ClockOut.Hunsu.Core.Settings;
using Xunit;

namespace ClockOut.Hunsu.Tests;

public class ExcelLinkTests
{
    [Fact]
    public void RoundTrips()
    {
        string s = ExcelLink.Format(4321, 638000000000000000);
        Assert.True(ExcelLink.TryParse(s, out int pid, out long ticks));
        Assert.Equal(4321, pid);
        Assert.Equal(638000000000000000, ticks);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("12")]
    [InlineData("0:5")]
    [InlineData("12:-3")]
    [InlineData("1:2:3")]
    public void BadTextIsAuto(string? text)
    {
        Assert.False(ExcelLink.TryParse(text, out _, out _));
        Assert.Equal("", ExcelLink.Normalize(text));
    }

    [Fact]
    public void SettingsNormalizeLinkedExcel()
    {
        Assert.Equal("77:99", HunsuSettings.FromJson("{\"linkedExcel\":\" 77:99 \"}").LinkedExcel);
        Assert.Equal("", HunsuSettings.FromJson("{\"linkedExcel\":\"zzz\"}").LinkedExcel);
        Assert.Equal("", HunsuSettings.FromJson("{}").LinkedExcel);
    }
}
