// 기능: 색연필 수식 흐름 시험: 같은 씨앗은 같은 획, 화살표가 칸 둘레에서 시작·끝남, 계획의 시각과 개수 한도, 칸 읽기, 결 무늬, 모듈 선언.
// 사용법: dotnet test
// 의존: ClockOut.Hunsu.Core(Marks, View, PixelRect)
using System.Text.Json;
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Core.Marks;
using ClockOut.Hunsu.Core.View;

namespace ClockOut.Hunsu.Tests;

public class PencilTests
{
    private static readonly SketchTones Tones = new("warn", "info", "good");

    private static SketchCell Cell(string addr, double x, double y, double w = 60, double h = 20) => new(addr, new MarkBox(x, y, w, h));

    [Fact]
    public void Rng_SameSeed_SameNumbers_DifferentSeed_Differs()
    {
        var a = new HandRng(HandRng.Seed("A1>D1")); var b = new HandRng(HandRng.Seed("A1>D1")); var c = new HandRng(HandRng.Seed("A1>D2"));
        Assert.Equal(a.NextDouble(), b.NextDouble());
        Assert.NotEqual(new HandRng(HandRng.Seed("A1>D1")).NextDouble(), c.NextDouble());
        Assert.Equal(HandRng.Seed("x"), HandRng.Seed("x")); // 실행마다 같다(FNV)
    }

    [Fact]
    public void Wobbly_StartsNearA_EndsNearB_AndWobbles()
    {
        var pts = PencilStroke.Wobbly(new PointD(0, 0), new PointD(200, 0), new HandRng(7), bow: 10, wobble: 1.6);
        Assert.True(pts.Count >= 30);
        Assert.True(PencilStroke.Dist(pts[0], new PointD(0, 0)) < 3);
        Assert.True(PencilStroke.Dist(pts[^1], new PointD(200, 0)) < 12); // 조금 지나침
        Assert.Contains(pts, p => Math.Abs(p.Y) > 5); // 휘어짐
        Assert.True(PencilStroke.Length(pts) > 200);
    }

    [Fact]
    public void Wobbly_SamePoints_SameSeed_IsIdentical()
    {
        var x = PencilStroke.Wobbly(new PointD(5, 5), new PointD(80, 40), new HandRng(3));
        var y = PencilStroke.Wobbly(new PointD(5, 5), new PointD(80, 40), new HandRng(3));
        Assert.Equal(x, y);
    }

    [Fact]
    public void Loop_SurroundsBox_AndOvershoots()
    {
        var box = new MarkBox(100, 100, 60, 20);
        var pts = PencilStroke.Loop(box, new HandRng(11), 3);
        Assert.All(pts, p => Assert.True(p.X > 85 && p.X < 175 && p.Y > 88 && p.Y < 132));
        Assert.True(pts.Min(p => p.X) < 100 && pts.Max(p => p.X) > 160); // 둘레를 감쌈
        Assert.True(PencilStroke.Dist(pts[0], pts[^1]) > 0.5); // 끝이 처음과 딱 맞지 않음(손으로 두른 느낌)
    }

    [Fact]
    public void Scribble_StaysInsideBox_AndCapsLines()
    {
        var box = new MarkBox(10, 10, 300, 200);
        var pts = PencilStroke.Scribble(box, new HandRng(1), 2, 14);
        Assert.InRange(pts.Count, 2, 2 * 40);
        Assert.All(pts, p => Assert.True(p.X >= 8 && p.X <= 313 && p.Y >= 8 && p.Y <= 213));
    }

    [Fact]
    public void ExitPoint_IsOnBoxEdgePlusGap()
    {
        var box = new MarkBox(0, 0, 60, 20);
        var p = PencilStroke.ExitPoint(box, new PointD(300, 10), 4);
        Assert.Equal(64, p.X, 3);
        Assert.Equal(10, p.Y, 3);
        var q = PencilStroke.ExitPoint(box, new PointD(30, -200), 4);
        Assert.Equal(-4, q.Y, 3);
    }

    [Fact]
    public void Arrowhead_WingsBehindTip()
    {
        var head = PencilStroke.Arrowhead(new PointD(100, 50), 1, 0, new HandRng(2), 11);
        Assert.Equal(3, head.Count);
        Assert.True(head[0].X < 100 && head[2].X < 100); // 진행 방향(+x)의 반대쪽
        Assert.True(head[0].Y != head[2].Y);
    }

    [Fact]
    public void Plan_Empty_WhenNoNeighbors_OrNoActive()
    {
        Assert.True(FlowSketch.Plan("s", [Cell("D1", 100, 100)], [], [], Tones).IsEmpty);
        Assert.True(FlowSketch.Plan("s", [], [Cell("A1", 0, 0)], [], Tones).IsEmpty);
    }

