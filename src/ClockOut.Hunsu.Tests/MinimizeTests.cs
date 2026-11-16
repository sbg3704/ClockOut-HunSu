// 기능: 최소화 공용 판단 시험: 일하는 중 기록(4초), 줄어든 모듈만 깜빡임, form mini 선언, 런처 묶음에 Working 전파, 실제 모듈 선언 확인.
// 사용법: dotnet test
// 의존: ClockOut.Hunsu.Core(Layout/Minimize, LauncherLayout, Manifest)
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Core.Manifest;

namespace ClockOut.Hunsu.Tests;

public class MinimizeTests
{
    private static readonly DateTime T0 = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Tracker_RecentOnlyInsideWindow()
    {
        var t = new ActivityTracker();
        Assert.False(t.IsRecent("a", T0));
        t.Touch("a", T0);
        Assert.True(t.IsRecent("a", T0.AddSeconds(3)));
        Assert.False(t.IsRecent("a", T0.AddSeconds(5)));
        Assert.Empty(t.Recent(T0.AddSeconds(5)));
        Assert.Contains("a", t.Recent(T0.AddSeconds(1)));
    }

    [Fact]
    public void IsWorking_OnlyWhenMinimized()
    {
        Assert.False(Minimize.IsWorking(minimized: false, drawsMarks: true, recentData: true));
        Assert.True(Minimize.IsWorking(true, drawsMarks: true, recentData: false));
        Assert.True(Minimize.IsWorking(true, false, recentData: true));
        Assert.False(Minimize.IsWorking(true, false, false));
    }

    [Fact]
    public void Form_Mini_StartsMinimized()
    {
        Assert.True(Minimize.StartsMinimized("mini"));
        Assert.False(Minimize.StartsMinimized("panel"));
        string json = """{"spec":0,"id":"hunsu-x","name":"x","version":"0.1.000","host":"excel","category":"c","author":"a","description":"d","dependencies":[],"shapes":["panel"],"place":"edge-left","order":1,"data":[],"commands":[],"form":"mini"}""";
        Assert.Equal("mini", ModuleManifest.Parse(json).Manifest!.Form);
    }

    [Fact]
    public void Launcher_WorkingPropagatesToGroup()
    {
        var mods = new[] { new LauncherModule("a", "a", 210, "", "", "info", false, Working: true), new LauncherModule("b", "b", 220, "", "", "info", false) };
        var item = Assert.Single(LauncherGroups.Build(mods, grouped: true));
        Assert.True(item.IsGroup);
        Assert.True(item.Working);
        Assert.False(LauncherGroups.Build(mods.Skip(1), grouped: false).Single().Working);
    }

    [Fact]
    public void LegendModules_StartMinimized()
    {
        string root = AppContext.BaseDirectory;
        while (root != null && !Directory.Exists(Path.Combine(root, "modules"))) root = Path.GetDirectoryName(root)!;
        Assert.NotNull(root);
        foreach (var id in new[] { "hunsu-marks", "hunsu-crosshair" })
        {
            var m = ModuleManifest.Parse(File.ReadAllText(Path.Combine(root, "modules", id, id + ".hunsu.json"))).Manifest;
            Assert.NotNull(m);
            Assert.Equal("mini", m!.Form);
        }
    }
}
