// 기능: 다리 조회 mark가 준 칸 자리(화면 픽셀)를 표시층 창 안의 자리(DIP)로 바꾸고, 엑셀 격자(panes) 밖으로 나간 부분은 자른다. 화면 없이 시험된다.
// 사용법: var boxes = MarkGeometry.Layout(cells, panes, windowOrigin: new PixelRect(...), scale: 1.25);
//         cells = MarkGeometry.Read(dataElement, "errors") 처럼 JSON 배열에서 읽는다.
// 의존: Core/PixelRect, System.Text.Json. 그리기(WPF)는 앱의 Overlay/MarkWindow가 맡는다.
using System.Text.Json;

namespace ClockOut.Hunsu.Core.Marks;

/// <summary>창 안 자리(DIP).</summary>
public readonly record struct MarkBox(double X, double Y, double W, double H);

public static class MarkGeometry
{
    /// <summary>{"x":..,"y":..,"w":..,"h":..} 배열을 읽는다. 크기가 0 이하이거나 숫자가 아닌 줄은 건너뛴다.</summary>
    public static IReadOnlyList<PixelRect> Read(JsonElement array)
    {
        var list = new List<PixelRect>();
        if (array.ValueKind != JsonValueKind.Array) return list;
        foreach (var e in array.EnumerateArray())
        {
            if (e.ValueKind != JsonValueKind.Object) continue;
            if (Int(e, "x") is not int x || Int(e, "y") is not int y || Int(e, "w") is not int w || Int(e, "h") is not int h) continue;
            if (w <= 0 || h <= 0) continue;
            list.Add(new PixelRect(x, y, w, h));
        }
        return list;
    }

    /// <summary>화면 픽셀 → 창 안 DIP. panes가 있으면 그 중 하나와 겹치는 부분만 남긴다(머리글·스크롤 막대 위에 그리지 않게).</summary>
    public static IReadOnlyList<MarkBox> Layout(IEnumerable<PixelRect> cells, IReadOnlyList<PixelRect> panes, PixelRect window, double scale)
    {
        if (scale <= 0) scale = 1;
        var result = new List<MarkBox>();
        foreach (var c in cells)
        {
            var clipped = panes.Count == 0 ? (PixelRect?)c : Clip(c, panes);
            if (clipped is not { } r) continue;
            var inWin = Intersect(r, window);
            if (inWin is not { } v) continue;
            result.Add(new MarkBox((v.X - window.X) / scale, (v.Y - window.Y) / scale, v.Width / scale, v.Height / scale));
        }
        return result;
    }

    private static PixelRect? Clip(PixelRect c, IReadOnlyList<PixelRect> panes)
    {
        PixelRect? best = null;
        long bestArea = 0;
        foreach (var p in panes)
        {
            if (Intersect(c, p) is not { } i) continue;
            long area = (long)i.Width * i.Height;
            if (area > bestArea) { best = i; bestArea = area; }
        }
        return best;
    }

    public static PixelRect? Intersect(PixelRect a, PixelRect b)
    {
        int l = Math.Max(a.X, b.X), t = Math.Max(a.Y, b.Y);
        int r = Math.Min(a.X + a.Width, b.X + b.Width), bt = Math.Min(a.Y + a.Height, b.Y + b.Height);
        return r > l && bt > t ? new PixelRect(l, t, r - l, bt - t) : null;
    }

    private static int? Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d) && double.IsFinite(d) ? (int)Math.Round(d) : null;
}
