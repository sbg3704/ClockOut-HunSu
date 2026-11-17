// 기능: 패널 한 장의 손그림 장식(테두리, 제목줄 빗금·형광펜 칠·분필 밑줄)의 획 목록을 만든다. 크기와 룩 종류가 같으면 늘 같은 모양이라 다시 그려도 깜빡이지 않는다.
//       화면(WPF) 없이 시험된다. 색과 굵기는 화면 쪽(Ui/Frame/HandFrame)이 룩의 테마 이름으로 입힌다.
// 사용법: var strokes = SkinSketch.Plan(width, height, titleHeight, titleTextWidth, SkinKind.Pen, seed: 1);  → 획마다 Role과 점 목록
// 의존: Core/Marks(PencilStroke, HandRng, MarkBox), Core/Settings(SkinKind).
using ClockOut.Hunsu.Core.Settings;

namespace ClockOut.Hunsu.Core.Marks;

public enum SkinRole
{
    Frame,      // 바깥 테두리(룩의 선 색)
    FrameEcho,  // 덧그린 두 번째 테두리(연필·분필, 흐리게)
    Hatch,      // 제목줄 색연필 빗금(강조색)
    Swash,      // 제목 글자 밑 형광펜 칠(강조색, 굵고 반투명)
    TitleLine,  // 제목줄 아래 선
}

public sealed record SkinStroke(SkinRole Role, IReadOnlyList<PointD> Points);

public static class SkinSketch
{
    public static IReadOnlyList<SkinStroke> Plan(double width, double height, double titleHeight, double titleTextWidth, SkinKind kind, ulong seed)
    {
        if (kind == SkinKind.None || width < 40 || height < 24) return [];
        var rng = new HandRng(seed ^ ((ulong)kind * 0x9E3779B97F4A7C15UL));
        var list = new List<SkinStroke>();
        var box = new MarkBox(2, 2, width - 4, height - 4);
        var frame = PencilStroke.Box(box, rng, pad: 0);
        list.Add(new SkinStroke(SkinRole.Frame, frame));
        if (kind is SkinKind.Pencil or SkinKind.Chalk)
            list.Add(new SkinStroke(SkinRole.FrameEcho, PencilStroke.Retrace(PencilStroke.Box(box, rng, pad: 1.2), rng, 1.4)));

        double th = Math.Max(14, titleHeight);
        switch (kind)
        {
            case SkinKind.Pencil:
                list.AddRange(Hatch(4, 4, width - 8, th - 5, 5, rng));
                list.Add(new SkinStroke(SkinRole.TitleLine, PencilStroke.Wobbly(new PointD(5, th), new PointD(width - 5, th + 1), rng, 0.8, 0.9)));
                break;
            case SkinKind.Pen:
                double sw = Math.Clamp(titleTextWidth + 10, 24, width - 40);
                list.Add(new SkinStroke(SkinRole.Swash, PencilStroke.Wobbly(new PointD(26, th * 0.74), new PointD(26 + sw, th * 0.72), rng, 0.6, 0.5)));
                break;
            case SkinKind.Chalk:
                list.Add(new SkinStroke(SkinRole.TitleLine, PencilStroke.Wobbly(new PointD(8, th), new PointD(width - 8, th), rng, 0.7, 0.8)));
                break;
        }
        return list;
    }

    /// <summary>사각형 안을 왼쪽 아래→오른쪽 위로 비스듬히 긋는 따로따로의 빗금(색연필로 칠한 띠).</summary>
    private static IEnumerable<SkinStroke> Hatch(double x, double y, double w, double h, double gap, HandRng rng)
    {
        double diag = w + h;
        for (double d = gap / 2; d < diag; d += gap * rng.Range(0.85, 1.2))
        {
            double xa = Math.Max(0, d - h), xb = Math.Min(w, d);
            if (xb - xa < 2) continue;
            var p = new PointD(x + xa, y + (d - xa));
            var q = new PointD(x + xb, y + (d - xb));
            yield return new SkinStroke(SkinRole.Hatch, PencilStroke.Wobbly(p, q, rng, 0, 0.5));
        }
    }
}
