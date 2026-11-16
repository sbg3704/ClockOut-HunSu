// 기능: 셀 위 표시층 창. 엑셀 창과 같은 자리·크기의 투명 창에 칸 테두리·옅은 칠·밑줄을 그린다(docs/08). 늘 클릭이 지나가고 활성화되지 않는다.
//       엑셀 창의 소유 창이라 엑셀 위에 있고, 엑셀을 최소화하면 같이 숨는다.
// 사용법: var w = new MarkWindow(); w.SetOwner(excel); w.Draw(excelRect, [(box, brush, "outline"), ...]); w.Clear(); w.Hide()
// 의존: Core/Marks(MarkBox), Native/User32. 자리는 Core/Marks/MarkGeometry가 계산해 준다(이 창은 그리기만).
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Core.Marks;
using ClockOut.Hunsu.Native;

namespace ClockOut.Hunsu.Overlay;

internal sealed class MarkWindow : Window
{
    private readonly Canvas _canvas = new() { IsHitTestVisible = false };
    private IntPtr _owner;
    private PixelRect _rect;

    public MarkWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        Focusable = false;
        IsHitTestVisible = false;
        Title = "훈수꾼 표시층";
        Content = _canvas;
        SourceInitialized += (_, _) =>
        {
            Handle = new WindowInteropHelper(this).Handle;
            long ex = User32.GetWindowLongPtr(Handle, User32.GWL_EXSTYLE).ToInt64() | User32.WS_EX_NOACTIVATE | User32.WS_EX_TOOLWINDOW | User32.WS_EX_TRANSPARENT;
            User32.SetWindowLongPtr(Handle, User32.GWL_EXSTYLE, new IntPtr(ex));
            if (_owner != IntPtr.Zero) User32.SetWindowLongPtr(Handle, User32.GWLP_HWNDPARENT, _owner);
        };
        new WindowInteropHelper(this).EnsureHandle();
    }

    public IntPtr Handle { get; private set; }

    /// <summary>이 창의 화면 배율(실제 픽셀 / DIP).</summary>
    public double Scale => PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;

    public void SetOwner(IntPtr owner)
    {
        _owner = owner;
        if (Handle != IntPtr.Zero) User32.SetWindowLongPtr(Handle, User32.GWLP_HWNDPARENT, owner);
    }

    /// <summary>엑셀 창 자리에 맞추고 표시를 새로 그린다. 같은 자리면 옮기지 않는다(레이어드 창 다시 그리기를 줄임).</summary>
    public void Draw(PixelRect excel, IReadOnlyList<(MarkBox Box, Brush Brush, string Style)> marks)
    {
        _canvas.Children.Clear();
        foreach (var (box, brush, style) in marks) _canvas.Children.Add(Shape(box, brush, style));
        if (marks.Count == 0) { Hide(); return; }
        if (!IsVisible) Show();
        if (excel != _rect || !User32.IsWindowVisible(Handle))
        {
            _rect = excel;
            var after = _owner != IntPtr.Zero ? User32.InsertAfterJustAbove(Handle, _owner) : IntPtr.Zero;
            User32.SetWindowPos(Handle, after, excel.X, excel.Y, excel.Width, excel.Height, User32.SWP_NOACTIVATE | User32.SWP_SHOWWINDOW);
        }
    }

    public void Clear()
    {
        _canvas.Children.Clear();
        Hide();
    }

    private static FrameworkElement Shape(MarkBox b, Brush brush, string style)
    {
        var color = brush is SolidColorBrush s ? s.Color : Colors.OrangeRed;
        FrameworkElement e = style switch
        {
            "fill" => new Rectangle { Width = b.W, Height = b.H, Fill = new SolidColorBrush(Color.FromArgb(0x33, color.R, color.G, color.B)), RadiusX = 2, RadiusY = 2 },
            "underline" => new Rectangle { Width = b.W, Height = 2, Fill = brush },
            _ => new Rectangle { Width = b.W + 2, Height = b.H + 2, Stroke = brush, StrokeThickness = 2, RadiusX = 3, RadiusY = 3 },
        };
        double x = style == "outline" ? b.X - 1 : b.X;
        double y = style switch { "outline" => b.Y - 1, "underline" => b.Y + b.H - 2, _ => b.Y };
        Canvas.SetLeft(e, x);
        Canvas.SetTop(e, y);
        return e;
    }
}
