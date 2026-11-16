// 기능: 다른 시트 흐름 그리기. 고른 칸에서 화면 아래 시트 탭까지 "쓕" 하고 휘어 내려갔다가 돌아오는 화살표 한 쌍과 그 탭을 두른 고리, 짧은 설명("매출 시트 G15에서 가져와요")을 만든다.
//       값이 들어오는 쪽(쓰는 칸)은 돌아오는 화살표가 굵고, 값이 나가는 쪽(쓰이는 칸)은 가는 화살표가 굵다. 탭 자리는 엑셀이 알려 주지 않아 시트 이름 글자 수로 어림한다(탭 줄의 왼쪽 끝과 높이는 앱이 격자 아래쪽에서 구한다).
// 사용법: FlowSketch.Plan(..., cross: new SketchCross(pre, dep, tabNames, strip));  읽기: FlowSketch.ReadCross(items), FlowSketch.ReadTabs(items)
// 의존: Core/Marks(FlowSketch, PencilStroke, SketchLook). 화면·엑셀 없이 시험된다.
using System.Text.Json;

namespace ClockOut.Hunsu.Core.Marks;

/// <summary>다른 시트 칸 하나(시트 이름과 주소).</summary>
public sealed record CrossRef(string Sheet, string Addr);

/// <summary>다른 시트 정보: 쓰는 칸 쪽 Pre, 쓰이는 칸 쪽 Dep, 보이는 시트 탭 이름 순서, 시트 탭 줄의 자리(창 안 DIP, 왼쪽 끝이 첫 탭).</summary>
public sealed record SketchCross(IReadOnlyList<CrossRef> Pre, IReadOnlyList<CrossRef> Dep, IReadOnlyList<string> Tabs, MarkBox Strip)
{
    public bool IsEmpty => Pre.Count == 0 && Dep.Count == 0;
}

public static partial class FlowSketch
{
    /// <summary>방향마다 그리는 시트 수의 한도.</summary>
    public const int MaxCrossSheets = 3;

    /// <summary>flow.graph의 items 배열에서 cross가 true인 줄만 읽는다(시트·주소가 있어야 함).</summary>
    public static IReadOnlyList<CrossRef> ReadCross(JsonElement items)
    {
        var list = new List<CrossRef>();
        if (items.ValueKind != JsonValueKind.Array) return list;
        foreach (var e in items.EnumerateArray())
        {
            if (e.ValueKind != JsonValueKind.Object) continue;
            if (!e.TryGetProperty("cross", out var c) || c.ValueKind != JsonValueKind.True) continue;
            string sheet = Str(e, "sheet"), addr = Str(e, "addr");
            if (sheet.Length > 0 && addr.Length > 0) list.Add(new CrossRef(sheet, addr));
        }
        return list;
    }

    /// <summary>sheets의 items 배열에서 시트 이름을 순서대로 읽는다.</summary>
    public static IReadOnlyList<string> ReadTabs(JsonElement items)
    {
        var list = new List<string>();
        if (items.ValueKind != JsonValueKind.Array) return list;
        foreach (var e in items.EnumerateArray())
        {
            string name = Str(e, "name");
            if (name.Length > 0) list.Add(name);
        }
        return list;
    }

    private static string Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    /// <summary>탭 하나의 자리 어림: 한글·한자는 넓게, 영문·숫자는 좁게, 양쪽 여백 포함. 이름을 못 찾으면 첫 탭 자리.</summary>
    public static MarkBox TabBox(SketchCross cross, string name)
    {
        double x = cross.Strip.X;
        foreach (var t in cross.Tabs)
        {
            double w = Math.Max(48, 26 + t.Sum(ch => ch >= 0x2E80 ? 13.0 : 7.5));
            if (t == name) return new MarkBox(x, cross.Strip.Y, w, cross.Strip.H);
            x += w;
        }
        return new MarkBox(cross.Strip.X, cross.Strip.Y, 60, cross.Strip.H);
    }

