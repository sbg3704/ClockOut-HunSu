// 기능: 색연필 수식 흐름(Core/Marks/FlowSketch의 계획)을 표시층 캔버스에 그린다. 획은 종이 결 무늬를 입힌 붓으로 긋고, 새로 그릴 때만 "휙" 하고 처음에서 끝으로 그려지는 움직임(점선 밀기)을 준다.
//       같은 계획이 그대로면 건드리지 않고, 스크롤로 자리만 바뀌면 움직임 없이 다시 그린다(깜빡이지 않게).
// 사용법: var p = new PencilPainter(canvas); p.Paint(plan, tone => brush, animate: true); p.Clear();
// 의존: Core/Marks(SketchPlan, PencilGrain, PencilStroke); 굵기 강약이 있는 획은 조각으로 나눠 그린다(캘리그라피), 손글씨 글꼴 Assets/Fonts(Nanum Pen Script, OFL 1.1). 글꼴이 없으면 맑은 고딕.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ClockOut.Hunsu.Core.Marks;

namespace ClockOut.Hunsu.Overlay;

internal sealed class PencilPainter
{
    private const int GrainSize = 96;
    private static readonly byte[] GrainAlpha = PencilGrain.Alpha(GrainSize, 20261116);
    private static readonly FontFamily Hand = new(new Uri("pack://application:,,,/"), "./Assets/Fonts/#Nanum Pen Script, Malgun Gothic");

    private readonly Canvas _canvas;
    private readonly Dictionary<Color, Brush> _grain = [];
    private string _key = "";
    private int _sig;

    public PencilPainter(Canvas canvas) => _canvas = canvas;

    public bool HasContent => _canvas.Children.Count > 0;

    public void Clear()
    {
        _canvas.Children.Clear();
        _key = "";
        _sig = 0;
    }

    public void Paint(SketchPlan plan, Func<string, Brush> tone, bool animate)
    {
        if (plan.IsEmpty) { Clear(); return; }
        int sig = Signature(plan);
        if (plan.Key == _key && sig == _sig) return;
        bool fresh = plan.Key != _key;
        _key = plan.Key;
        _sig = sig;
        _canvas.Children.Clear();
        animate &= fresh;
        foreach (var s in plan.Strokes) _canvas.Children.Add(Stroke(s, tone(s.Tone), animate, plan.Grain));
        foreach (var l in plan.Labels) _canvas.Children.Add(Label(l, tone(l.Tone), animate));
    }

    private static int Signature(SketchPlan plan)
    {
        var h = new HashCode();
        foreach (var s in plan.Strokes) { h.Add((int)Math.Round(s.Points[0].X)); h.Add((int)Math.Round(s.Points[0].Y)); h.Add(s.Points.Count); }
        foreach (var l in plan.Labels) { h.Add((int)Math.Round(l.X)); h.Add((int)Math.Round(l.Y)); }
        return h.ToHashCode();
    }

    private const int ChunkPoints = 4;         // 한 조각(굵기가 일정한 짧은 선)에 쓰는 점 수. 조각이 이어져 굵기가 부드럽게 변한다
    private const double EaseExponent = 2.5;   // 움직임 곡선(처음이 빠르고 끝에서 멈칫): progress = 1 - (1 - t)^2.5

