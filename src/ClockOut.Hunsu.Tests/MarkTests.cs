// 기능: 셀 위 표시층 시험: view.json marks[] 읽기, 칸 자리 읽기, 격자 자르기, 화면 픽셀 → 창 DIP, 표시 모듈 선언과 예시 데이터.
// 사용법: dotnet test
// 의존: ClockOut.Hunsu.Core(Marks, View, PixelRect)
using System.Text.Json;
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Core.Marks;
using ClockOut.Hunsu.Core.View;

namespace ClockOut.Hunsu.Tests;

public class MarkTests
{
    [Fact]
    public void ViewSpec_ReadsMarks_AndRejectsBadOnes()
    {
        var (v, errors) = ViewSpec.Parse("""{"panel":[],"marks":[{"items":"mark.errors","tone":"bad","style":"outline"},{"items":"mark.pre"},{"items":"x.y","style":"zigzag"},{"tone":"good"}]}""");
        Assert.Equal(2, v!.Marks.Count);
        Assert.Equal("outline", v.Marks[1].Style);
        Assert.Equal("info", v.Marks[1].Tone);
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void ViewSpec_NoMarks_IsEmpty() => Assert.Empty(ViewSpec.Parse("""{"panel":[]}""").Spec!.Marks);

    [Fact]
    public void Read_SkipsBadRows()
    {
        using var doc = JsonDocument.Parse("""[{"x":1,"y":2,"w":3,"h":4},{"x":1,"y":2,"w":0,"h":4},{"x":"a","y":2,"w":3,"h":4},5]""");
        Assert.Equal([new PixelRect(1, 2, 3, 4)], MarkGeometry.Read(doc.RootElement));
    }

    [Fact]
    public void Layout_ConvertsToWindowDips_AndClipsToPanes()
    {
        var window = new PixelRect(100, 100, 1000, 800);
        var panes = new[] { new PixelRect(150, 200, 900, 600) };
        var cells = new[] { new PixelRect(140, 250, 60, 20), new PixelRect(10, 10, 20, 20), new PixelRect(300, 300, 50, 25) };
        var boxes = MarkGeometry.Layout(cells, panes, window, 1.25);
        Assert.Equal(2, boxes.Count);
        Assert.Equal(new MarkBox(40, 120, 40, 16), boxes[0]); // 왼쪽 10px는 머리글 쪽이라 잘림
        Assert.Equal(new MarkBox(160, 160, 40, 20), boxes[1]);
    }

    [Fact]
    public void Layout_NoPanes_UsesWindowOnly()
    {
        var boxes = MarkGeometry.Layout([new PixelRect(0, 0, 50, 50)], [], new PixelRect(10, 10, 100, 100), 1);
        Assert.Equal([new MarkBox(0, 0, 40, 40)], boxes);
    }

    [Fact]
    public void MarksModule_PathsMatchSampleData()
    {
        string dir = ManifestViewTests.FindRepoDir("modules/hunsu-marks");
        var (v, _) = ViewSpec.Parse(File.ReadAllText(Path.Combine(dir, "view.json")));
        var scope = new DataScope();
        using var doc = JsonDocument.Parse("""{"sheet":"매출","zoom":100,"panes":[{"x":0,"y":0,"w":500,"h":500}],"errors":[{"addr":"C5","x":10,"y":10,"w":60,"h":20}],"pre":[],"dep":[],"active":[],"truncated":false}""");
        scope.Set("mark", doc.RootElement);
        Assert.Equal(3, v!.Marks.Count);
        foreach (var m in v.Marks) Assert.True(scope.TryGet(m.Items, out var arr) && arr.ValueKind == JsonValueKind.Array);
    }

    [Fact]
    public void Settings_MarkPoll_Range()
    {
        Assert.Equal(400, ClockOut.Hunsu.Core.Settings.HunsuSettings.FromJson("").MarkPollMs);
        Assert.Equal(0, ClockOut.Hunsu.Core.Settings.HunsuSettings.FromJson("""{"markPollMs":0}""").MarkPollMs);
        Assert.Equal(400, ClockOut.Hunsu.Core.Settings.HunsuSettings.FromJson("""{"markPollMs":50}""").MarkPollMs);
    }
}
