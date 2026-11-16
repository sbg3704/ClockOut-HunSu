// 기능: 손으로 그린 듯한 획의 점 목록을 만든다(색연필 수식 흐름의 바탕). 곧은 선에 떨림·휘어짐·삐져나옴을 더하고, 칸 둘레를 손으로 두른 고리, 비스듬한 칠하기(스크리블), 화살촉(V)을 만든다.
//       같은 씨앗(칸 주소)이면 늘 같은 모양이라 스크롤해도 깜빡이지 않는다. 화면(WPF) 없이 시험된다.
// 사용법: var rng = new HandRng(HandRng.Seed("A1>D1")); var line = PencilStroke.Wobbly(a, b, rng); var ring = PencilStroke.Loop(box, rng); var head = PencilStroke.Arrowhead(tip, dirX, dirY, rng);
// 의존: Core/Marks/MarkGeometry(MarkBox). 외부 패키지 없음.
namespace ClockOut.Hunsu.Core.Marks;

public readonly record struct PointD(double X, double Y);

/// <summary>씨앗이 같으면 늘 같은 수를 내는 작은 난수(xorshift64*). string.GetHashCode는 실행마다 달라서 쓰지 않는다.</summary>
public sealed class HandRng
{
    private ulong _s;

    public HandRng(ulong seed)
    {
        _s = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        Next(); Next();
    }

    public static ulong Seed(string text)
    {
        ulong h = 14695981039346656037UL; // FNV-1a 64
        foreach (char c in text) { h ^= c; h *= 1099511628211UL; }
        return h;
    }

    private ulong Next()
    {
        _s ^= _s >> 12; _s ^= _s << 25; _s ^= _s >> 27;
        return _s * 2685821657736338717UL;
    }

    /// <summary>0 이상 1 미만.</summary>
    public double NextDouble() => (Next() >> 11) / (double)(1UL << 53);

    public double Range(double min, double max) => min + (max - min) * NextDouble();

    public bool Coin() => (Next() & 1) == 1;
}

public static class PencilStroke
{
    private const double Step = 6.0; // 점 사이 간격(px). 이보다 촘촘하면 점만 늘고 모양은 같다.

    public static double Length(IReadOnlyList<PointD> pts)
    {
        double sum = 0;
        for (int i = 1; i < pts.Count; i++) sum += Dist(pts[i - 1], pts[i]);
        return sum;
    }

