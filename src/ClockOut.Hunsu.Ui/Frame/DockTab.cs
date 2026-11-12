// 기능: 접힌 패널의 손잡이(tab). 폭 28, 세로 글씨 제목 + 배지 하나, 마우스를 올리면 폭 36으로 늘어난다(docs/03 8장).
// 사용법: var t = new DockTab(); t.SetContent("선택", "12", "info"); t.Clicked += ...
// 의존: Theme. 코드로만 만든다(작은 부품이라 XAML 없이).
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Ui.Frame;

public sealed class DockTab : Border
{
    private readonly TextBlock _title = new();
    private readonly Border _badge = new();
    private readonly TextBlock _badgeText = new();

    public DockTab()
    {
        Cursor = Cursors.Hand;
        Loaded += (_, _) => ApplyTheme();
        var stack = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 8) };
        _title.LayoutTransform = new RotateTransform(90);
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        _badge.Child = _badgeText;
        _badge.Margin = new Thickness(0, 6, 0, 0);
        _badge.Padding = new Thickness(4, 0, 4, 0);
        _badge.HorizontalAlignment = HorizontalAlignment.Center;
        _badgeText.HorizontalAlignment = HorizontalAlignment.Center;
        stack.Children.Add(_title);
        stack.Children.Add(_badge);
        Child = stack;
        MouseLeftButtonUp += (_, _) => Clicked?.Invoke();
        MouseEnter += (_, _) => Grow("Hunsu.TabWidthHover");
        MouseLeave += (_, _) => Grow("Hunsu.TabWidth");
    }

    public event Action? Clicked;

    public void SetContent(string title, string badge, string tone)
    {
        _title.Text = title;
        _badgeText.Text = badge;
        _badge.Visibility = string.IsNullOrEmpty(badge) || badge == "-" ? Visibility.Collapsed : Visibility.Visible;
        if (IsLoaded) Tint(tone);
        else Loaded += (_, _) => Tint(tone);
    }

    private void ApplyTheme()
    {
        Width = (double)FindResource("Hunsu.TabWidth");
        CornerRadius = (CornerRadius)FindResource("Hunsu.PanelRadius");
        Background = (Brush)FindResource("Hunsu.Glass");
        BorderBrush = (Brush)FindResource("Hunsu.Line");
        BorderThickness = new Thickness(1);
        Effect = (System.Windows.Media.Effects.Effect)FindResource("Hunsu.Shadow1");
        _title.Foreground = (Brush)FindResource(ThemeKeys.Text);
        _title.FontFamily = (FontFamily)FindResource("Hunsu.FontTitle");
        _title.FontSize = (double)FindResource("Hunsu.FontSmall");
        _badgeText.FontSize = (double)FindResource("Hunsu.FontCaption");
        _badge.CornerRadius = (CornerRadius)FindResource("Hunsu.PillRadius");
    }

    private void Tint(string tone)
    {
        var brush = ThemeKeys.Tone(this, tone);
        _badgeText.Foreground = brush;
        _badge.Background = brush is SolidColorBrush s ? new SolidColorBrush(Color.FromArgb(0x26, s.Color.R, s.Color.G, s.Color.B)) : Brushes.Transparent;
    }

    private void Grow(string key)
    {
        if (!IsLoaded) return;
        BeginAnimation(WidthProperty, new DoubleAnimation((double)FindResource(key), ThemeKeys.Get<Duration>(this, ThemeKeys.Fast)));
    }
}
