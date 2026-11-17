// 기능: 손그림 장식을 얹는 투명 겹. Panel 모드는 패널 틀(테두리, 제목줄 색연필 빗금·형광펜 칠·분필 밑줄, 종이 결), Ring·Box 모드는 작은 요소(둥근 아이콘·훈 원 / 알약·빠른 띠 카드)의 외곽선. 룩(skin)이 plain이면 아무것도 그리지 않는다.
//       룩을 바꾸면(ThemePicker.SkinChanged) 껐다 켜지 않고 바로 다시 그린다.
//       획 계산은 Core/Marks/SkinSketch(시험됨)가 하고, 여기서는 룩의 테마 이름(Hunsu.Skin.*)으로 색과 굵기를 입혀 그리기만 한다. 크기가 바뀔 때만 다시 그린다(패널 카드가 비트맵 캐시라 움직임 중 부담 없음).
// 사용법: <local:HandFrame TitleHeight="26" /> 를 패널 카드 안, 내용 위에 놓는다. TitleTextWidth·Seed(제목 글)는 PanelFrame이 정한다. 작은 요소는 new HandFrame { Mode = HandMode.Ring, Seed = 이름 }. 클릭은 통과(IsHitTestVisible=False).
// 의존: Core/Marks(SkinSketch, PencilGrain), Core/Settings(SkinKind), Theme(Skin*.xaml의 Hunsu.Skin.Kind·Frame·Swash·FrameWidth).
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClockOut.Hunsu.Core.Marks;
using ClockOut.Hunsu.Core.Settings;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Ui.Frame;

public enum HandMode { Panel, Ring, Box }

public sealed class HandFrame : FrameworkElement
{
    private const int GrainSize = 96;

    public static readonly DependencyProperty TitleHeightProperty =
        DependencyProperty.Register(nameof(TitleHeight), typeof(double), typeof(HandFrame), new FrameworkPropertyMetadata(26.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double TitleHeight { get => (double)GetValue(TitleHeightProperty); set => SetValue(TitleHeightProperty, value); }

    public HandFrame()
    {
        IsHitTestVisible = false;
        Loaded += (_, _) => { ThemePicker.SkinChanged += OnSkinChanged; InvalidateVisual(); };
        Unloaded += (_, _) => ThemePicker.SkinChanged -= OnSkinChanged;
    }

    private void OnSkinChanged() => Dispatcher.BeginInvoke(InvalidateVisual, System.Windows.Threading.DispatcherPriority.Render);

    /// <summary>Panel: 패널 틀 장식. Ring: 둥근 요소 외곽선(손으로 두른 고리). Box: 알약·카드 외곽선(손으로 그은 네모).</summary>
    public HandMode Mode { get; set; } = HandMode.Panel;

    private double _titleTextWidth;
    private string _seed = "";

    public double TitleTextWidth
    {
        get => _titleTextWidth;
        set { _titleTextWidth = value; InvalidateVisual(); }
    }

    /// <summary>패널마다 손맛이 조금씩 다르게 하는 씨앗(보통 제목 글). 같으면 늘 같은 모양.</summary>
    public string Seed
    {
        get => _seed;
        set { _seed = value ?? ""; InvalidateVisual(); }
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var kind = Kind();
        if (kind == SkinKind.None || ActualWidth < 8 || ActualHeight < 8) return;
        var frame = TryFindResource("Hunsu.Skin.Frame") as Brush ?? Brushes.Gray;
        var swash = TryFindResource("Hunsu.Skin.Swash") as Brush ?? TryFindResource("Hunsu.Accent") as Brush ?? Brushes.Gold;
        double w = TryFindResource("Hunsu.Skin.FrameWidth") is double fw ? fw : 1.6;
        if (kind == SkinKind.Pencil && Mode == HandMode.Panel) PaintGrain(dc, frame);
        ulong seed = HandRng.Seed("hunsu-skin|" + _seed);
        var plan = Mode == HandMode.Panel
            ? SkinSketch.Plan(ActualWidth, ActualHeight, TitleHeight, TitleTextWidth, kind, seed)
            : SkinSketch.Outline(ActualWidth, ActualHeight, Mode == HandMode.Ring, kind, seed);
        foreach (var s in plan)
        {
            var (brush, thick, alpha) = s.Role switch
            {
                SkinRole.Frame => (frame, w, 0.92),
                SkinRole.FrameEcho => (frame, w * 0.6, kind == SkinKind.Chalk ? 0.5 : 0.38),
                SkinRole.Hatch => (swash, 1.5, 0.78),
                SkinRole.Swash => (swash, 9.0, 0.62),
                _ => (kind == SkinKind.Chalk ? swash : frame, kind == SkinKind.Chalk ? 2.2 : w * 0.9, 0.85),
            };
            dc.PushOpacity(alpha);
            dc.DrawGeometry(null, new Pen(brush, thick) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }, Poly(s.Points));
            dc.Pop();
        }
    }

    private SkinKind Kind() =>
        TryFindResource("Hunsu.Skin.Kind") is string k && Enum.TryParse<SkinKind>(k, true, out var kind) ? kind : SkinKind.None;

    private static StreamGeometry Poly(IReadOnlyList<PointD> pts)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(pts[0].X, pts[0].Y), false, false);
            ctx.PolyLineTo([.. pts.Skip(1).Select(p => new Point(p.X, p.Y))], true, true);
        }
        g.Freeze();
        return g;
    }

    private static readonly Dictionary<Color, ImageBrush> GrainBrushes = [];

    /// <summary>종이 결: 연필 선 색을 아주 옅게 반복해 깐다(색연필 룩만).</summary>
    private void PaintGrain(DrawingContext dc, Brush frame)
    {
        var color = (frame as SolidColorBrush)?.Color ?? Colors.Gray;
        if (!GrainBrushes.TryGetValue(color, out var brush))
        {
            var alpha = PencilGrain.Alpha(GrainSize, 20261117);
            var px = new byte[GrainSize * GrainSize * 4];
            for (int i = 0; i < alpha.Length; i++)
            {
                double a = alpha[i] / 255.0 * 0.10; // 아주 옅게
                px[i * 4] = (byte)(color.B * a); px[i * 4 + 1] = (byte)(color.G * a); px[i * 4 + 2] = (byte)(color.R * a); px[i * 4 + 3] = (byte)(255 * a);
            }
            var bmp = BitmapSource.Create(GrainSize, GrainSize, 96, 96, PixelFormats.Pbgra32, null, px, GrainSize * 4);
            bmp.Freeze();
            brush = new ImageBrush(bmp) { TileMode = TileMode.Tile, Stretch = Stretch.None, ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, GrainSize, GrainSize) };
            brush.Freeze();
            GrainBrushes[color] = brush;
        }
        dc.DrawRectangle(brush, null, new Rect(0, 0, ActualWidth, ActualHeight));
    }
}
