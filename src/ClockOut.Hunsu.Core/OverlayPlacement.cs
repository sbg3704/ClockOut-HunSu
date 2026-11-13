// 기능: 따라갈 창(엑셀)의 사각형을 받아 오버레이 창을 놓을 자리를 계산한다. 0단계는 "창 안쪽 오른쪽 위" 한 자리.
// 사용법: PixelRect r = OverlayPlacement.TopRightInside(excelRect, overlayWidth, overlayHeight, margin)
// 의존: PixelRect. 순수 계산이라 앱(WindowFollower)과 측정 도구(bench)가 같은 답을 낸다.
namespace ClockOut.Hunsu.Core;

public static class OverlayPlacement
{
    /// <summary>기본 여백(픽셀). 엑셀 제목줄과 리본을 덜 가리도록 위쪽은 더 띄운다.</summary>
    public const int MarginRight = 24;
    public const int MarginTop = 160;

    public static PixelRect TopRightInside(PixelRect target, int overlayWidth, int overlayHeight, int marginRight = MarginRight, int marginTop = MarginTop)
    {
        int w = Math.Max(1, overlayWidth);
        int h = Math.Max(1, overlayHeight);
        int x = target.Right - marginRight - w;
        int y = target.Y + marginTop;
        // 창이 오버레이보다 작으면 창 왼쪽, 위쪽 끝에 맞춘다(창 밖으로 나가지 않게).
        if (x < target.X) x = target.X;
        if (y + h > target.Bottom) y = Math.Max(target.Y, target.Bottom - h);
        return new PixelRect(x, y, w, h);
    }

    /// <summary>왼쪽 도크: 창 안쪽 왼쪽 위.</summary>
    public static PixelRect TopLeftInside(PixelRect target, int overlayWidth, int overlayHeight, int marginLeft = MarginRight, int marginTop = MarginTop)
    {
        var r = TopRightInside(target, overlayWidth, overlayHeight, marginLeft, marginTop);
        int x = target.X + marginLeft;
        if (x + r.Width > target.Right) x = Math.Max(target.X, target.Right - r.Width);
        return r with { X = x };
    }

    /// <summary>도크 창 높이: 엑셀 창 높이의 80%(docs/03 8장)에서 위 여백을 뺀 만큼, 최소 minHeight.</summary>
    public static int DockHeight(PixelRect target, int minHeight, int marginTop = MarginTop) =>
        Math.Max(minHeight, Math.Min((int)(target.Height * 0.8), target.Height - marginTop - 16));

    /// <summary>오버레이를 숨겨야 하는가(최소화, 숨김, 크기 없음).</summary>
    public static bool ShouldHide(bool targetVisible, bool targetMinimized, PixelRect target) =>
        !targetVisible || targetMinimized || target.IsEmpty;
}
