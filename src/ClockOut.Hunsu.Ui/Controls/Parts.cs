// 기능: 부품 여섯 가지(stat 큰 숫자, text, badge, divider, list 목록, button 단추)와 "준비 중" 자리표시. 크기와 색은 Theme 이름으로만 쓴다(docs/03 7장).
//       list·button 코드는 ListParts.cs(같은 클래스의 일부).
// 사용법: var (element, fill) = Parts.Create(viewItem, onCommand); fill(scope) 로 값 채우기. onCommand(명령 이름, 인자 JSON)은 단추·줄을 눌렀을 때 불린다.
// 의존: Theme, Core/View(Template). 부품은 자기 모양만 그리고 데이터를 직접 구하지 않는다(docs/01 3.4장 4번).
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ClockOut.Hunsu.Core.View;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Ui.Controls;

public static partial class Parts
{
    public static (FrameworkElement Element, Action<DataScope> Fill) Create(ViewItem item, Action<string, string>? onCommand = null) => item.Type switch
    {
        "list" => ListPart(item, onCommand),
        "button" => ButtonPart(item, onCommand),
        "stat" => Stat(item),
        "text" => Text(item),
        "badge" => Badge(item),
        "divider" => (Divider(), _ => { }),
        _ => (Placeholder($"'{item.Type}' 부품은 준비 중"), _ => { }),
    };

    private static (FrameworkElement, Action<DataScope>) Stat(ViewItem item)
    {
        var value = new TextBlock { FontWeight = FontWeights.Bold, Text = "-" };
        var label = new TextBlock { Margin = new Thickness(0, 0, 0, 2) };
        var box = new StackPanel { Margin = new Thickness(0, 4, 0, 6) };
        box.Children.Add(label);
        box.Children.Add(value);
        box.Loaded += (_, _) =>
        {
            value.FontFamily = (FontFamily)box.FindResource("Hunsu.FontNumber");
            value.FontSize = (double)box.FindResource("Hunsu.FontStat");
            label.FontSize = (double)box.FindResource("Hunsu.FontSmall");
            label.Foreground = (Brush)box.FindResource(ThemeKeys.Muted);
            System.Windows.Documents.Typography.SetNumeralAlignment(value, FontNumeralAlignment.Tabular);
        };
        return (box, scope =>
        {
            string next = Template.Render(item.Get("value"), scope);
            label.Text = Template.Render(item.Get("label"), scope);
            value.Foreground = ThemeKeys.Tone(box, Template.Render(item.Get("tone"), scope));
            if (next != value.Text) Changed(value, next);
        });
    }

    /// <summary>값이 바뀌면 살짝 올라오며 나타난다(fast 120ms, 투명도+이동만).</summary>
    private static void Changed(TextBlock t, string text)
    {
        t.Text = text;
        if (!t.IsLoaded) return;
        var dur = ThemeKeys.Get<Duration>(t, ThemeKeys.Fast);
        var shift = new TranslateTransform(0, 4);
        t.RenderTransform = shift;
        shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, dur) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        t.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.4, 1, dur));
    }

    private static (FrameworkElement, Action<DataScope>) Text(ViewItem item)
    {
        var t = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) };
        t.Loaded += (_, _) => t.FontSize = (double)t.FindResource("Hunsu.FontSmall");
        return (t, scope =>
        {
            t.Text = Template.Render(item.Get("text"), scope);
            string tone = Template.Render(item.Get("tone"), scope);
            t.Foreground = tone.Length == 0 ? (Brush)t.FindResource(ThemeKeys.Text) : ThemeKeys.Tone(t, tone);
        });
    }

    private static (FrameworkElement, Action<DataScope>) Badge(ViewItem item)
    {
        var text = new TextBlock { Margin = new Thickness(8, 1, 8, 1) };
        var b = new Border { Child = text, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 4), MinHeight = 18 };
        b.Loaded += (_, _) =>
        {
            b.CornerRadius = (CornerRadius)b.FindResource("Hunsu.PillRadius");
            text.FontSize = (double)b.FindResource("Hunsu.FontCaption");
        };
        return (b, scope =>
        {
            text.Text = Template.Render(item.Get("text"), scope);
            var brush = ThemeKeys.Tone(b, Template.Render(item.Get("tone", "info"), scope));
            text.Foreground = brush;
            b.Background = brush is SolidColorBrush s ? new SolidColorBrush(Color.FromArgb(0x26, s.Color.R, s.Color.G, s.Color.B)) : Brushes.Transparent; // 톤 색 15%(docs/03 7장)
        });
    }

    private static FrameworkElement Divider()
    {
        var d = new Border { Height = 1, Margin = new Thickness(0, 6, 0, 6) };
        d.Loaded += (_, _) => d.Background = (Brush)d.FindResource("Hunsu.Line");
        return d;
    }

    public static FrameworkElement Placeholder(string text)
    {
        var t = new TextBlock { Text = text, Margin = new Thickness(0, 4, 0, 4) };
        t.Loaded += (_, _) => t.Foreground = (Brush)t.FindResource(ThemeKeys.Muted);
        return t;
    }
}
