// 기능: 엑셀 옆 모듈 아이콘 런처의 순수 계산(docs/09 2장). ① 모듈을 기능 묶음(order 100 단위 범위)으로 나누고 ② 배치 모양 5종
//       (초승달·부채·꽃송이·자석 줄·알약 목록)마다 아이콘 자리를 낸다. 화면이나 WPF를 모른다.
// 사용법: var items = LauncherGroups.Build(modules, grouped: true);
//         var plan = LauncherShapes.Plan(DockShapes.Crescent, items.Count, scale: 1, width: 360, height: 600, awake: true, grouped: true);
//         plan.Items[i] → 오른쪽 가장자리(x = width) 기준 아이콘 중심. 왼쪽 도크는 부르는 쪽이 x를 width - x로 뒤집는다.
// 의존: 없음(순수 계산). 크기 단위는 논리 px(DIP).
namespace ClockOut.Hunsu.Core.Layout;

/// <summary>배치 모양 이름(settings.dockShape).</summary>
public static class DockShapes
{
    public const string Crescent = "crescent", Fan = "fan", Cluster = "cluster", Rail = "rail", Pills = "pills";
    public static readonly IReadOnlyList<string> All = [Crescent, Fan, Cluster, Rail, Pills];

    public static string Normalize(string? shape)
    {
        string s = (shape ?? "").Trim().ToLowerInvariant();
        return All.Contains(s) ? s : Crescent;
    }
}

/// <summary>런처에 넣을 모듈 하나: id, 이름, 순서, 아이콘 뜻 이름, 배지 글, 톤, 지금 패널이 열려 있는가.</summary>
public sealed record LauncherModule(string Id, string Name, int Order, string Icon, string Badge, string Tone, bool Open);

/// <summary>1단계 칸: 묶음 또는 모듈 하나. 묶음이면 Members가 2개 이상이다.</summary>
public sealed record LauncherItem(string Key, bool IsGroup, string Name, string Icon, string Badge, string Tone, bool Open, IReadOnlyList<LauncherModule> Members);

public static class LauncherGroups
{
    /// <summary>묶음 = docs/01 6.2의 order 범위. (아래끝, 위끝, 열쇠, 이름, 대표 아이콘)</summary>
    public static readonly IReadOnlyList<(int Lo, int Hi, string Key, string Name, string Icon)> Ranges =
    [
        (0, 199, "status", "상태", "sum"),
        (200, 299, "check", "점검", "audit"),
        (300, 399, "move", "이동", "sheets"),
        (400, 499, "formula", "수식", "flow"),
        (500, 899, "other", "그 밖", "more"),
        (900, int.MaxValue, "ref", "참고", "book"),
    ];

    /// <summary>모듈을 1단계 칸으로. grouped=false면 모듈마다 한 칸. 켜진 모듈이 하나뿐인 묶음은 모듈 칸으로, 없는 묶음은 뺀다.</summary>
    public static IReadOnlyList<LauncherItem> Build(IEnumerable<LauncherModule> modules, bool grouped)
    {
        var sorted = modules.OrderBy(m => m.Order).ThenBy(m => m.Id, StringComparer.Ordinal).ToList();
        if (!grouped) return sorted.Select(Single).ToList();
        var items = new List<LauncherItem>();
        foreach (var r in Ranges)
        {
            var members = sorted.Where(m => m.Order >= r.Lo && m.Order <= r.Hi).ToList();
            if (members.Count == 0) continue;
            if (members.Count == 1) { items.Add(Single(members[0])); continue; }
            items.Add(new LauncherItem(r.Key, true, r.Name, r.Icon, SumBadges(members.Select(m => m.Badge)), TopTone(members.Select(m => m.Tone)), members.Any(m => m.Open), members));
        }
        return items;
    }

    private static LauncherItem Single(LauncherModule m) => new(m.Id, false, m.Name, m.Icon, m.Badge, m.Tone, m.Open, [m]);

    /// <summary>묶음 배지 = 안쪽 숫자의 합. 숫자가 아닌 배지가 있으면 점 하나(•), 다 비면 빈 글.</summary>
    public static string SumBadges(IEnumerable<string> badges)
    {
        long sum = 0;
        bool any = false, other = false;
        foreach (var b in badges)
        {
            string t = (b ?? "").Trim();
            if (t.Length == 0 || t == "-" || t == "0") continue;
            if (long.TryParse(t.Replace(",", ""), out var n)) { sum += n; any = true; }
            else other = true;
        }
        if (any) return sum.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        return other ? "•" : "";
    }

    /// <summary>가장 높은 톤: bad > warn > info > good > muted.</summary>
    public static string TopTone(IEnumerable<string> tones)
    {
        string[] rank = ["bad", "warn", "info", "good", "muted"];
        var set = tones.Select(t => (t ?? "").Trim()).ToHashSet();
        return rank.FirstOrDefault(set.Contains) ?? "info";
    }
}

/// <summary>점(아이콘 중심, 논리 px).</summary>
public readonly record struct LPoint(double X, double Y);

/// <summary>자리 계산 결과. Rest는 잠든 모습: "dots"(작은 점), "hub"(훈 원만), "dim"(흐린 아이콘).</summary>
public sealed record LauncherPlan(LPoint Hub, double HubSize, bool ShowHub, double ItemSize, IReadOnlyList<LPoint> Items, string Rest, IReadOnlyList<LPoint> RestDots, bool Pills, double WakeDistance);