    /// <summary>획 하나. 굵기 강약(Pressure)이 있으면 조각으로 나눠 조각마다 굵기를 달리하고(캘리그라피), 없으면 한 줄로 긋는다.
    /// 조각들은 한 묶음(Canvas)에 담겨 묶음 전체에 진하기를 줘서(반투명 마커가 이음매에서 진해지지 않게) 그려진다.</summary>
    private FrameworkElement Stroke(SketchStroke s, Brush toneBrush, bool animate, bool grain)
    {
        var brush = grain ? Grain(toneBrush) : toneBrush;
        if (s.Pressure is not { Count: > 1 } pr || pr.Count != s.Points.Count) return Plain(s, brush, animate);
        var group = new Canvas { Opacity = s.Alpha, IsHitTestVisible = false };
        double total = PencilStroke.Length(s.Points), walked = 0;
        for (int i = 0; i < s.Points.Count - 1; i += ChunkPoints - 1)
        {
            int end = Math.Min(s.Points.Count - 1, i + ChunkPoints - 1);
            var geo = new StreamGeometry();
            double sum = 0, len = 0;
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(s.Points[i].X, s.Points[i].Y), false, false);
                for (int j = i + 1; j <= end; j++)
                {
                    ctx.LineTo(new Point(s.Points[j].X, s.Points[j].Y), true, true);
                    len += PencilStroke.Dist(s.Points[j - 1], s.Points[j]);
                }
                for (int j = i; j <= end; j++) sum += pr[j];
            }
            geo.Freeze();
            var chunk = new Path
            {
                Data = geo, Stroke = brush, StrokeThickness = Math.Max(0.6, s.Width * sum / (end - i + 1)),
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
                IsHitTestVisible = false,
            };
            if (animate && total > 0)
            {
                // 이 조각이 "그려지는 시각": 움직임 곡선을 거꾸로 풀어, 선이 빨리 나가는 처음에는 조각이 드문드문, 끝에서는 촘촘히 나타나게 한다
                double progress = Math.Clamp(walked / total, 0, 1);
                double t = 1 - Math.Pow(1 - progress, 1 / EaseExponent);
                chunk.Opacity = 0;
                var show = new DoubleAnimationUsingKeyFrames { BeginTime = TimeSpan.FromMilliseconds(s.StartMs + s.DurationMs * t) };
                show.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                Timeline.SetDesiredFrameRate(show, 30);
                chunk.BeginAnimation(UIElement.OpacityProperty, show);
            }
            walked += len;
            group.Children.Add(chunk);
        }
        return group;
    }

    /// <summary>굵기가 일정한 한 줄 획(칠하기). 점선 하나가 획보다 길게 잡고 밀어 넣으면 처음에서 끝으로 그려진다(점선 길이는 선 굵기의 배수).</summary>
    private static Path Plain(SketchStroke s, Brush brush, bool animate)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(s.Points[0].X, s.Points[0].Y), false, false);
            ctx.PolyLineTo([.. s.Points.Skip(1).Select(p => new Point(p.X, p.Y))], true, true);
        }
        geo.Freeze();
        var path = new Path
        {
            Data = geo, Stroke = brush, StrokeThickness = s.Width,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
            Opacity = s.Alpha, IsHitTestVisible = false,
        };
        if (!animate) return path;
        double dash = (PencilStroke.Length(s.Points) + 4) / s.Width;
        path.StrokeDashArray = [dash, dash];
        path.StrokeDashOffset = dash;
        var anim = new DoubleAnimation(dash, 0, TimeSpan.FromMilliseconds(s.DurationMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(s.StartMs),
            EasingFunction = new PowerEase { Power = EaseExponent, EasingMode = EasingMode.EaseOut },
        };
        Timeline.SetDesiredFrameRate(anim, 30);
        path.BeginAnimation(Shape.StrokeDashOffsetProperty, anim);
        return path;
    }

    private static FrameworkElement Label(SketchLabel l, Brush toneBrush, bool animate)
    {
        var c = toneBrush is SolidColorBrush sb ? sb.Color : Colors.DimGray;
        var ink = new SolidColorBrush(Color.FromRgb((byte)(c.R * 0.55), (byte)(c.G * 0.55), (byte)(c.B * 0.55)));
        var paper = new SolidColorBrush(Color.FromArgb(0xD8, 0xFF, 0xF7, 0xDA));
        ink.Freeze(); paper.Freeze();
        if (l.Badge) return Badge(l, ink, paper, c, animate);
        var box = new Border
        {
            Background = paper,
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 0, 4, 1),
            IsHitTestVisible = false,
            RenderTransform = new RotateTransform(l.Angle),
            Child = new TextBlock { Text = l.Text, FontFamily = Hand, FontSize = 17, Foreground = ink, IsHitTestVisible = false },
        };
        Canvas.SetLeft(box, l.X);
        Canvas.SetTop(box, l.Y);
        if (!animate) return box;
        box.Opacity = 0;
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(110)) { BeginTime = TimeSpan.FromMilliseconds(l.StartMs) };
        Timeline.SetDesiredFrameRate(fade, 30);
        box.BeginAnimation(UIElement.OpacityProperty, fade);
        return box;
    }

    /// <summary>화살표 한가운데의 작은 번호표: 종이색 동그라미에 톤 색 테두리와 손글씨 숫자.</summary>
    private static FrameworkElement Badge(SketchLabel l, Brush ink, Brush paper, Color c, bool animate)
    {
        var ring = new SolidColorBrush(c);
        ring.Freeze();
        var box = new Border
        {
            Width = 19, Height = 19, CornerRadius = new CornerRadius(10),
            Background = paper, BorderBrush = ring, BorderThickness = new Thickness(1.6),
            IsHitTestVisible = false,
            Child = new TextBlock { Text = l.Text, FontFamily = Hand, FontSize = 15, Foreground = ink, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false },
        };
        Canvas.SetLeft(box, l.X - 9.5);
        Canvas.SetTop(box, l.Y - 9.5);
        if (!animate) return box;
        box.Opacity = 0;
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(90)) { BeginTime = TimeSpan.FromMilliseconds(l.StartMs) };
        Timeline.SetDesiredFrameRate(fade, 30);
        box.BeginAnimation(UIElement.OpacityProperty, fade);
        return box;
    }

    /// <summary>톤 색에 종이 결(투명도 무늬)을 입힌 반복 붓. 같은 색이면 한 번만 만든다.</summary>
    private Brush Grain(Brush tone)
    {
        if (tone is not SolidColorBrush sb) return tone;
        if (_grain.TryGetValue(sb.Color, out var cached)) return cached;
        var c = sb.Color;
        var px = new byte[GrainSize * GrainSize * 4];
        for (int i = 0; i < GrainAlpha.Length; i++)
        {
            int a = GrainAlpha[i];
            px[i * 4] = (byte)(c.B * a / 255);
            px[i * 4 + 1] = (byte)(c.G * a / 255);
            px[i * 4 + 2] = (byte)(c.R * a / 255);
            px[i * 4 + 3] = (byte)a;
        }
        var bmp = BitmapSource.Create(GrainSize, GrainSize, 96, 96, PixelFormats.Pbgra32, null, px, GrainSize * 4);
        bmp.Freeze();
        var brush = new ImageBrush(bmp)
        {
            TileMode = TileMode.Tile,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, GrainSize, GrainSize),
            Stretch = Stretch.Fill,
        };
        brush.Freeze();
        _grain[c] = brush;
        return brush;
    }
}
