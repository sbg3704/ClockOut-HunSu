// 기능: "일하는 중" 점. 줄어든 모듈 아이콘 귀퉁이에서 천천히 숨 쉬듯 깜빡인다(투명도만 움직임, 부드럽고 가볍다). Flash는 데이터 도착 때 한 번 반짝.
// 사용법: 아이콘 Canvas/Grid에 ActivityDot.Create(scope, size)를 올려 두면 끝. 모듈마다 코드 없음. Visibility가 Collapsed면 애니메이션은 멈춘다.
// 의존: Theme(ThemeKeys.Accent). 색·시간은 여기서만 정한다(사이 900ms, 투명도 0.35~1).
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Ui.Frame;

public static class ActivityDot
{
    public static readonly Duration Breath = new(TimeSpan.FromMilliseconds(900));

    /// <summary>지름 size의 숨 쉬는 점. 화면에 올라가 있는 동안만 움직인다.</summary>
    public static Border Create(FrameworkElement scope, double size = 7)
    {
        var dot = new Border
        {
            Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), IsHitTestVisible = false,
            Background = scope.TryFindResource(ThemeKeys.Accent) as Brush ?? Brushes.DodgerBlue,
            BorderBrush = scope.TryFindResource("Hunsu.Glass") as Brush ?? Brushes.Black, BorderThickness = new Thickness(1),
        };
        dot.Loaded += (_, _) => dot.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(1.0, 0.35, Breath) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() });
        dot.Unloaded += (_, _) => dot.BeginAnimation(UIElement.OpacityProperty, null);
        return dot;
    }
}
