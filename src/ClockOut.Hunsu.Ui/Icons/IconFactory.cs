// 기능: 아이콘 프로필(세트)과 뜻 이름으로 그림 부품을 만든다(docs/09 3장·3.1장). 선 그림 세트는 선 굵기를 1.5(24 격자 기준)로 맞추고,
//       색은 하나(보통 Hunsu.Text)로 칠한다. 프로필에 없는 이름은 기본 프로필(fluent)로, 그것도 없으면 이름 첫 글자를 쓴다.
// 사용법: FrameworkElement g = IconFactory.Create("fluent", "sum", 20, brush, fallbackText: "선택 요약");
// 의존: Icons/IconData.g.cs(자동 생성, tools/icons/make_icon_data.py). 경로 해석은 한 번만 하고 얼려(Freeze) 다시 쓴다.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ClockOut.Hunsu.Ui.Icons;

/// <summary>그림 하나: 원래 그림 이름, WPF 경로 문법 데이터, 보기 상자, 선 그림인가, 원래 선 굵기.</summary>
internal sealed record IconGlyph(string Name, string Data, double X, double Y, double W, double H, bool Stroke, double StrokeWidth);

public static class IconFactory
{
    public const string DefaultProfile = "fluent";
    private const double LineWidth24 = 1.5; // 24 격자 기준 선 굵기(docs/09 3장)
    private static readonly Dictionary<(IconGlyph, double), Geometry?> Cache = [];

    /// <summary>프로필에 그 뜻 이름 그림이 있는가.</summary>
    public static bool Has(string profile, string key) => Find(profile, key) != null;

    public static FrameworkElement Create(string profile, string? key, double size, Brush brush, string fallbackText = "")
    {
        var glyph = Find(profile, key ?? "") ?? Find(DefaultProfile, key ?? "");
        var geo = glyph == null ? null : GeometryFor(glyph, size);
        if (glyph == null || geo == null) return Letter(fallbackText, size, brush);
        // 보기 상자 → size 크기 변환을 그림 자체에 넣는다(부품 변환으로 하면 배치 단계에서 잘릴 수 있다). 선 굵기는 바뀐 크기 기준.
        var path = new Path { Data = geo, Stretch = Stretch.None, IsHitTestVisible = false };
        if (glyph.Stroke)
        {
            path.Stroke = brush;
            path.StrokeThickness = LineWidth24 * size / 24.0;
            path.StrokeStartLineCap = path.StrokeEndLineCap = PenLineCap.Round;
            path.StrokeLineJoin = PenLineJoin.Round;
        }
        else path.Fill = brush;
        return new Canvas { Width = size, Height = size, Children = { path }, IsHitTestVisible = false };
    }

    private static IconGlyph? Find(string profile, string key) =>
        IconData.Profiles.TryGetValue(profile, out var map) && map.TryGetValue(key, out var g) ? g : null;

    private static Geometry? GeometryFor(IconGlyph g, double size)
    {
        var k = (g, size);
        if (Cache.TryGetValue(k, out var hit)) return hit;
        Geometry? geo;
        try
        {
            geo = Geometry.Parse(g.Data).Clone();
            double scale = size / Math.Max(g.W, g.H);
            geo.Transform = new MatrixTransform(scale, 0, 0, scale, -g.X * scale, -g.Y * scale);
            geo.Freeze();
        }
        catch (FormatException)
        {
            geo = null; // 생성 도구가 못 바꾼 경로: 글자로 대신 그린다
        }
        Cache[k] = geo;
        return geo;
    }

    private static TextBlock Letter(string text, double size, Brush brush) => new()
    {
        Text = text.Length > 0 ? text[..1] : "?",
        Foreground = brush,
        FontSize = size * 0.8,
        FontWeight = FontWeights.SemiBold,
        Width = size,
        Height = size,
        TextAlignment = TextAlignment.Center,
        LineHeight = size,
        LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
        IsHitTestVisible = false,
    };
}
