// 기능: 수식 흐름을 색연필 스케치로 바꾸는 계획. 활성 칸을 두르고, 쓰는 칸(앞)에서 활성 칸으로, 활성 칸에서 쓰이는 칸(뒤)으로 휘어진 손 화살표를 긋는다.
//       획마다 시작 시각과 걸리는 시간을 정해 "휙휙휙" 이어 그려지게 한다(겹쳐 시작). 그리기와 움직임은 앱(Overlay/PencilPainter)이 맡고, 여기는 점·시간만 계산한다.
// 사용법: var plan = FlowSketch.Plan(sheet, active, pre, dep, new("warn","info","good"), look, pre2, dep2);  (look·pre2·dep2는 없어도 됨: 색연필, 한 단계)  plan.Strokes / plan.Labels / plan.Key
//         칸 읽기: FlowSketch.ReadCells(jsonArray, panes, windowRect, scale)  (addr가 있는 줄만, 보이는 부분만)
// 의존: Core/Marks(PencilStroke, SketchLook, MarkGeometry), Core/PixelRect. 화면·엑셀 없이 시험된다.
using System.Text.Json;

namespace ClockOut.Hunsu.Core.Marks;

/// <summary>주소가 붙은 칸 자리(창 안 DIP). To는 두 번째 단계 칸이 이어지는 첫 단계 칸의 주소.</summary>
public sealed record SketchCell(string Addr, MarkBox Box, string To = "");

/// <summary>획 하나. StartMs부터 DurationMs 동안 처음에서 끝으로 그려진다. Shade는 칠하기(스크리블). Pressure는 점마다 굵기 배율(캘리그라피 강약, 없으면 일정한 굵기).</summary>
public sealed record SketchStroke(IReadOnlyList<PointD> Points, string Tone, double Width, double Alpha, double StartMs, double DurationMs, bool Shade = false, IReadOnlyList<double>? Pressure = null);

/// <summary>손글씨 이름표. StartMs에 나타난다. Angle은 도. Badge면 (X,Y)가 가운데인 작은 동그라미 번호표.</summary>
public sealed record SketchLabel(string Text, double X, double Y, double Angle, string Tone, double StartMs, bool Badge = false);

public sealed record SketchTones(string Active, string Pre, string Dep);

