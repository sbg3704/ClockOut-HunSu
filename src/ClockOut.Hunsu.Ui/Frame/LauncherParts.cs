// 기능: 런처(LauncherView)가 쓰는 작은 그림 조각 만들기. 훈 원, 원형 아이콘(배지·열림 고리 포함), 알약, 잠든 점, 이름 알약, 위쪽 이름표, 묶음 이름표.
// 사용법: LauncherView 안에서만 부른다. var c = LauncherParts.Icon(scope, profile, icon, name, size, badge, tone, open);
// 의존: Theme(색·글꼴 이름), Icons/IconFactory. 값(색, 시간)은 직접 쓰지 않고 Theme에서 찾는다.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClockOut.Hunsu.Ui.Icons;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Ui.Frame;

internal static class LauncherParts
{
    public static Brush B(FrameworkElement scope, string key) => scope.TryFindResource(key) as Brush ?? Brushes.Gray;

    /// <summary>훈 원: 지름 size, 가운데 "훈".</summary>
    public static Border Hub(FrameworkElement scope, double size)
    {
        var b = new Border
        {
            Width = size, Height = size, CornerRadius = new CornerRadius(size / 2),
            // 흰 시트 위에서도 눈에 띄게: 강조색으로 꽉 채우고 진한 테두리·큰 그림자(2026-11-17 "너무 흰색이라 눈에 안 띔").
            Background = B(scope, ThemeKeys.Accent), BorderBrush = B(scope, ThemeKeys.Text), BorderThickness = new Thickness(2),
            Cursor = System.Windows.Input.Cursors.Hand,
            Effect = scope.TryFindResource("Hunsu.Shadow2") as System.Windows.Media.Effects.Effect,
        };
        var glyph = new TextBlock
        {
            Text = "훈", FontSize = size * 0.42, FontWeight = FontWeights.Bold, Foreground = B(scope, "Hunsu.AccentInk"),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        glyph.SetResourceReference(TextBlock.FontFamilyProperty, "Hunsu.FontTitle"); // 룩을 바꾸면 따라간다
        b.Child = new Grid { Children = { glyph, new HandFrame { Mode = HandMode.Ring, Seed = "hub" } } };
        return b;
    }

    /// <summary>원형 아이콘 칸(Canvas, 크기 size). 원·그림·배지·열림 고리. 원(Border)은 Tag로 돌려준다(올림 색 바꾸기).</summary>
    public static Canvas Icon(FrameworkElement scope, string profile, string icon, string name, double size, string badge, string tone, bool open, bool group, bool working = false)
    {
        var c = new Canvas { Width = size, Height = size, Cursor = System.Windows.Input.Cursors.Hand };
        if (open)
        {
            var ring = new Border { Width = size + 8, Height = size + 8, CornerRadius = new CornerRadius(size / 2 + 4), BorderBrush = B(scope, ThemeKeys.Accent), BorderThickness = new Thickness(2), IsHitTestVisible = false };
            Canvas.SetLeft(ring, -4); Canvas.SetTop(ring, -4);
            c.Children.Add(ring);
        }
        var circle = new Border
        {
            Width = size, Height = size, CornerRadius = new CornerRadius(size / 2),
            Background = B(scope, "Hunsu.Glass"), BorderBrush = B(scope, ThemeKeys.Text), BorderThickness = new Thickness(group ? 2 : 1.5),
            Effect = scope.TryFindResource("Hunsu.Shadow1") as System.Windows.Media.Effects.Effect,
        };
        double g = Math.Round(size * 0.52);
        var glyph = IconFactory.Create(profile, icon, g, B(scope, ThemeKeys.Text), name);
        Canvas.SetLeft(glyph, (size - g) / 2); Canvas.SetTop(glyph, (size - g) / 2);
        c.Children.Add(circle);
        c.Children.Add(new HandFrame { Mode = HandMode.Ring, Seed = name, Width = size, Height = size }); // 손그림 룩의 외곽선
        c.Children.Add(glyph);
        if (Shown(badge))
        {
            var bd = Badge(scope, badge, tone, Math.Max(15, size * 0.42));
            bd.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(bd, size - bd.DesiredSize.Width * 0.6); Canvas.SetTop(bd, -4);
            c.Children.Add(bd);
        }
        if (working && !open)
        {
            var dot = ActivityDot.Create(scope, Math.Max(6, size * 0.2));
            Canvas.SetLeft(dot, size - dot.Width - 1); Canvas.SetTop(dot, size - dot.Height - 1);
            c.Children.Add(dot);
        }
        c.Tag = circle;
        return c;
    }

    /// <summary>알약(알약 목록 모양): 그림 + 이름 + 숫자 + 묶음이면 ‹.</summary>
    public static Border Pill(FrameworkElement scope, string profile, string icon, string name, double height, string badge, string tone, bool open, bool group)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        double g = Math.Round(height * 0.5);
        var glyph = IconFactory.Create(profile, icon, g, B(scope, ThemeKeys.Accent), name);
        glyph.Margin = new Thickness(0, 0, 8, 0);
        row.Children.Add(glyph);
        row.Children.Add(Text(scope, name, ThemeKeys.Text));
        if (Shown(badge)) row.Children.Add(new TextBlock { Text = " " + badge, FontWeight = FontWeights.SemiBold, Foreground = ThemeKeys.Tone(scope, tone), VerticalAlignment = VerticalAlignment.Center, FontSize = Small(scope) });
        if (group) row.Children.Add(Text(scope, "  ‹", ThemeKeys.Muted));
        return new Border
        {
            Height = height, CornerRadius = new CornerRadius(height / 2), Padding = new Thickness(height * 0.28, 0, 12, 0),
            Background = B(scope, "Hunsu.Glass"), BorderBrush = open ? B(scope, ThemeKeys.Accent) : B(scope, ThemeKeys.Text), BorderThickness = new Thickness(open ? 2.5 : 1.5),
            Effect = scope.TryFindResource("Hunsu.Shadow1") as System.Windows.Media.Effects.Effect,
            Cursor = System.Windows.Input.Cursors.Hand, Child = new Grid { Children = { row, new HandFrame { Mode = HandMode.Box, Seed = name } } },
        };
    }

