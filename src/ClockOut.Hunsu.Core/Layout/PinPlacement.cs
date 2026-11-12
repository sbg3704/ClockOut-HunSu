// 기능: 핀 창(엑셀 밖으로 떼어 낸 창)의 자리 계산. 처음 뗄 때 기본 자리, 저장된 자리가 화면 밖이면 안으로 되돌리기(docs/03 9장).
// 사용법: PinPlacement.Default(panelScreenRect, w, h) ; PinPlacement.KeepOnScreen(saved, workAreas)
// 의존: PixelRect. 순수 계산.
namespace ClockOut.Hunsu.Core.Layout;

public static class PinPlacement
{
    /// <summary>최소 크기(docs/01 5장 window 최소 320×200)를 픽셀로 맞춘다.</summary>
    public static PixelRect AtLeast(PixelRect r, int minW, int minH) => r with { Width = Math.Max(r.Width, minW), Height = Math.Max(r.Height, minH) };

    /// <summary>처음 뗄 때: 패널이 있던 자리에서 왼쪽으로 조금 비켜 띄운다(도크 자리와 겹치지 않게).</summary>
    public static PixelRect Default(PixelRect panelOnScreen, int width, int height, int shift = 24) =>
        new(panelOnScreen.X - width - shift, panelOnScreen.Y, width, height);

    /// <summary>저장된 자리가 어느 작업 영역에도 충분히 걸치지 않으면(모니터를 뗐을 때 등) 가장 가까운 작업 영역 안으로 옮긴다.</summary>
    public static PixelRect KeepOnScreen(PixelRect r, IReadOnlyList<PixelRect> workAreas, int minVisible = 48)
    {
        if (workAreas.Count == 0) return r;
        foreach (var a in workAreas)
        {
            int visW = Math.Min(r.Right, a.Right) - Math.Max(r.X, a.X);
            int visH = Math.Min(r.Bottom, a.Bottom) - Math.Max(r.Y, a.Y);
            if (visW >= minVisible && visH >= minVisible && r.Y >= a.Y) return r; // 제목줄이 화면 위로 숨지 않게 위쪽은 꼭 안에
        }
        var best = workAreas.OrderBy(a => Distance(r, a)).First();
        int x = Math.Clamp(r.X, best.X, Math.Max(best.X, best.Right - r.Width));
        int y = Math.Clamp(r.Y, best.Y, Math.Max(best.Y, best.Bottom - r.Height));
        return r with { X = x, Y = y };
    }

    private static double Distance(PixelRect r, PixelRect a)
    {
        double cx = r.X + r.Width / 2.0, cy = r.Y + r.Height / 2.0;
        double dx = Math.Max(Math.Max(a.X - cx, 0), cx - a.Right);
        double dy = Math.Max(Math.Max(a.Y - cy, 0), cy - a.Bottom);
        return dx * dx + dy * dy;
    }
}
