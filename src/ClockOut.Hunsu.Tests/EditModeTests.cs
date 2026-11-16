// 기능: 편집 모드가 쓰는 배치 값 시험: 모듈별 잠금 저장·읽기, 선언 기본값은 잠기지 않음, 배치 글을 되살려 되돌릴 수 있음.
// 사용법: dotnet test
// 의존: ClockOut.Hunsu.Core(Layout/LayoutState)
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Core.Layout;

namespace ClockOut.Hunsu.Tests;

public class EditModeTests
{
    private static readonly string[] Shapes = ["tab", "panel"];

    [Fact]
    public void Locked_DefaultsToFalse_AndRoundTrips()
    {
        var s = new LayoutState();
        Assert.False(s.Effective("hunsu-x", Places.EdgeRight, 100, Shapes).Locked);
        s.Update("hunsu-x", p => p with { Locked = true });
        Assert.True(s.Effective("hunsu-x", Places.EdgeRight, 100, Shapes).Locked);
        var (back, ok) = LayoutState.FromJson(s.ToJson());
        Assert.True(ok);
        Assert.True(back.Effective("hunsu-x", Places.EdgeRight, 100, Shapes).Locked);
    }

    [Fact]
    public void Snapshot_RestoresLayoutAfterEdits()
    {
        var s = new LayoutState();
        s.Update("hunsu-x", p => p with { Place = Places.EdgeLeft, Opacity = 1 });
        string snapshot = s.ToJson();
        s.Update("hunsu-x", p => p with { Place = Places.EdgeRight, Opacity = 3, Locked = true });
        var (snap, ok) = LayoutState.FromJson(snapshot);
        Assert.True(ok);
        s.Modules.Clear();
        foreach (var kv in snap.Modules) s.Modules[kv.Key] = kv.Value;
        var eff = s.Effective("hunsu-x", Places.EdgeRight, 100, Shapes);
        Assert.Equal((Places.EdgeLeft, 1, false), (eff.Place, eff.Opacity, eff.Locked));
    }

    [Fact]
    public void DockRatio_DefaultClampAndRoundTrip()
    {
        var s = new LayoutState();
        Assert.Equal(LayoutState.DefaultDockRatio, s.DockRatio(Places.EdgeLeft));
        s.Docks[Places.EdgeLeft] = 0.7;
        s.Docks[Places.EdgeRight] = 5;
        var (back, ok) = LayoutState.FromJson(s.ToJson());
        Assert.True(ok);
        Assert.Equal(0.7, back.DockRatio(Places.EdgeLeft), 3);
        Assert.Equal(0.9, back.DockRatio(Places.EdgeRight), 3);
    }

    [Fact]
    public void Snap_PullsNearEdgesAndIgnoresFarOnes()
    {
        var area = new PixelRect(0, 0, 1920, 1080);
        // 왼쪽 변이 5픽셀, 위쪽 변이 8픽셀 떨어짐 → 붙는다
        var near = PinPlacement.Snap(new PixelRect(5, 8, 300, 200), [area]);
        Assert.Equal((0, 0), (near.X, near.Y));
        // 오른쪽 변이 화면 오른쪽 끝과 3픽셀 → 오른쪽 변에 맞춘다
        var right = PinPlacement.Snap(new PixelRect(1617, 400, 300, 200), [area]);
        Assert.Equal(1620, right.X);
        // 멀면(50픽셀) 그대로
        var far = PinPlacement.Snap(new PixelRect(50, 50, 300, 200), [area]);
        Assert.Equal((50, 50), (far.X, far.Y));
    }

    [Fact]
    public void HoverSharpen_DefaultsOnAndSurvivesRoundTrip()
    {
        Assert.True(new ClockOut.Hunsu.Core.Settings.HunsuSettings().HoverSharpen);
        var off = ClockOut.Hunsu.Core.Settings.HunsuSettings.FromJson("{\"hoverSharpen\": false}");
        Assert.False(off.HoverSharpen);
        Assert.False(ClockOut.Hunsu.Core.Settings.HunsuSettings.FromJson(off.ToJson()).HoverSharpen);
    }
}