public sealed record SketchPlan(string Key, IReadOnlyList<SketchStroke> Strokes, IReadOnlyList<SketchLabel> Labels, double TotalMs, bool Grain = true)
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
            if (boxes.Count > 0) list.Add(new SketchCell(a.GetString() ?? "", boxes[0], e.TryGetProperty("to", out var to) && to.ValueKind == JsonValueKind.String ? to.GetString() ?? "" : ""));
        }
        return list;
    }

    private static int? Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d) && double.IsFinite(d) ? (int)Math.Round(d) : null;

    /// <summary>두 번째 단계 한 묶음에서 그리는 칸의 최대 수.</summary>
    public const int MaxPerGroup2 = 8;

    private const double Level2Weight = 0.75; // 두 번째 단계는 조금 가늘고 옅게

    private sealed class Ctx(SketchLook look)
    {
        public readonly SketchLook Look = look;
        public readonly List<SketchStroke> Strokes = [];
        public readonly List<SketchLabel> Labels = [];
        public int Number;
    }

    public static SketchPlan Plan(string sheet, IReadOnlyList<SketchCell> active, IReadOnlyList<SketchCell> pre, IReadOnlyList<SketchCell> dep, SketchTones tones,
        SketchLook? look = null, IReadOnlyList<SketchCell>? pre2 = null, IReadOnlyList<SketchCell>? dep2 = null)
    {
        if (active.Count == 0 || (pre.Count == 0 && dep.Count == 0)) return SketchPlan.Empty;
        var ctx = new Ctx(look ?? SketchLook.Pencil);
        var a = active[0];
        double cursor = 0;

        // 활성 칸: 두 겹으로 두른다
        var rngA = new HandRng(HandRng.Seed(sheet + "!" + a.Addr + "*"));
        cursor = AddPencil(ctx, PencilStroke.Loop(a.Box, rngA, 3.5), tones.Active, 3.0, 0.95, cursor, 3.2, rngA);
        cursor = AddPencil(ctx, PencilStroke.Loop(a.Box, rngA, 7), tones.Active, 2.0, 0.8, cursor - 60, 3.2, rngA);
        ctx.Labels.Add(new SketchLabel("여기", a.Box.X + a.Box.W + 2, a.Box.Y - 15, -4, tones.Active, 60));

        // 단계 순서 = 계산이 흘러가는 순서: 더 앞(2단계 쓰는 칸) → 쓰는 칸 → 쓰이는 칸 → 더 뒤. 번호도 이 순서로 붙는다.
        var ac = Center(a.Box);
        (IReadOnlyList<SketchCell> Cells, IReadOnlyList<SketchCell>? Parents, string Tone, bool IsPre, bool Level2)[] stages =
        [
            (pre2 ?? [], pre, tones.Pre, true, true), (pre, null, tones.Pre, true, false),
            (dep, null, tones.Dep, false, false), (dep2 ?? [], dep, tones.Dep, false, true),
        ];
        foreach (var (group, parents, tone, isPre, level2) in stages)
        {
            double weight = level2 ? Level2Weight : 1;
            // 가까운 칸부터 한도만큼 고르고, 번호는 읽는 순서(위→아래, 왼쪽→오른쪽). 그리는 순서도 같다.
            var near = group.Where(c => c.Addr != a.Addr && (!level2 || parents!.Any(p => p.Addr == c.To)))
                .OrderBy(c => Dist(Center(c.Box), ac)).Take(level2 ? MaxPerGroup2 : MaxPerGroup)
                .OrderBy(c => Math.Round(c.Box.Y / 12)).ThenBy(c => c.Box.X).ToList();
            for (int i = 0; i < near.Count; i++)
            {
                var c = near[i];
                var target = level2 ? parents!.First(p => p.Addr == c.To) : a;
                var rng = new HandRng(HandRng.Seed(sheet + "!" + (isPre ? c.Addr + ">" + target.Addr : target.Addr + ">" + c.Addr)));
                double cc = cursor;
                // 이웃 칸: 칠하고(작은 칸만, 프로필이 허용하면) 모양으로 둘러싼다
                if (ctx.Look.ShadeWidth > 0 && c.Box.W * c.Box.H <= ShadeMaxArea)
                {
                    var scr = PencilStroke.Scribble(c.Box, rng, ctx.Look.ShadeWidth > 4 ? 9 : 5, 14);
                    if (scr.Count > 1) ctx.Strokes.Add(new SketchStroke(scr, tone, ctx.Look.ShadeWidth * weight, ctx.Look.ShadeAlpha * weight, cc, Clamp(PencilStroke.Length(scr) / 4.5, 80, 220), true));
                    cc += 40;
                }
                bool range = c.Box.W > a.Box.W * 1.4 || c.Box.H > a.Box.H * 1.4;
                cc = DrawProfile(ctx, c.Box, isPre, range, tone, cc, rng, weight);
                // 화살표: 앞쪽은 이웃 → 대상, 뒤쪽은 대상 → 이웃 (대상은 고른 칸, 두 번째 단계는 이어지는 첫 단계 칸)
                var from = isPre ? c : target;
                var to = isPre ? target : c;
                var p0 = PencilStroke.ExitPoint(from.Box, Center(to.Box), 4);
                var p1 = PencilStroke.ExitPoint(to.Box, Center(from.Box), 6);
                double len = PencilStroke.Dist(p0, p1);
                if (len >= 12)
                {
                    double bow = Math.Min(40, len * 0.14 * ctx.Look.Bow) * (rng.Coin() ? 1 : -1);
                    var line = PencilStroke.Wobbly(p0, p1, rng, bow, ctx.Look.Wobble);
                    double lineStart = cc;
                    cc = AddPencil(ctx, line, tone, 2.8 * weight, 0.95 * weight, cc, 2.6, rng);
                    // 번호표: 화살표 한가운데에 작은 동그라미 숫자(이 순서로 따라가면 된다)
                    var mid = line[line.Count / 2];
                    ctx.Labels.Add(new SketchLabel((++ctx.Number).ToString(), mid.X, mid.Y, 0, tone, lineStart + (cc - lineStart) * 0.5, true));
                    var last = line[^1]; var prev = line[Math.Max(0, line.Count - 3)];
                    var head = PencilStroke.Arrowhead(last, last.X - prev.X, last.Y - prev.Y, rng, 11 * Math.Max(0.8, ctx.Look.Width));
                    ctx.Strokes.Add(new SketchStroke(head, tone, W(ctx, 2.8 * weight), 0.95 * weight * ctx.Look.Alpha, cc - 30, 80, false, PencilStroke.Pressure(head, new HandRng(7), ctx.Look.Taper * 0.5, 0)));
                    cc += 50;
                }
                string cap = i == 0
                    ? (level2 ? (isPre ? $"{c.Addr} (더 앞)" : $"{c.Addr} (더 뒤)") : (isPre ? $"{c.Addr} (쓰는 칸)" : $"{c.Addr} (쓰이는 칸)"))
                    : c.Addr;
                double ly = c.Box.Y >= 18 ? c.Box.Y - 17 : c.Box.Y + c.Box.H + 1;
                ctx.Labels.Add(new SketchLabel(cap, c.Box.X, ly, rng.Range(-5, 3), tone, cc - 40));
                cursor += Math.Max(70, (cc - cursor) * 0.62); // 다음 칸은 이 칸이 끝나기 전에 휙 시작
            }
        }

        var strokes = ctx.Strokes;
        var labels = ctx.Labels;
        double total = Math.Max(strokes.Max(s => s.StartMs + s.DurationMs), labels.Count == 0 ? 0 : labels.Max(l => l.StartMs + 120));
        double k = total > BudgetMs ? BudgetMs / total : 1;
        if (k < 1)
        {
            strokes = [.. strokes.Select(s => s with { StartMs = s.StartMs * k, DurationMs = Math.Max(45, s.DurationMs * k) })];
            labels = [.. labels.Select(l => l with { StartMs = l.StartMs * k })];
            total = Math.Min(total, BudgetMs);
        }
        string key = sheet + "|" + ctx.Look.Name + "|" + a.Addr + "|" + string.Join(",", pre.Select(c => c.Addr)) + "|" + string.Join(",", dep.Select(c => c.Addr))
            + "|" + string.Join(",", (pre2 ?? []).Select(c => c.Addr + "<" + c.To)) + "|" + string.Join(",", (dep2 ?? []).Select(c => c.Addr + "<" + c.To));
        return new SketchPlan(key, strokes, labels, total, ctx.Look.Grain);
    }

    private static double W(Ctx ctx, double width) => Math.Max(0.8, width * ctx.Look.Width);

    /// <summary>이웃 칸의 "프로필"(모양): 쓰는 칸은 네모(범위면 대괄호 한 쌍), 쓰이는 칸은 동그라미(범위면 동그라미 + 물결 밑줄). 모양만 봐도 앞/뒤와 한 칸/범위가 구별된다.</summary>
    private static double DrawProfile(Ctx ctx, MarkBox box, bool isPre, bool range, string tone, double start, HandRng rng, double weight)
    {
        double w = 2.3 * weight, al = 0.9 * weight;
        if (isPre && !range) return AddPencil(ctx, PencilStroke.Box(box, rng, 2.5), tone, w, al, start, 3.0, rng);
        if (isPre)
        {
            double e = AddPencil(ctx, PencilStroke.Bracket(box, rng, true), tone, w * 1.1, al, start, 3.0, rng);
            return AddPencil(ctx, PencilStroke.Bracket(box, rng, false), tone, w * 1.1, al, e - 40, 3.0, rng);
        }
        double end = AddPencil(ctx, PencilStroke.Loop(box, rng, 2.5), tone, w, al, start, 3.0, rng);
        if (!range) return end;
        var under = PencilStroke.Wobbly(new PointD(box.X, box.Y + box.H + 6), new PointD(box.X + box.W, box.Y + box.H + 6), rng, 0, ctx.Look.Wobble * 1.8);
        return AddPencil(ctx, under, tone, w * 0.85, al * 0.95, end - 40, 3.0, rng);
    }

    /// <summary>획 하나(프로필에 따라 굵기·진하기를 곱함)와, 프로필이 허용하면 덧그린 얇은 둘째 줄을 더하고, 이 획이 끝나는 시각을 돌려준다. speed는 px/ms.</summary>
    private static double AddPencil(Ctx ctx, IReadOnlyList<PointD> pts, string tone, double width, double alpha, double start, double speed, HandRng rng)
    {
        double dur = Clamp(PencilStroke.Length(pts) / speed, 70, 280);
        double w = W(ctx, width), al = Math.Min(1, alpha * ctx.Look.Alpha);
        ctx.Strokes.Add(new SketchStroke(pts, tone, w, al, start, dur, false, PencilStroke.Pressure(pts, rng, ctx.Look.Taper, ctx.Look.Nib)));
        if (ctx.Look.Retrace)
        {
            var again = PencilStroke.Retrace(pts, rng);
            ctx.Strokes.Add(new SketchStroke(again, tone, Math.Max(1, w * 0.55), al * 0.65, start + 18, dur * 1.05, false, PencilStroke.Pressure(again, rng, ctx.Look.Taper, 0)));
        }
        return start + dur;
    }

    private static PointD Center(MarkBox b) => new(b.X + b.W / 2, b.Y + b.H / 2);
    private static double Dist(PointD a, PointD b) => PencilStroke.Dist(a, b);
    private static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));
}
