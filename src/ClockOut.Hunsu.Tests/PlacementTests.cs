// 기능: 오버레이 자리 계산과 사각형 읽기 시험.
// 사용법: dotnet test
// 의존: ClockOut.Hunsu.Core(OverlayPlacement, PixelRect)
using ClockOut.Hunsu.Core;

namespace ClockOut.Hunsu.Tests;

public class PlacementTests
{
    [Fact]
    public void TopRightInside_UsesMargins()
    {
        var r = OverlayPlacement.TopRightInside(new PixelRect(100, 50, 1000, 700), 300, 200, 24, 160);
        Assert.Equal(new PixelRect(776, 210, 300, 200), r);
    }

    [Fact]
    public void TopRightInside_SmallWindow_StaysInside()
    {
        var r = OverlayPlacement.TopRightInside(new PixelRect(0, 0, 200, 250), 300, 200, 24, 160);
        Assert.Equal(0, r.X);
        Assert.Equal(50, r.Y);
    }

    [Fact]
    public void ShouldHide_WhenMinimizedOrHidden()
    {
        var ok = new PixelRect(0, 0, 10, 10);
        Assert.True(OverlayPlacement.ShouldHide(false, false, ok));
        Assert.True(OverlayPlacement.ShouldHide(true, true, ok));
        Assert.True(OverlayPlacement.ShouldHide(true, false, new PixelRect(0, 0, 0, 10)));
        Assert.False(OverlayPlacement.ShouldHide(true, false, ok));
    }

    [Fact]
    public void PixelRect_ParseAndFormat()
    {
        Assert.True(PixelRect.TryParse("1,-2,30,40", out var r));
        Assert.Equal(new PixelRect(1, -2, 30, 40), r);
        Assert.Equal("1,-2,30,40", r.ToString());
        Assert.False(PixelRect.TryParse("1,2,3", out _));
        Assert.True(r.Contains(1, -2));
        Assert.False(r.Contains(31, 0));
    }
}
