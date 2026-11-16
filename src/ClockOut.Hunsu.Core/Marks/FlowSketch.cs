// 기능: 수식 흐름을 색연필 스케치로 바꾸는 계획. 활성 칸을 두르고, 쓰는 칸(앞)에서 활성 칸으로, 활성 칸에서 쓰이는 칸(뒤)으로 휘어진 손 화살표를 긋는다.
//       획마다 시작 시각과 걸리는 시간을 정해 "휙휙휙" 이어 그려지게 한다(겹쳐 시작). 그리기와 움직임은 앱(Overlay/PencilPainter)이 맡고, 여기는 점·시간만 계산한다.
// 사용법: var plan = FlowSketch.Plan(sheet, active, pre, dep, new("warn","info","good"));  plan.Strokes / plan.Labels / plan.Key
//         칸 읽기: FlowSketch.ReadCells(jsonArray, panes, windowRect, scale)  (addr가 있는 줄만, 보이는 부분만)
// 의존: Core/Marks(PencilStroke, MarkGeometry), Core/PixelRect. 화면·엑셀 없이 시험된다.
using System.Text.Json;

namespace ClockOut.Hunsu.Core.Marks;

/// <summary>주소가 붙은 칸 자리(창 안 DIP).</summary>
public sealed record SketchCell(string Addr, MarkBox Box);

/// <summary>획 하나. StartMs부터 DurationMs 동안 처음에서 끝으로 그려진다. Shade는 칠하기(스크리블).</summary>
public sealed record SketchStroke(IReadOnlyList<PointD> Points, string Tone, double Width, double Alpha, double StartMs, double DurationMs, bool Shade = false);

/// <summary>손글씨 이름표. StartMs에 나타난다. Angle은 도.</summary>
public sealed record SketchLabel(string Text, double X, double Y, double Angle, string Tone, double StartMs);

public sealed record SketchTones(string Active, string Pre, string Dep);

public sealed record SketchPlan(string Key, IReadOnlyList<SketchStroke> Strokes, IReadOnlyList<SketchLabel> Labels, double TotalMs)
{
    public static readonly SketchPlan Empty = new("", [], [], 0);
    public bool IsEmpty => Strokes.Count == 0;
}

public static class FlowSketch
{
    /// <summary>한 묶음(앞/뒤)에서 그리는 칸의 최대 수. 가까운 것부터. 넘으면 그만큼 덜 그린다(그리기 부담 한도).</summary>
    public const int MaxPerGroup = 10;

    /// <summary>전체가 이 시간 안에 끝나게 시각을 줄인다(ms).</summary>
    public const double BudgetMs = 1100;

    private const double ShadeMaxArea = 90000; // 이보다 큰 범위는 칠하지 않고 둘레만 두른다