    /// <summary>한 방향의 다른 시트들을 그리고 끝나는 시각을 돌려준다. isPre면 값이 들어옴(돌아오는 화살표가 굵음), 아니면 나감(내려가는 화살표가 굵음).</summary>
    private static double AddCross(Ctx ctx, string sheet, SketchCell a, IReadOnlyList<CrossRef> refs, bool isPre, string tone, SketchCross cross, double start)
    {
        double cursor = start;
        foreach (var group in refs.GroupBy(r => r.Sheet).Take(MaxCrossSheets))
        {
            string name = group.Key, first = group.First().Addr;
            int more = group.Count() - 1;
            var tab = TabBox(cross, name);
            var rng = new HandRng(HandRng.Seed(sheet + "!" + a.Addr + (isPre ? "<" : ">") + name));
            double cc = cursor;
            int before = ctx.Strokes.Count, labelsBefore = ctx.Labels.Count;

            cc = AddPencil(ctx, PencilStroke.Loop(tab, rng, 3), tone, 2.2, 0.9, cc, 3.0, rng);          // 탭을 빙 두른다
            var tabTop = new PointD(tab.X + tab.W / 2, tab.Y - 6);
            var p0 = PencilStroke.ExitPoint(a.Box, tabTop, 4);
            double dist = PencilStroke.Dist(p0, tabTop);
            double bow = Math.Min(80, dist * 0.22) * (rng.Coin() ? 1 : -1) * Math.Max(0.6, ctx.Look.Bow);
            var down = PencilStroke.Wobbly(p0, tabTop, rng, bow, ctx.Look.Wobble);                         // 쓕~ 내려감
            var backFrom = new PointD(tabTop.X + 12, tabTop.Y);
            var backTo = PencilStroke.ExitPoint(a.Box, backFrom, 6);
            var back = PencilStroke.Wobbly(backFrom, backTo, rng, bow, ctx.Look.Wobble);                    // 돌아옴(같은 부호의 휘어짐이라 반대쪽 곡선)
            double wDown = isPre ? 0.6 : 1.0, wBack = isPre ? 1.0 : 0.6;
            double downStart = cc;
            cc = AddPencil(ctx, down, tone, 2.8 * wDown, 0.95 * wDown, cc, 3.4, rng);
            AddHead(ctx, down, tone, 2.8 * wDown, cc, rng);
            if (!isPre) AddBadge(ctx, down, tone, downStart, cc);
            double backStart = cc;
            cc = AddPencil(ctx, back, tone, 2.8 * wBack, 0.95 * wBack, cc - 20, 3.4, rng);
            AddHead(ctx, back, tone, 2.8 * wBack, cc, rng);
            if (isPre) AddBadge(ctx, back, tone, backStart, cc);

            // 간편 설명: 탭 위쪽에 손글씨로
            string note = (isPre ? $"{name} 시트 {first}" : $"{name} 시트 {first}") + (more > 0 ? $" 외 {more}칸" : "") + (isPre ? " 에서 가져와요" : " 로 보내요");
            ctx.Labels.Add(new SketchLabel(note, Math.Max(4, tab.X - 8), tab.Y - 38 - 22 * ctx.CrossNotes++, rng.Range(-3, 2), tone, cc - 60));

            // 이 묶음에서 더해진 것은 모두 늦게 도착하는 것으로 표시한다
            for (int i = before; i < ctx.Strokes.Count; i++) ctx.Strokes[i] = ctx.Strokes[i] with { Late = true };
            for (int i = labelsBefore; i < ctx.Labels.Count; i++) ctx.Labels[i] = ctx.Labels[i] with { Late = true };
            cursor += Math.Max(80, (cc - cursor) * 0.62);
        }
        return cursor;
    }

    private static void AddHead(Ctx ctx, IReadOnlyList<PointD> line, string tone, double width, double at, HandRng rng)
    {
        var last = line[^1]; var prev = line[Math.Max(0, line.Count - 3)];
        var head = PencilStroke.Arrowhead(last, last.X - prev.X, last.Y - prev.Y, rng, 11 * Math.Max(0.8, ctx.Look.Width));
        ctx.Strokes.Add(new SketchStroke(head, tone, W(ctx, width), 0.95 * ctx.Look.Alpha, at - 30, 80, false, PencilStroke.Pressure(head, new HandRng(7), ctx.Look.Taper * 0.5, 0)));
    }

    private static void AddBadge(Ctx ctx, IReadOnlyList<PointD> line, string tone, double start, double end)
    {
        var mid = line[line.Count / 2];
        ctx.Labels.Add(new SketchLabel((++ctx.Number).ToString(), mid.X, mid.Y, 0, tone, start + (end - start) * 0.5, true));
    }
}
