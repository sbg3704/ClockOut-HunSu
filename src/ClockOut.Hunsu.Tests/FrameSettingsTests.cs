// 기능: 틀 다듬기 설정 시험: 테마 고르기(dark/light/auto), 패널 너비 범위.
// 사용법: dotnet test
// 의존: ClockOut.Hunsu.Core(Settings)
using ClockOut.Hunsu.Core.Settings;

namespace ClockOut.Hunsu.Tests;

public class FrameSettingsTests
{
    [Theory]
    [InlineData(null, false, false)]
    [InlineData("dark", true, false)]
    [InlineData("light", false, true)]
    [InlineData(" LIGHT ", false, true)]
    [InlineData("auto", true, true)]
    [InlineData("auto", false, false)]
    [InlineData("pink", true, false)]
    public void Theme_Picks(string? theme, bool systemLight, bool expectLight) =>
        Assert.Equal(expectLight, ThemeChoice.IsLight(theme, systemLight));

    [Fact]
    public void Settings_ThemeAndWidth_DefaultsAndClamp()
    {
        var d = HunsuSettings.FromJson("");
        Assert.Equal("dark", d.Theme);
        Assert.Equal(240, d.PanelWidth);
        var s = HunsuSettings.FromJson("""{"theme":"Light","panelWidth":320}""");
        Assert.Equal("light", s.Theme);
        Assert.Equal(320, s.PanelWidth);
        var bad = HunsuSettings.FromJson("""{"theme":"neon","panelWidth":9999}""");
        Assert.Equal("dark", bad.Theme);
        Assert.Equal(240, bad.PanelWidth);
    }
}
