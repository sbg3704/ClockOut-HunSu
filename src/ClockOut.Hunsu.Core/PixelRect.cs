// 기능: 화면 픽셀 단위 사각형(왼쪽, 위, 너비, 높이). 창 따라가기 계산에 쓴다.
// 사용법: new PixelRect(x, y, w, h), r.Right, r.Contains(px, py)
// 의존: 없음(순수 값).
namespace ClockOut.Hunsu.Core;

public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    [System.Text.Json.Serialization.JsonIgnore] public int Right => X + Width;
    [System.Text.Json.Serialization.JsonIgnore] public int Bottom => Y + Height;
    [System.Text.Json.Serialization.JsonIgnore] public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool Contains(int px, int py) => px >= X && px < Right && py >= Y && py < Bottom;

    public static PixelRect FromLtrb(int left, int top, int right, int bottom) => new(left, top, right - left, bottom - top);

    public override string ToString() => $"{X},{Y},{Width},{Height}";

    public static bool TryParse(string? text, out PixelRect rect)
    {
        rect = default;
        var parts = (text ?? "").Split(',');
        if (parts.Length != 4) return false;
        var v = new int[4];
        for (int i = 0; i < 4; i++)
        {
            if (!int.TryParse(parts[i], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out v[i])) return false;
        }
        rect = new PixelRect(v[0], v[1], v[2], v[3]);
        return true;
    }
}