    public static double Dist(PointD a, PointD b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>a에서 b로 휘어 가며 떨리는 선. bow는 가운데가 휘는 정도(px, 부호가 방향), wobble은 떨림 크기(px).</summary>
    public static IReadOnlyList<PointD> Wobbly(PointD a, PointD b, HandRng rng, double bow = 0, double wobble = 1.6)
    {
        double len = Dist(a, b);
        if (len < 1) return [a, b];
        double ux = (b.X - a.X) / len, uy = (b.Y - a.Y) / len;
        double nx = -uy, ny = ux;
        // 시작·끝이 살짝 어긋나고 끝은 조금 지나친다(손이 멈추기 전에 더 나감).
        var p0 = new PointD(a.X + nx * rng.Range(-1.2, 1.2), a.Y + ny * rng.Range(-1.2, 1.2));
        double over = Math.Min(5, len * 0.04);
        var p2 = new PointD(b.X + ux * over + nx * rng.Range(-1.5, 1.5), b.Y + uy * over + ny * rng.Range(-1.5, 1.5));
        var c = new PointD((p0.X + p2.X) / 2 + nx * bow, (p0.Y + p2.Y) / 2 + ny * bow);
        double f1 = rng.Range(1.2, 2.2), f2 = rng.Range(2.8, 4.6), ph1 = rng.Range(0, 6.283), ph2 = rng.Range(0, 6.283);
        int n = Math.Max(4, (int)Math.Ceiling(len / Step));
        var pts = new List<PointD>(n + 1);
        for (int i = 0; i <= n; i++)
        {
            double t = i / (double)n, u = 1 - t;
            double x = u * u * p0.X + 2 * u * t * c.X + t * t * p2.X;
            double y = u * u * p0.Y + 2 * u * t * c.Y + t * t * p2.Y;
            double env = Math.Sqrt(Math.Sin(Math.PI * t)); // 양 끝은 덜 떨림
            double off = wobble * env * (0.65 * Math.Sin(6.283 * f1 * t + ph1) + 0.35 * Math.Sin(6.283 * f2 * t + ph2));
            pts.Add(new PointD(x + nx * off, y + ny * off));
        }
        return pts;
    }

    /// <summary>칸(또는 범위) 둘레를 손으로 한 바퀴 두른 고리. 조금 더 돌아 끝이 처음과 어긋나게 겹친다. 네모에 가까운 둥근 모양.</summary>
    public static IReadOnlyList<PointD> Loop(MarkBox box, HandRng rng, double pad = 3)
    {
        double cx = box.X + box.W / 2, cy = box.Y + box.H / 2;
        double rx = box.W / 2 + pad, ry = box.H / 2 + pad;
        double start = rng.Range(-2.6, -1.6);         // 왼쪽 위쪽에서 시작
        double sweep = 6.283 * rng.Range(1.07, 1.16);  // 한 바퀴보다 조금 더
        double ph = rng.Range(0, 6.283), grow = rng.Range(2.0, 4.5);
        double perim = 2 * Math.PI * Math.Sqrt((rx * rx + ry * ry) / 2);
        int n = Math.Max(12, (int)Math.Ceiling(perim * sweep / 6.283 / Step));
        var pts = new List<PointD>(n + 1);
        for (int i = 0; i <= n; i++)
        {
            double t = i / (double)n, th = start + sweep * t;
            double cs = Math.Cos(th), sn = Math.Sin(th);
            // 지수 2.8 초타원: 칸 모서리에 붙는 둥근 네모
            double ex = Math.Sign(cs) * Math.Pow(Math.Abs(cs), 2.0 / 2.8);
            double ey = Math.Sign(sn) * Math.Pow(Math.Abs(sn), 2.0 / 2.8);
            double wob = 1 + 0.03 * Math.Sin(2 * th + ph) + 0.02 * Math.Sin(5 * th + ph * 1.7);
            double gx = grow * t; // 끝 쪽이 조금 바깥으로 벌어짐
            pts.Add(new PointD(cx + (rx * wob + gx) * ex, cy + (ry * wob + gx) * ey));
        }
        return pts;
    }

    /// <summary>손으로 그린 네모 한 획: 네 변을 이어 긋되 모서리마다 조금 지나치고 어긋난다.</summary>
    public static IReadOnlyList<PointD> Box(MarkBox box, HandRng rng, double pad = 2)
    {
        double l = box.X - pad, t = box.Y - pad, r = box.X + box.W + pad, b = box.Y + box.H + pad;
        var corners = new[] { new PointD(l, t), new PointD(r, t), new PointD(r, b), new PointD(l, b), new PointD(l, t) };
        var pts = new List<PointD>();
        for (int i = 0; i < 4; i++) pts.AddRange(Wobbly(corners[i], corners[i + 1], rng, rng.Range(-1.5, 1.5), 1.0));
        return pts;
    }

    /// <summary>범위를 감싸는 대괄호 한 짝(left면 [ , 아니면 ]). 위·아래 끝이 안쪽으로 꺾인다.</summary>
    public static IReadOnlyList<PointD> Bracket(MarkBox box, HandRng rng, bool left, double pad = 3)
    {
        double x = left ? box.X - pad : box.X + box.W + pad, t = box.Y - pad, b = box.Y + box.H + pad;
        double arm = Math.Min(10, box.W / 3 + 3) * (left ? 1 : -1);
        var pts = new List<PointD>();
        pts.AddRange(Wobbly(new PointD(x + arm, t), new PointD(x, t), rng, 0, 0.6));
        pts.AddRange(Wobbly(new PointD(x, t), new PointD(x, b), rng, rng.Range(-2, 2), 1.2));
        pts.AddRange(Wobbly(new PointD(x, b), new PointD(x + arm, b), rng, 0, 0.6));
        return pts;
    }

    /// <summary>칸 안을 비스듬히 오가며 칠하는 한 줄 스크리블. 줄 수는 maxLines로 줄인다(큰 칸도 가볍게).</summary>
    public static IReadOnlyList<PointD> Scribble(MarkBox box, HandRng rng, double spacing = 5, int maxLines = 14)
    {
        double x0 = box.X + 2, y0 = box.Y + 2, w = Math.Max(1, box.W - 4), h = Math.Max(1, box.H - 4);
        double diag = w + h;
        spacing = Math.Max(spacing, diag / maxLines);
        var pts = new List<PointD>();
        bool down = true;
        for (double d = spacing / 2; d < diag; d += spacing)
        {
            // 직선 x + y = d (왼쪽 아래로 내려가는 사선)와 상자의 교점
            double xa = Math.Max(0, d - h), xb = Math.Min(w, d);
            if (xb - xa < 1) continue;
            var p = new PointD(x0 + xa + rng.Range(-1, 1), y0 + (d - xa) + rng.Range(-1, 1)); // 아래 끝(x 작은 쪽)
            var q = new PointD(x0 + xb + rng.Range(-1, 1), y0 + (d - xb) + rng.Range(-1, 1)); // 위 끝
            if (down) { pts.Add(p); pts.Add(q); } else { pts.Add(q); pts.Add(p); }
            down = !down;
        }
        return pts;
    }

    /// <summary>끝점(tip)에서 진행 방향(dx,dy)의 반대쪽으로 벌어지는 V 화살촉. 한 획(날개 하나 → 끝 → 날개 하나).</summary>
    public static IReadOnlyList<PointD> Arrowhead(PointD tip, double dx, double dy, HandRng rng, double size = 11)
    {
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-6) { dx = 1; dy = 0; len = 1; }
        dx /= len; dy /= len;
        double ang = rng.Range(0.42, 0.60);
        double s1 = size * rng.Range(0.85, 1.15), s2 = size * rng.Range(0.85, 1.15);
        var w1 = Rotate(-dx, -dy, ang);
        var w2 = Rotate(-dx, -dy, -ang);
        return
        [
            new PointD(tip.X + w1.X * s1, tip.Y + w1.Y * s1),
            new PointD(tip.X + dx * rng.Range(0, 1.5), tip.Y + dy * rng.Range(0, 1.5)),
            new PointD(tip.X + w2.X * s2, tip.Y + w2.Y * s2),
        ];
    }

