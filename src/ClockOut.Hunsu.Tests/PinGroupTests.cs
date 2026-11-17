// 기능: 핀 창 묶음(자석으로 붙은 것끼리 같이 다니기)과 도크 합쳐 보기 설정의 시험.
// 사용법: dotnet test. 의존: Core/Layout/PinGroups, Core/Settings/HunsuSettings.
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Core.Settings;
using Xunit;

namespace ClockOut.Hunsu.Tests;

public class PinGroupTests
{
    [Fact]
    public void Connected_FollowsChainOfTouchingWindows()
    {
        PixelRect[] r = [new(0, 0, 100, 100), new(100, 0, 100, 100), new(100, 100, 100, 80), new(400, 400, 50, 50)];
        Assert.Equal([0, 1, 2], PinGroups.Connected(r, 0));
        Assert.Equal([0, 1, 2], PinGroups.Connected(r, 2));
        Assert.Equal([3], PinGroups.Connected(r, 3));
    }

    [Fact]
    public void Touching_NeedsRealOverlapAlongTheEdge()
    {
        Assert.True(PinGroups.Touching(new(0, 0, 100, 100), new(102, 20, 80, 80)));   // 2px 틈은 붙은 것으로
        Assert.False(PinGroups.Touching(new(0, 0, 100, 100), new(100, 95, 80, 80)));  // 모서리만 스침
        Assert.False(PinGroups.Touching(new(0, 0, 100, 100), new(120, 0, 80, 80)));   // 떨어짐
    }

    [Fact]
    public void Connected_BadStart_IsEmpty() => Assert.Empty(PinGroups.Connected([new(0, 0, 10, 10)], 5));

    [Fact]
    public void PanelJoin_DefaultsOffAndSurvivesRoundTrip()
    {
        Assert.False(new HunsuSettings().PanelJoin);
        var s = HunsuSettings.FromJson("""{"panelJoin":true}""");
        Assert.True(s.PanelJoin);
        Assert.True(HunsuSettings.FromJson(s.ToJson()).PanelJoin);
    }

    [Fact]
    public void Sidebar_SettingsAreNormalized()
    {
        var d = new HunsuSettings();
        Assert.Equal(("off", 300), (d.Sidebar, d.SidebarWidth));
        var s = HunsuSettings.FromJson("""{"sidebar":" RIGHT ","sidebarWidth":420}""");
        Assert.Equal(("right", 420), (s.Sidebar, s.SidebarWidth));
        var bad = HunsuSettings.FromJson("""{"sidebar":"top","sidebarWidth":5000}""");
        Assert.Equal(("off", 300), (bad.Sidebar, bad.SidebarWidth));
    }
}
