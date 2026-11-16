// 기능: 색연필 결(종이의 올)을 흉내 내는 투명도 무늬를 만든다. 타일 하나(size×size)의 투명도 값 0~255를 돌려준다. 이어 붙여도 이음매가 안 보이게 가장자리를 감싼다.
// 사용법: byte[] alpha = PencilGrain.Alpha(96, seed);  앱이 색을 입혀 반복 붓(ImageBrush)으로 쓴다.
// 의존: Core/Marks(HandRng). 화면 없이 시험된다.
namespace ClockOut.Hunsu.Core.Marks;

public static class PencilGrain
{
    public const int MinAlpha = 120;

    public static byte[] Alpha(int size, ulong seed)
    {
        if (size < 8) size = 8;
        var rng = new HandRng(seed);
        // 거친 알갱이(4칸마다 한 점)와 고운 알갱이를 섞는다. 점 사이는 쌍선형 보간, 가장자리는 감싸서 이어 붙여도 매끈하다.
        int coarse = Math.Max(2, size / 4);
        var grid = new double[coarse * coarse];
        for (int i = 0; i < grid.Length; i++) grid[i] = rng.NextDouble();
        var result = new byte[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                double gx = x * coarse / (double)size, gy = y * coarse / (double)size;
                int x0 = (int)gx, y0 = (int)gy;
                double fx = gx - x0, fy = gy - y0;
                double v00 = grid[(y0 % coarse) * coarse + x0 % coarse], v10 = grid[(y0 % coarse) * coarse + (x0 + 1) % coarse];
                double v01 = grid[((y0 + 1) % coarse) * coarse + x0 % coarse], v11 = grid[((y0 + 1) % coarse) * coarse + (x0 + 1) % coarse];
                double smooth = (v00 * (1 - fx) + v10 * fx) * (1 - fy) + (v01 * (1 - fx) + v11 * fx) * fy;
                double fine = rng.NextDouble();
                double n = 0.45 * smooth + 0.55 * fine;
                // 올이 센 곳은 진하고, 약한 곳은 종이가 비친다
                result[y * size + x] = (byte)(MinAlpha + (255 - MinAlpha) * Math.Clamp((n - 0.2) / 0.6, 0, 1));
            }
        }
        return result;
    }
}
