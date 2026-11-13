// 기능: Theme.xaml의 이름을 코드에서 쓸 때의 도우미. 톤 이름(info, good, warn, bad, muted)을 붓으로 바꾼다.
// 사용법: ThemeKeys.Tone(element, "warn") → Brush;  ThemeKeys.Get<Duration>(element, ThemeKeys.Fast)
// 의존: Theme/Theme.xaml(앱 자원에 합쳐져 있어야 함). 값은 여기 적지 않는다(이름만).
using System.Windows;
using System.Windows.Media;

namespace ClockOut.Hunsu.Ui.Theme;

public static class ThemeKeys
{
    public const string Fast = "Hunsu.Fast";
    public const string Base = "Hunsu.Base";
    public const string Text = "Hunsu.Text";
    public const string Muted = "Hunsu.Muted";
    public const string Accent = "Hunsu.Accent";

    public static Brush Tone(FrameworkElement scope, string? tone)
    {
        string key = (tone ?? "").Trim() switch
        {
            "info" => "Hunsu.Tone.Info",
            "good" => "Hunsu.Tone.Good",
            "warn" => "Hunsu.Tone.Warn",
            "bad" => "Hunsu.Tone.Bad",
            "muted" => "Hunsu.Tone.Muted",
            _ => Text,
        };
        return scope.TryFindResource(key) as Brush ?? Brushes.White;
    }

    public static T Get<T>(FrameworkElement scope, string key) => (T)scope.FindResource(key);
}
