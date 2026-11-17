// 기능: 떼어 낸 패널(핀 창)들이 자석으로 붙어 있는지 보고, 붙은 것끼리 한 묶음으로 찾는다. 하나를 끌면 붙은 것이 모두 같이 움직이게 하는 데 쓴다. 화면 없이 시험된다.
// 사용법: var ids = PinGroups.Connected(rects, start);  → start와 (변이 맞닿아 이어진) 모든 창의 번호. 맞닿음 = 변이 tolerance 픽셀 안이고 겹치는 길이가 minOverlap 이상.
// 의존: Core/PixelRect. 붙인 창은 PinPlacement.Snap이 변을 정확히 맞추므로 맞닿음으로 본다. Alt를 누르고 끌면 묶음 없이 혼자 움직인다(PinWindow).
namespace ClockOut.Hunsu.Core.Layout;

public static class PinGroups
{
    public static bool Touching(PixelRect a, PixelRect b, int tolerance = 3, int minOverlap = 12)
    {
        int overlapY = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y);
        int overlapX = Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X);
        bool sideBySide = overlapY >= minOverlap && (Math.Abs(a.Right - b.X) <= tolerance || Math.Abs(b.Right - a.X) <= tolerance);
        bool stacked = overlapX >= minOverlap && (Math.Abs(a.Bottom - b.Y) <= tolerance || Math.Abs(b.Bottom - a.Y) <= tolerance);
        return sideBySide || stacked;
    }

    /// <summary>start번 창과 이어진 모든 창 번호(start 포함, 오름차순).</summary>
    public static IReadOnlyList<int> Connected(IReadOnlyList<PixelRect> rects, int start, int tolerance = 3, int minOverlap = 12)
    {
        if (start < 0 || start >= rects.Count) return [];
        var seen = new HashSet<int> { start };
        var queue = new Queue<int>([start]);
        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            for (int j = 0; j < rects.Count; j++)
                if (!seen.Contains(j) && Touching(rects[i], rects[j], tolerance, minOverlap)) { seen.Add(j); queue.Enqueue(j); }
        }
        return [.. seen.Order()];
    }
}