    public static IReadOnlyList<SketchCell> ReadCells(JsonElement array, IReadOnlyList<PixelRect> panes, PixelRect window, double scale)
    {
        var list = new List<SketchCell>();
        if (array.ValueKind != JsonValueKind.Array) return list;
        foreach (var e in array.EnumerateArray())
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("addr", out var a) || a.ValueKind != JsonValueKind.String) continue;
            if (Int(e, "x") is not int x || Int(e, "y") is not int y || Int(e, "w") is not int w || Int(e, "h") is not int h || w <= 0 || h <= 0) continue;
            var boxes = MarkGeometry.Layout([new PixelRect(x, y, w, h)], panes, window, scale);
            if (boxes.Count > 0) list.Add(new SketchCell(a.GetString() ?? "", boxes[0]));
        }
        return list;
    }

    private static int? Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d) && double.IsFinite(d) ? (int)Math.Round(d) : null;

    public static SketchPlan Plan(string sheet, IReadOnlyList<SketchCell> active, IReadOnlyList<SketchCell> pre, IReadOnlyList<SketchCell> dep, SketchTones tones)
    {
        if (active.Count == 0 || (pre.Count == 0 && dep.Count == 0)) return SketchPlan.Empty;
        var a = active[0];
        var strokes = new List<SketchStroke>();
        var labels = new List<SketchLabel>();
        double cursor = 0;

        // 활성 칸 두르기
        var rngA = new HandRng(HandRng.Seed(sheet + "!" + a.Addr + "*"));
        var ring = PencilStroke.Loop(a.Box, rngA, 3.5);
        cursor = AddPencil(strokes, ring, tones.Active, 3.0, 0.95, cursor, 3.2, rngA);
        labels.Add(new SketchLabel("여기", a.Box.X + a.Box.W + 2, a.Box.Y - 15, -4, tones.Active, 60));

        var ac = Center(a.Box);
        foreach (var (group, tone, isPre) in new[] { (pre, tones.Pre, true), (dep, tones.Dep, false) })
        {
            var near = group.Where(c => c.Addr != a.Addr).OrderBy(c => Dist(Center(c.Box), ac)).Take(MaxPerGroup).ToList();
            for (int i = 0; i < near.Count; i++)
            {
                var c = near[i];
                var rng = new HandRng(HandRng.Seed(sheet + "!" + (isPre ? c.Addr + ">" + a.Addr : a.Addr + ">" + c.Addr)));
                double cc = cursor;
                // 이웃 칸: 칠하고(작은 칸만) 둘러싼다
                if (c.Box.W * c.Box.H <= ShadeMaxArea)
                {
                    var scr = PencilStroke.Scribble(c.Box, rng, 5, 14);
                    if (scr.Count > 1) strokes.Add(new SketchStroke(scr, tone, 1.5, 0.5, cc, Clamp(PencilStroke.Length(scr) / 4.5, 80, 220), true));
                    cc += 40;
                }
                var loop = PencilStroke.Loop(c.Box, rng, 2.5);
                cc = AddPencil(strokes, loop, tone, 2.3, 0.9, cc, 3.0, rng);
                // 화살표: 앞은 이웃 → 활성, 뒤는 활성 → 이웃
                var from = isPre ? c : a;
                var to = isPre ? a : c;
                var p0 = PencilStroke.ExitPoint(from.Box, Center(to.Box), 4);
                var p1 = PencilStroke.ExitPoint(to.Box, Center(from.Box), 6);
                double len = PencilStroke.Dist(p0, p1);
                if (len >= 12)
                {
                    double bow = Math.Min(40, len * 0.14) * (rng.Coin() ? 1 : -1);
                    var line = PencilStroke.Wobbly(p0, p1, rng, bow, 1.6);
                    cc = AddPencil(strokes, line, tone, 2.8, 0.95, cc, 2.6, rng);
                    var last = line[^1]; var prev = line[Math.Max(0, line.Count - 3)];
                    var head = PencilStroke.Arrowhead(last, last.X - prev.X, last.Y - prev.Y, rng);
                    strokes.Add(new SketchStroke(head, tone, 2.8, 0.95, cc - 30, 80));
                    cc += 50;
                }
                string cap = i == 0 ? (isPre ? $"{c.Addr} (쓰는 칸)" : $"{c.Addr} (쓰이는 칸)") : c.Addr;
                double ly = c.Box.Y >= 18 ? c.Box.Y - 17 : c.Box.Y + c.Box.H + 1;
                labels.Add(new SketchLabel(cap, c.Box.X, ly, rng.Range(-5, 3), tone, cc - 40));
                cursor += Math.Max(70, (cc - cursor) * 0.62); // 다음 칸은 이 칸이 끝나기 전에 휙 시작
            }
        }

        double total = Math.Max(strokes.Max(s => s.StartMs + s.DurationMs), labels.Count == 0 ? 0 : labels.Max(l => l.StartMs + 120));
        double k = total > BudgetMs ? BudgetMs / total : 1;
        if (k < 1)
        {
            strokes = [.. strokes.Select(s => s with { StartMs = s.StartMs * k, DurationMs = Math.Max(45, s.DurationMs * k) })];
            labels = [.. labels.Select(l => l with { StartMs = l.StartMs * k })];
            total = Math.Min(total, BudgetMs);
        }
        string key = sheet + "|" + a.Addr + "|" + string.Join(",", pre.Select(c => c.Addr)) + "|" + string.Join(",", dep.Select(c => c.Addr));
        return new SketchPlan(key, strokes, labels, total);
    }

    /// <summary>획 하나(굵은 첫 줄)와 덧그린 얇은 둘째 줄을 더하고, 이 획이 끝나는 시각을 돌려준다. speed는 px/ms.</summary>
    private static double AddPencil(List<SketchStroke> list, IReadOnlyList<PointD> pts, string tone, double width, double alpha, double start, double speed, HandRng rng)
    {
        double dur = Clamp(PencilStroke.Length(pts) / speed, 70, 280);
        list.Add(new SketchStroke(pts, tone, width, alpha, start, dur));
        list.Add(new SketchStroke(PencilStroke.Retrace(pts, rng), tone, Math.Max(1, width * 0.55), alpha * 0.65, start + 18, dur * 1.05));
        return start + dur;
    }

    private static PointD Center(MarkBox b) => new(b.X + b.W / 2, b.Y + b.H / 2);
    private static double Dist(PointD a, PointD b) => PencilStroke.Dist(a, b);
    private static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));
}