    /// <summary>이름 알약(아이콘 뒤에서 옆으로 늘어나는 것). 끝 쪽 안쪽 여백 = 아이콘 반지름 + 10.</summary>
    public static Border Tag(FrameworkElement scope, string text, string badge, string tone, double height, bool mirror)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(Text(scope, text, ThemeKeys.Text, bold: true));
        if (Shown(badge)) row.Children.Add(new TextBlock { Text = "  " + badge, FontWeight = FontWeights.SemiBold, Foreground = ThemeKeys.Tone(scope, tone), FontSize = Small(scope), VerticalAlignment = VerticalAlignment.Center });
        double inner = height / 2 + 10;
        return new Border
        {
            Height = height + 2, CornerRadius = new CornerRadius((height + 2) / 2), IsHitTestVisible = false,
            Padding = mirror ? new Thickness(inner, 0, 12, 0) : new Thickness(12, 0, inner, 0),
            Background = B(scope, "Hunsu.Glass"), BorderBrush = B(scope, ThemeKeys.Text), BorderThickness = new Thickness(1.5), Child = row,
            Effect = scope.TryFindResource("Hunsu.Shadow1") as System.Windows.Media.Effects.Effect,
        };
    }

    /// <summary>작은 이름표(모듈 줄 아이콘 위, 묶음 이름).</summary>
    public static Border Label(FrameworkElement scope, string text, bool strong) => new()
    {
        CornerRadius = new CornerRadius(999), Padding = new Thickness(8, 1, 8, 2), IsHitTestVisible = false,
        Background = B(scope, "Hunsu.Glass"), BorderBrush = B(scope, "Hunsu.Line"), BorderThickness = new Thickness(1),
        Child = new TextBlock { Text = text, FontSize = (double)(scope.TryFindResource("Hunsu.FontCaption") ?? 11.0), Foreground = B(scope, strong ? ThemeKeys.Text : ThemeKeys.Muted) },
    };

    /// <summary>잠든 점(지름 6). 숫자가 있으면 warn 톤, 열린 모듈이면 accent.</summary>
    public static Border Dot(FrameworkElement scope, bool hasBadge, bool open) => new()
    {
        Width = 6, Height = 6, CornerRadius = new CornerRadius(3), IsHitTestVisible = false,
        Background = open ? B(scope, ThemeKeys.Accent) : hasBadge ? ThemeKeys.Tone(scope, "warn") : B(scope, ThemeKeys.Muted),
    };

    public static bool Shown(string? badge) => !string.IsNullOrWhiteSpace(badge) && badge.Trim() != "-" && badge.Trim() != "0";

    private static Border Badge(FrameworkElement scope, string text, string tone, double h) => new()
    {
        MinWidth = h, Height = h, CornerRadius = new CornerRadius(h / 2), Padding = new Thickness(4, 0, 4, 0), IsHitTestVisible = false,
        Background = ThemeKeys.Tone(scope, string.IsNullOrWhiteSpace(tone) || tone == "info" ? "warn" : tone),
        BorderBrush = B(scope, "Hunsu.Surface"), BorderThickness = new Thickness(1.5),
        Child = new TextBlock { Text = text, FontSize = h * 0.62, FontWeight = FontWeights.SemiBold, Foreground = B(scope, "Hunsu.AccentInk"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
    };

    private static TextBlock Text(FrameworkElement scope, string text, string brushKey, bool bold = false) => new()
    {
        Text = text, Foreground = B(scope, brushKey), FontSize = Small(scope) + (bold ? 1 : 0), FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center,
    };

    private static double Small(FrameworkElement scope) => scope.TryFindResource("Hunsu.FontSmall") is double d ? d : 12;
}
