// 기능: 엑셀 옆 아이콘 런처의 순수 계산 시험: 기능 묶음 나누기, 배지 합·톤, 배치 모양 5종의 자리, 설정 범위, 모듈 icon 칸 (docs/09 2장).
// 사용법: dotnet test
// 의존: ClockOut.Hunsu.Core(Layout/LauncherLayout, Settings, Manifest)
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Core.Manifest;
using ClockOut.Hunsu.Core.Settings;

namespace ClockOut.Hunsu.Tests;

public class LauncherTests
{
    private static LauncherModule M(string id, int order, string badge = "", string tone = "info", bool open = false) =>
        new(id, id, order, "", badge, tone, open);

    private static readonly LauncherModule[] Sample =
    [
        M("sel", 100, open: true), M("book", 150), M("err", 200, "3", "warn"), M("audit", 250, "1", "bad"), M("marks", 210),
        M("sheets", 300), M("hist", 320), M("flow", 400), M("keys", 900),
    ];

    [Fact]
    public void Groups_FollowOrderRanges_SingleBecomesModule()
    {
        var items = LauncherGroups.Build(Sample, grouped: true);
        Assert.Equal(["status", "check", "move", "flow", "keys"], items.Select(i => i.Key));
        Assert.True(items[0].IsGroup);
        Assert.Equal(["sel", "book"], items[0].Members.Select(m => m.Id));
        Assert.True(items[0].Open); // 안쪽에 열린 모듈이 있으면 묶음도 열림
        Assert.False(items[3].IsGroup); // 수식 묶음은 하나뿐이라 모듈 칸
        Assert.Equal(["err", "marks", "audit"], items[1].Members.Select(m => m.Id)); // order 순
    }

    [Fact]
    public void Groups_BadgeSumAndTopTone()
    {
        var check = LauncherGroups.Build(Sample, true).First(i => i.Key == "check");
        Assert.Equal("4", check.Badge);
        Assert.Equal("bad", check.Tone);
        Assert.Equal("1,200", LauncherGroups.SumBadges(["1,000", "200", "-", ""]));
        Assert.Equal("•", LauncherGroups.SumBadges(["확인", ""]));
        Assert.Equal("", LauncherGroups.SumBadges(["0", "-"]));
        Assert.Equal("warn", LauncherGroups.TopTone(["good", "warn", "info"]));
    }

    [Fact]
    public void Ungrouped_OneItemPerModule()
    {
        var items = LauncherGroups.Build(Sample, grouped: false);
        Assert.Equal(Sample.Length, items.Count);
        Assert.All(items, i => Assert.False(i.IsGroup));
    }

    [Theory]
    [InlineData("crescent")]
    [InlineData("fan")]
    [InlineData("cluster")]
    [InlineData("rail")]
    [InlineData("pills")]
    public void Shapes_PlaceEveryItemInsideWindow(string shape)
    {
        foreach (int n in new[] { 1, 5, 11 })
        {
            var p = LauncherShapes.Plan(shape, n, 1.0, 400, 700, awake: true, grouped: n <= 5);
            Assert.Equal(n, p.Items.Count);
            Assert.All(p.Items, i => { Assert.InRange(i.X, 0, 400); Assert.InRange(i.Y, 0, 700); });
            // 아이콘끼리 겹치지 않음(중심 간격 ≥ 지름)
            for (int a = 0; a < n; a++)
                for (int b = a + 1; b < n; b++)
                    Assert.True(Math.Sqrt(Math.Pow(p.Items[a].X - p.Items[b].X, 2) + Math.Pow(p.Items[a].Y - p.Items[b].Y, 2)) >= p.ItemSize - 0.5, $"{shape} n={n} {a}-{b}");
        }
    }

    [Fact]
    public void Crescent_StaysNarrow_AndFanArcIsCapped()
    {
        // 초승달은 펼쳐도 가장자리에서 140px 안(docs/09 2장: 가로 약 80px + 훈 원)
        Assert.True(LauncherShapes.ReservedWidth("crescent", 5, 1.0, true) <= 140);
        var fan = LauncherShapes.Plan("fan", 11, 1.0, 600, 800, true, false);
        double top = fan.Items.Min(i => i.Y), bottom = fan.Items.Max(i => i.Y);
        Assert.True(bottom - top > 0);
        Assert.True(LauncherShapes.ReservedWidth("crescent", 5, 1.16, true) > LauncherShapes.ReservedWidth("crescent", 5, 0.86, true));
    }

    [Fact]
    public void Row_GoesAwayFromEdge()
    {
        var row = LauncherShapes.Row(300, 100, 3, 32);
        Assert.True(row[0].X < 300 && row[1].X < row[0].X && row[2].X < row[1].X);
        Assert.Equal(40, row[0].X - row[1].X, 3);
    }

    [Fact]
    public void Settings_LauncherDefaultsAndClamp()
    {
        var d = HunsuSettings.FromJson("");
        Assert.Equal(("crescent", true, "m", "fluent", 0.45), (d.DockShape, d.DockGroups, d.DockSize, d.IconProfile, d.IdleOpacity));
        var s = HunsuSettings.FromJson("""{"dockShape":"Fan","dockGroups":false,"dockSize":"L","iconProfile":"Tabler","idleOpacity":0.8}""");
        Assert.Equal(("fan", false, "l", "tabler", 0.8), (s.DockShape, s.DockGroups, s.DockSize, s.IconProfile, s.IdleOpacity));
        var bad = HunsuSettings.FromJson("""{"dockShape":"star","dockSize":"xl","iconProfile":"emoji","idleOpacity":0}""");
        Assert.Equal(("crescent", "m", "fluent", 0.45), (bad.DockShape, bad.DockSize, bad.IconProfile, bad.IdleOpacity));
    }

    [Fact]
    public void Manifest_ReadsIconName()
    {
        string json = """
        {"spec":0,"id":"hunsu-x","name":"엑스","version":"0.1.000","host":"excel","category":"c","author":"a","description":"d",
         "dependencies":[],"shapes":["tab","panel"],"place":"edge-right","order":100,"icon":"Sum","data":[]}
        """;
        var (m, errors) = ModuleManifest.Parse(json);
        Assert.Empty(errors);
        Assert.Equal("sum", m!.Icon);
    }
}