    [Fact]
    public void Plan_DrawsActiveFirst_ThenNeighbors_InTimeOrder_WithinBudget()
    {
        var pre = new[] { Cell("A1", 10, 100), Cell("B1", 100, 100), Cell("C1", 200, 100) };
        var plan = FlowSketch.Plan("흐름", [Cell("D1", 300, 100)], pre, [Cell("E1", 400, 100)], Tones);
        Assert.False(plan.IsEmpty);
        Assert.Equal("warn", plan.Strokes[0].Tone); // 활성 칸 두르기가 맨 처음
        Assert.Equal(0, plan.Strokes[0].StartMs);
        Assert.Contains(plan.Strokes, s => s.Tone == "info" && s.Shade);
        Assert.Contains(plan.Strokes, s => s.Tone == "good");
        Assert.All(plan.Strokes, s => Assert.True(s.StartMs >= 0 && s.DurationMs >= 45 && s.StartMs + s.DurationMs <= FlowSketch.BudgetMs + 1));
        Assert.True(plan.TotalMs <= FlowSketch.BudgetMs + 1);
        // 가까운 칸(C1)이 먼 칸(A1)보다 먼저 그려진다
        double firstInfo = plan.Strokes.Where(s => s.Tone == "info").Min(s => s.StartMs);
        Assert.True(firstInfo > 0);
        Assert.Equal("흐름|D1|A1,B1,C1|E1", plan.Key);
        Assert.Contains(plan.Labels, l => l.Text == "C1 (쓰는 칸)");
        Assert.Contains(plan.Labels, l => l.Text == "E1 (쓰이는 칸)");
    }

    [Fact]
    public void Plan_SameInput_SameStrokes_AndScrollKeepsShape()
    {
        var a = FlowSketch.Plan("s", [Cell("D1", 300, 100)], [Cell("A1", 10, 100)], [], Tones);
        var b = FlowSketch.Plan("s", [Cell("D1", 300, 100)], [Cell("A1", 10, 100)], [], Tones);
        Assert.Equal(a.Strokes[0].Points, b.Strokes[0].Points);
        // 스크롤로 모두 같은 만큼 내려가면 모양은 같고 자리만 옮겨진다
        var c = FlowSketch.Plan("s", [Cell("D1", 300, 150)], [Cell("A1", 10, 150)], [], Tones);
        Assert.Equal(a.Strokes[0].Points[0].X, c.Strokes[0].Points[0].X, 6);
        Assert.Equal(a.Strokes[0].Points[0].Y + 50, c.Strokes[0].Points[0].Y, 6);
        Assert.Equal(a.Key, c.Key);
    }

    [Fact]
    public void Plan_LimitsNeighbors_AndSkipsShadeForBigRanges()
    {
        var pre = Enumerable.Range(0, 40).Select(i => Cell("A" + (i + 1), 10 + i * 5, 300 + i * 10)).ToList();
        var plan = FlowSketch.Plan("s", [Cell("Z1", 600, 100)], pre, [], Tones);
        Assert.Equal(FlowSketch.MaxPerGroup, plan.Labels.Count(l => l.Tone == "info"));
        var big = FlowSketch.Plan("s", [Cell("Z1", 600, 100)], [Cell("A1", 10, 10, 800, 400)], [], Tones);
        Assert.DoesNotContain(big.Strokes, s => s.Shade); // 큰 범위는 칠하지 않고 둘레만
    }

    [Fact]
    public void Plan_NeighborOverlappingActive_DoesNotCrash_AndHasNoArrow()
    {
        var plan = FlowSketch.Plan("s", [Cell("B1", 100, 100)], [Cell("A1", 100, 100)], [], Tones);
        Assert.False(plan.IsEmpty);
    }

    [Fact]
    public void ReadCells_NeedsAddr_AndClipsToPanes()
    {
        using var doc = JsonDocument.Parse("""[{"addr":"A1","x":110,"y":210,"w":60,"h":20},{"x":0,"y":0,"w":10,"h":10},{"addr":"B1","x":5,"y":5,"w":10,"h":10}]""");
        var cells = FlowSketch.ReadCells(doc.RootElement, [new PixelRect(100, 200, 900, 600)], new PixelRect(100, 100, 1000, 800), 1);
        var only = Assert.Single(cells);
        Assert.Equal("A1", only.Addr);
        Assert.Equal(new MarkBox(10, 110, 60, 20), only.Box);
    }

    [Fact]
    public void Grain_IsDeterministic_AndTexturedInRange()
    {
        var a = PencilGrain.Alpha(64, 5); var b = PencilGrain.Alpha(64, 5);
        Assert.Equal(a, b);
        Assert.Equal(64 * 64, a.Length);
        Assert.All(a, v => Assert.True(v >= PencilGrain.MinAlpha));
        Assert.True(a.Max() - a.Min() > 60); // 무늬가 있다(균일하지 않음)
    }

    [Fact]
    public void ViewSpec_Pencil_RequiresRole()
    {
        var (v, errors) = ViewSpec.Parse("""{"panel":[],"marks":[{"items":"mark.active","style":"pencil","role":"active"},{"items":"mark.pre","style":"pencil"},{"items":"mark.dep","style":"pencil","role":"zzz"}]}""");
        var m = Assert.Single(v!.Marks);
        Assert.Equal("active", m.Role);
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void PencilModule_DeclaresThreeRoles_AndPathsMatchSampleData()
    {
        string dir = ManifestViewTests.FindRepoDir("modules/hunsu-pencilflow");
        var (v, errors) = ViewSpec.Parse(File.ReadAllText(Path.Combine(dir, "view.json")));
        Assert.Empty(errors);
        Assert.Equal(["active", "pre", "dep"], v!.Marks.Select(m => m.Role).ToArray());
        var scope = new DataScope();
        using var doc = JsonDocument.Parse("""{"sheet":"흐름","panes":[],"active":[{"addr":"D1","x":1,"y":1,"w":5,"h":5}],"pre":[],"dep":[],"truncated":false}""");
        scope.Set("mark", doc.RootElement);
        foreach (var m in v.Marks) Assert.True(scope.TryGet(m.Items, out var arr) && arr.ValueKind == JsonValueKind.Array);
        Assert.True(scope.TryGet("mark.sheet", out var sheet) && sheet.GetString() == "흐름");
    }
}
