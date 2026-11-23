// 기능: 설정 마법사 [모양] 옆의 수식 흐름 미리보기. 가짜 작은 시트(격자, 값, 합계 칸)를 그리고, 실제 색연필 수식 흐름과 같은 계산(Core/Marks/FlowSketch)과
//       같은 그리기(Overlay/PencilPainter)로 "쓰는 칸 → 합계 칸 → 쓰이는 칸" 흐름을 고른 그림 모양(색연필·형광 마커·펜)으로 휙 그려 보인다.
//       고른 패널 룩의 색을 따른다(만들 때 Theme 이름으로 읽음). 엑셀과 상관없는 가짜 칸만 쓴다.
// 사용법: panel.Children.Add(FlowPreview.Build(this, settings.PencilLook));  다시 지으면 다시 그려진다(고를 때마다 움직임).
// 의존: Core/Marks(FlowSketch, SketchCell, MarkBox, SketchTones, SketchLook), Overlay/PencilPainter, Ui Theme(ThemeKeys.Tone).
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ClockOut.Hunsu.Core.Marks;
using ClockOut.Hunsu.Overlay;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Shell;

internal static class FlowPreview
{
    private const double ColW = 62, RowH = 26, HeadW = 26, HeadH = 22;
    private const int Cols = 4, Rows = 7;

    // 가짜 시트: B2:B4 값, D5 = SUM(B2:B4), D7 = D5*1.1 (주소 → 보이는 글)
    private static readonly (string Addr, string Text)[] Cells =
    [
        ("A2", "1월"), ("A3", "2월"), ("A4", "3월"), ("A5", "합계"), ("A7", "부가세 포함"),
        ("B2", "120"), ("B3", "95"), ("B4", "140"),
        ("D5", "355"), ("D7", "390.5"),
    ];

    public static FrameworkElement Build(FrameworkElement r, string pencilLook)
    {
        double w = HeadW + Cols * ColW, h = HeadH + Rows * RowH;
        var sheet = new Canvas { Width = w, Height = h, ClipToBounds = true, Background = Brushes.Transparent };
        var line = (Brush)r.FindResource("Hunsu.Line");
        var ink = (Brush)r.FindResource(ThemeKeys.Text);
        var muted = (Brush)r.FindResource(ThemeKeys.Muted);
        double small = (double)r.FindResource("Hunsu.FontSmall");
        for (int c = 0; c <= Cols; c++) sheet.Children.Add(Line(HeadW + c * ColW, 0, HeadW + c * ColW, h, line));
        for (int row = 0; row <= Rows; row++) sheet.Children.Add(Line(0, HeadH + row * RowH, w, HeadH + row * RowH, line));
        for (int c = 0; c < Cols; c++) sheet.Children.Add(Text(((char)('A' + c)).ToString(), HeadW + c * ColW, 0, ColW, HeadH, muted, small, center: true));
        for (int row = 0; row < Rows; row++) sheet.Children.Add(Text((row + 1).ToString(), 0, HeadH + row * RowH, HeadW, RowH, muted, small, center: true));
        foreach (var (addr, text) in Cells)
        {
            var b = Box(addr);
            sheet.Children.Add(Text(text, b.X + 4, b.Y, b.W - 8, b.H, ink, small, center: false, right: char.IsDigit(text[0])));
        }

        var marks = new Canvas { Width = w, Height = h, IsHitTestVisible = false };
        var plan = FlowSketch.Plan("미리보기", [Cell("D5")], [Cell("B2"), Cell("B3"), Cell("B4")], [Cell("D7")],
            new SketchTones("warn", "info", "good"), SketchLook.ByName(pencilLook));
        var painter = new PencilPainter(marks);
        marks.Loaded += (_, _) => painter.Paint(plan, tone => ThemeKeys.Tone(r, tone), animate: true);

        var stage = new Grid { Width = w, Height = h };
        stage.Children.Add(sheet);
        stage.Children.Add(marks);
        var box = new StackPanel();
        box.Children.Add(new Border
        {
            Child = stage,
            Padding = new Thickness(10),
            CornerRadius = (CornerRadius)r.FindResource("Hunsu.PanelRadius"),
            Background = (Brush)r.FindResource("Hunsu.GlassAlt"),
            BorderBrush = line,
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left,
        });
        box.Children.Add(new TextBlock
        {
            Text = "D5(합계)를 골랐을 때: 파랑은 쓰는 칸, 초록은 이 값을 쓰는 칸",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = w + 20,
            Margin = new Thickness(2, 6, 0, 0),
            FontSize = (double)r.FindResource("Hunsu.FontCaption"),
            Foreground = muted,
        });
        return box;
    }

    /// <summary>"B3" → 미리보기 캔버스 안 칸 자리.</summary>
    private static MarkBox Box(string addr)
    {
        int col = addr[0] - 'A';
        int row = int.Parse(addr[1..], System.Globalization.CultureInfo.InvariantCulture) - 1;
        return new MarkBox(HeadW + col * ColW, HeadH + row * RowH, ColW, RowH);
    }

    private static SketchCell Cell(string addr) => new(addr, Box(addr));

    private static Line Line(double x1, double y1, double x2, double y2, Brush b) => new() { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = b, StrokeThickness = 0.6, SnapsToDevicePixels = true };

    private static TextBlock Text(string text, double x, double y, double w, double h, Brush b, double size, bool center, bool right = false)
    {
        var t = new TextBlock
        {
            Text = text,
            Width = w,
            Height = h,
            FontSize = size,
            Foreground = b,
            TextAlignment = center ? TextAlignment.Center : right ? TextAlignment.Right : TextAlignment.Left,
            Padding = new Thickness(0, (h - size * 1.35) / 2, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Canvas.SetLeft(t, x);
        Canvas.SetTop(t, y);
        return t;
    }
}
