// 기능: 모듈 관리자 창의 작은 조각: 단추(Chip, 고른 것은 강조색), 켜짐/꺼짐 스위치(Switch), 칸 제목(Caption). [모듈]과 [모양] 두 쪽이 같이 쓴다.
// 사용법: ManagerParts.Chip(this, "왼쪽", selected, () => ...);  ManagerParts.Switch(this, on, () => ...)  (첫 인자는 Theme 이름을 찾을 부품)
// 의존: Ui Theme(Hunsu.Accent, AccentInk, GlassAlt, GlassHover, Line, RowRadius, FontSmall, FontCaption). 값은 Theme 이름으로만 쓴다.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Shell;

internal static class ManagerParts
{
    /// <summary>켜짐/꺼짐 스위치(누르면 act).</summary>
    public static FrameworkElement Switch(FrameworkElement r, bool on, Action act)
    {
        var knob = new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(8),
            Background = (Brush)r.FindResource(on ? "Hunsu.AccentInk" : "Hunsu.Text"),
            HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Margin = new Thickness(3),
        };
        var track = new Border
        {
            Width = 42,
            Height = 22,
            CornerRadius = new CornerRadius(11),
            Background = on ? (Brush)r.FindResource("Hunsu.Accent") : (Brush)r.FindResource("Hunsu.Line"),
            Child = knob,
            Cursor = Cursors.Hand,
            ToolTip = on ? "끄기" : "켜기",
        };
        track.MouseLeftButtonUp += (_, e) => { e.Handled = true; act(); };
        return track;
    }

    /// <summary>작은 단추. selected이면 강조색.</summary>
    public static Border Chip(FrameworkElement r, string text, bool selected, Action act)
    {
        Brush rest = selected ? (Brush)r.FindResource("Hunsu.Accent") : (Brush)r.FindResource("Hunsu.GlassAlt");
        var label = new TextBlock
        {
            Text = text,
            FontSize = (double)r.FindResource("Hunsu.FontSmall"),
            Foreground = (Brush)r.FindResource(selected ? "Hunsu.AccentInk" : ThemeKeys.Text),
            FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal,
        };
        var chip = new Border
        {
            Child = label,
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 0, 6, 0),
            CornerRadius = (CornerRadius)r.FindResource("Hunsu.RowRadius"),
            Background = rest,
            BorderBrush = (Brush)r.FindResource("Hunsu.Line"),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
        };
        Brush hover = (Brush)r.FindResource("Hunsu.GlassHover");
        if (!selected)
        {
            chip.MouseEnter += (_, _) => chip.Background = hover;
            chip.MouseLeave += (_, _) => chip.Background = rest;
        }
        chip.MouseLeftButtonUp += (_, e) => { e.Handled = true; act(); };
        return chip;
    }

    /// <summary>작은 회색 칸 제목(켜짐, 꺼짐, 모양 …).</summary>
    public static TextBlock Caption(FrameworkElement r, string text) => new()
    {
        Text = text,
        Margin = new Thickness(2, 14, 0, 8),
        FontSize = (double)r.FindResource("Hunsu.FontCaption"),
        FontWeight = FontWeights.SemiBold,
        Foreground = (Brush)r.FindResource(ThemeKeys.Muted),
    };
}