    /// <summary>캘리그라피 강약: 점마다 굵기 배율(평균 약 0.9). 처음은 가늘게 시작해 굵어지고 끝은 날렵하게 빠지며(taper), 납작한 펜촉처럼 방향에 따라 굵기가 달라진다(nib). 둘 다 0이면 모두 1.</summary>
    public static IReadOnlyList<double> Pressure(IReadOnlyList<PointD> pts, HandRng rng, double taper, double nib)
    {
        int n = pts.Count;
        var w = new double[n];
        double ph = rng.Range(0, 6.283), nibAngle = 0.7; // 펜촉 각도(약 40도)
        for (int i = 0; i < n; i++)
        {
            double t = n > 1 ? i / (double)(n - 1) : 0;
            double head = 0.3 + 0.7 * Smooth(t / 0.16), tail = 0.2 + 0.8 * Smooth((1 - t) / 0.24);
            double sway = 1 + 0.16 * Math.Sin(6.283 * 0.9 * t + ph);
            double press = 1 + taper * (head * tail * sway - 1);
            int a = Math.Max(0, i - 1), b = Math.Min(n - 1, i + 1);
            double ang = Math.Atan2(pts[b].Y - pts[a].Y, pts[b].X - pts[a].X);
            double nibK = 1 + nib * (2 * Math.Abs(Math.Sin(ang - nibAngle)) - 1);
            w[i] = Math.Max(0.12, press * nibK);
        }
        return w;
    }

    private static double Smooth(double x) { x = Math.Clamp(x, 0, 1); return x * x * (3 - 2 * x); }

    /// <summary>같은 획을 다시 한 번 덧그린 듯한 두 번째 획: 점마다 천천히 변하는 어긋남을 더한다.</summary>
    public static IReadOnlyList<PointD> Retrace(IReadOnlyList<PointD> pts, HandRng rng, double amount = 1.1)
    {
        double ax = rng.Range(0.4, 1.0) * amount, ay = rng.Range(0.4, 1.0) * amount, ph = rng.Range(0, 6.283);
        var result = new List<PointD>(pts.Count);
        for (int i = 0; i < pts.Count; i++)
        {
            double k = Math.Sin(i * 0.35 + ph);
            result.Add(new PointD(pts[i].X + ax * k + 0.5, pts[i].Y + ay * Math.Cos(i * 0.3 + ph) - 0.4));
        }
        return result;
    }

    /// <summary>상자 중심에서 target 쪽으로 나가다 상자 둘레(gap만큼 밖)와 만나는 점. 화살표가 칸 속이 아니라 가장자리에서 시작·끝나게 한다.</summary>
    public static PointD ExitPoint(MarkBox box, PointD target, double gap)
    {
        double cx = box.X + box.W / 2, cy = box.Y + box.H / 2;
        double dx = target.X - cx, dy = target.Y - cy;
        if (Math.Abs(dx) < 1e-9 && Math.Abs(dy) < 1e-9) return new PointD(cx, cy);
        double hw = box.W / 2 + gap, hh = box.H / 2 + gap;
        double t = Math.Min(Math.Abs(dx) < 1e-9 ? double.MaxValue : hw / Math.Abs(dx), Math.Abs(dy) < 1e-9 ? double.MaxValue : hh / Math.Abs(dy));
        return new PointD(cx + dx * t, cy + dy * t);
    }

    private static PointD Rotate(double x, double y, double a) => new(x * Math.Cos(a) - y * Math.Sin(a), x * Math.Sin(a) + y * Math.Cos(a));
}
