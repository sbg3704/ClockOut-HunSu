// 기능: 가장자리 도크에 놓인 모듈들의 순서와 "넘칠 때 접기"를 계산한다(docs/01 6.2, 6.4장).
// 사용법: var plan = DockLayout.Plan(entries, availableHeight, gap); plan[i].Collapsed → 손잡이(tab)로 그림
//   entries는 order 순으로 정렬해 준다. 펼친 높이 합이 availableHeight를 넘으면 order가 큰 것부터 접는다.
// 의존: 없음(순수 계산). 높이는 화면이 잰 값(논리 px)을 넣는다.
namespace ClockOut.Hunsu.Core.Layout;

/// <summary>도크 한 칸: 모듈 id, 순서, 사용자가 접었는지, 펼쳤을 때와 접었을 때 높이.</summary>
public sealed record DockEntry(string Id, int Order, bool CollapsedByUser, double PanelHeight, double TabHeight);

/// <summary>계산 결과 한 칸: 접힘 여부(사용자 또는 공간 부족), 위쪽 위치, 높이.</summary>
public sealed record DockSlot(string Id, bool Collapsed, bool ForcedByOverflow, double Top, double Height);

public static class DockLayout
{
    public static IReadOnlyList<DockSlot> Plan(IEnumerable<DockEntry> entries, double availableHeight, double gap)
    {
        var sorted = entries.OrderBy(e => e.Order).ThenBy(e => e.Id, StringComparer.Ordinal).ToList();
        var collapsed = sorted.Select(e => e.CollapsedByUser).ToArray();
        var forced = new bool[sorted.Count];
        double Total() => sorted.Select((e, i) => collapsed[i] ? e.TabHeight : e.PanelHeight).Sum() + gap * Math.Max(0, sorted.Count - 1);
        // 넘치면 order가 큰(뒤쪽) 펼친 패널부터 접는다.
        for (int i = sorted.Count - 1; i >= 0 && Total() > availableHeight; i--)
        {
            if (collapsed[i]) continue;
            collapsed[i] = true;
            forced[i] = true;
        }
        var result = new List<DockSlot>(sorted.Count);
        double top = 0;
        for (int i = 0; i < sorted.Count; i++)
        {
            double h = collapsed[i] ? sorted[i].TabHeight : sorted[i].PanelHeight;
            result.Add(new DockSlot(sorted[i].Id, collapsed[i], forced[i], top, h));
            top += h + gap;
        }
        return result;
    }
}

/// <summary>패널 투명도 단계. 평소(마우스가 없을 때) 불투명도이고, 마우스를 올리면 늘 1.0이다(docs/03 3장: 읽을 때는 선명하게).</summary>
public static class OpacityLevels
{
    public static readonly double[] Rest = [1.0, 0.8, 0.55];
    public static readonly string[] Names = ["선명", "반투명", "흐림"];

    public static int Next(int level) => (Math.Clamp(level, 0, Rest.Length - 1) + 1) % Rest.Length;

    public static double RestOpacity(int level) => Rest[Math.Clamp(level, 0, Rest.Length - 1)];
}