public static class LauncherShapes
{
    /// <summary>크기 이름(settings.dockSize) → 배율.</summary>
    public static double Scale(string? size) => (size ?? "").Trim().ToLowerInvariant() switch { "s" => 0.86, "l" => 1.16, _ => 1.0 };

    /// <summary>n칸의 자리. 오른쪽 가장자리가 x=width, 세로 가운데가 height*centerRatio.</summary>
    public static LauncherPlan Plan(string shape, int n, double scale, double width, double height, bool awake, bool grouped, double centerRatio = 0.45)
    {
        double k = scale <= 0 ? 1 : scale, cy = height * centerRatio, hubSize = 44 * k, hubX = width - hubSize / 2 - 4;
        var hub = new LPoint(hubX, cy);
        switch (DockShapes.Normalize(shape))
        {
            case DockShapes.Fan:
            {
                double s = 40 * k, space = 60 * k, r = 105 * k, maxArc = 160 * Math.PI / 180;
                double arc = n > 1 ? space * (n - 1) / r : 0;
                if (arc > maxArc) { arc = maxArc; r = space * (n - 1) / arc; }
                var pts = Enumerable.Range(0, n).Select(i =>
                {
                    double a = Math.PI - arc / 2 + (n > 1 ? arc * i / (n - 1) : arc / 2);
                    return new LPoint(hubX + r * Math.Cos(a), cy - r * Math.Sin(a));
                }).ToList();
                return new LauncherPlan(hub, hubSize, true, s, pts, "hub", [], false, r + 70 * k);
            }
            case DockShapes.Cluster:
            {
                double s = 40 * k, big = 56 * k, cx = width - big / 2 - 4;
                double[] ring1 = [90, 126, 162, 198, 234, 270], ring2 = [105, 135, 165, 195, 225, 255]; // 가장자리 반대쪽 반원에만
                var pts = new List<LPoint>();
                for (int i = 0; i < n; i++)
                {
                    bool outer = i >= ring1.Length;
                    double a = (outer ? ring2[(i - ring1.Length) % ring2.Length] : ring1[i]) * Math.PI / 180, r = (outer ? 122 : 74) * k;
                    pts.Add(new LPoint(cx + r * Math.Cos(a), cy - r * Math.Sin(a)));
                }
                return new LauncherPlan(new LPoint(cx, cy), big, true, s, pts, "dim", [], false, 180 * k);
            }
            case DockShapes.Rail:
            {
                double s = 36 * k, gap = 44 * k, x = width - s / 2 - 6, top = cy - gap * n / 2;
                var pts = Enumerable.Range(0, n).Select(i => new LPoint(x, top + gap * (i + 1))).ToList();
                return new LauncherPlan(new LPoint(x, top), hubSize * 0.9, true, s, pts, "dim", [], false, 80 * k);
            }
            case DockShapes.Pills:
            {
                double s = 32 * k, gap = 40 * k, x = width - 6, top = cy - gap * (n - 1) / 2;
                var pts = Enumerable.Range(0, n).Select(i => new LPoint(x, top + gap * i)).ToList();
                var dots = Enumerable.Range(0, n).Select(i => new LPoint(width - 10, cy - 16 * (n - 1) / 2.0 + 16 * i)).ToList();
                return new LauncherPlan(hub, hubSize, false, s, pts, "dots", dots, true, 190 * k);
            }
            default: // 초승달
            {
                double gap = (grouped ? 50 : 40) * k, s = (grouped ? 38 : 34) * k, span = gap * Math.Max(0, n - 1), bulge = 40 * k, baseX = hubX - hubSize / 2 - 4 - s / 2;
                var pts = Enumerable.Range(0, n).Select(i =>
                {
                    double dy = -span / 2 + gap * i, t = span > 0 ? dy / (span / 2) : 0;
                    return new LPoint(baseX - bulge * (1 - t * t), cy + dy);
                }).ToList();
                double rg = 11, rs = rg * Math.Max(0, n - 1);
                var dots = Enumerable.Range(0, n).Select(i =>
                {
                    double dy = -rs / 2 + rg * i, t = rs > 0 ? dy / (rs / 2) : 0;
                    return new LPoint(hubX - hubSize / 2 - 8 - 12 * (1 - t * t), cy + dy);
                }).ToList();
                return new LauncherPlan(hub, hubSize, true, s, pts, "dots", dots, false, 120 * k);
            }
        }
    }

    /// <summary>패널 칸이 비워 둘 가장자리 폭(펼친 아이콘이 차지하는 가로 폭 + 여백). 알약 목록은 이름 길이를 몰라 어림값.</summary>
    public static double ReservedWidth(string shape, int n, double scale, bool grouped)
    {
        if (DockShapes.Normalize(shape) == DockShapes.Pills) return 150 * (scale <= 0 ? 1 : scale);
        var p = Plan(shape, Math.Max(1, n), scale, 0, 600, awake: true, grouped);
        double left = p.Items.Count == 0 ? 0 : p.Items.Min(i => i.X - p.ItemSize / 2);
        left = Math.Min(left, p.Hub.X - p.HubSize / 2);
        return Math.Ceiling(-left + 8);
    }

    /// <summary>펼친 묶음의 모듈 줄: 왼쪽 끝(left)에서 왼쪽으로 count개, 세로 y. 간격 = 지름 + 8.</summary>
    public static IReadOnlyList<LPoint> Row(double left, double y, int count, double size) =>
        Enumerable.Range(0, count).Select(j => new LPoint(left - 12 - size / 2 - j * (size + 8), y)).ToList();
}
