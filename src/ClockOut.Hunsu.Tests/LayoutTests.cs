// 기능: 도크 배치(넘칠 때 접기), 배치 저장 읽기·쓰기와 합치기, 핀 창 자리, 투명도 단계, 단축키 등록 판단 시험.
// 사용법: dotnet test
// 의존: ClockOut.Hunsu.Core(Layout, HotkeyPolicy, OverlayPlacement)
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Core.Layout;

namespace ClockOut.Hunsu.Tests;

public class LayoutTests
{
    [Fact]
    public void Dock_SortsByOrder_AndStacks()
    {
        var plan = DockLayout.Plan([new("b", 200, false, 100, 40), new("a", 100, false, 150, 40)], 1000, 8);
        Assert.Equal(["a", "b"], plan.Select(p => p.Id));
        Assert.Equal(0, plan[0].Top);
        Assert.Equal(158, plan[1].Top);
        Assert.All(plan, p => Assert.False(p.Collapsed));
    }

    [Fact]
    public void Dock_Overflow_CollapsesHighestOrderFirst()
    {
        var plan = DockLayout.Plan([new("a", 100, false, 300, 40), new("b", 200, false, 300, 40), new("c", 300, false, 300, 40)], 700, 10);
        // 300+300+300+20 = 920 > 700 → c 접기(300+300+40+20=660)
        Assert.False(plan[0].Collapsed);
        Assert.False(plan[1].Collapsed);
        Assert.True(plan[2].Collapsed);
        Assert.True(plan[2].ForcedByOverflow);
    }

    [Fact]
    public void Dock_UserCollapsed_NotForced()
    {
        var plan = DockLayout.Plan([new("a", 100, true, 300, 40)], 1000, 8);
        Assert.True(plan[0].Collapsed);
        Assert.False(plan[0].ForcedByOverflow);
        Assert.Equal(40, plan[0].Height);
    }

    [Fact]
    public void LayoutState_RoundTrip_AndUserWins()
    {
        var s = new LayoutState();
        s.Update("hunsu-a", p => p with { Place = Places.Pin, Collapsed = true, Opacity = 2, PinRect = new PixelRect(10, 20, 320, 200) });
        var (back, ok) = LayoutState.FromJson(s.ToJson());
        Assert.True(ok);
        var e = back.Effective("hunsu-a", Places.EdgeRight, 100, ["tab", "panel", "window"]);
        Assert.Equal(Places.Pin, e.Place);
        Assert.True(e.Collapsed);
        Assert.Equal(2, e.Opacity);
        Assert.Equal(new PixelRect(10, 20, 320, 200), e.PinRect);
        Assert.DoesNotContain("right", s.ToJson()); // 계산 속성(Right, Bottom)은 저장하지 않는다
    }

    [Fact]
    public void LayoutState_InvalidSavedPlace_FallsBackToDeclared()
    {
        var s = new LayoutState();
        s.Update("hunsu-a", p => p with { Place = Places.Strip });
        Assert.Equal(Places.EdgeRight, s.Effective("hunsu-a", Places.EdgeRight, 100, ["tab", "panel"]).Place);
        Assert.Equal(Places.EdgeRight, s.Effective("hunsu-new", Places.EdgeRight, 100, ["panel"]).Place);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"spec\":99}")]
    public void LayoutState_Bad_StartsEmpty(string text)
    {
        var (s, ok) = LayoutState.FromJson(text);
        Assert.False(ok);
        Assert.Empty(s.Modules);
    }

    [Fact]
    public void Pin_KeepOnScreen()
    {
        var screens = new[] { new PixelRect(0, 0, 1920, 1040) };
        var inside = new PixelRect(100, 100, 320, 200);
        Assert.Equal(inside, PinPlacement.KeepOnScreen(inside, screens));
        var gone = new PixelRect(3000, 500, 320, 200); // 떼어 낸 두 번째 모니터에 있던 자리
        Assert.Equal(new PixelRect(1600, 500, 320, 200), PinPlacement.KeepOnScreen(gone, screens));
        var aboveTop = new PixelRect(100, -50, 320, 200);
        Assert.Equal(0, PinPlacement.KeepOnScreen(aboveTop, screens).Y);
        Assert.Equal(new PixelRect(436, 300, 320, 200), PinPlacement.Default(new PixelRect(780, 300, 240, 180), 320, 200));
    }

    [Fact]
    public void Opacity_Cycles()
    {
        Assert.Equal(1, OpacityLevels.Next(0));
        Assert.Equal(0, OpacityLevels.Next(2));
        Assert.Equal(1.0, OpacityLevels.RestOpacity(0));
        Assert.Equal(1.0, OpacityLevels.RestOpacity(-5));
    }

    [Fact]
    public void Hotkey_OnlyWhenExcelOrSelfInFront()
    {
        Assert.True(HotkeyPolicy.ShouldRegister("XLMAIN", 10, 99));
        Assert.True(HotkeyPolicy.ShouldRegister("HwndWrapper", 99, 99));
        Assert.False(HotkeyPolicy.ShouldRegister("Chrome_WidgetWin_1", 10, 99));
        Assert.False(HotkeyPolicy.ShouldRegister(null, 10, 99));
    }

    [Fact]
    public void Placement_LeftAndDockHeight()
    {
        var t = new PixelRect(100, 50, 1000, 700);
        Assert.Equal(124, OverlayPlacement.TopLeftInside(t, 300, 200).X);
        Assert.Equal(524, OverlayPlacement.DockHeight(t, 200)); // min(560, 700-160-16=524)
        Assert.Equal(400, OverlayPlacement.DockHeight(new PixelRect(0, 0, 400, 300), 400));
    }
}
