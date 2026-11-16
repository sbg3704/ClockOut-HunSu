// 기능: 설정의 theme 값("dark", "light", "auto")과 Windows 앱 모드로 실제로 쓸 테마를 고른다. 화면과 레지스트리 없이 시험된다.
// 사용법: bool light = ThemeChoice.IsLight(settings.Theme, systemPrefersLight: true);
// 의존: 없음. Windows 앱 모드를 읽는 일은 앱(Ui/Theme/ThemePicker)이 맡는다.
namespace ClockOut.Hunsu.Core.Settings;

public static class ThemeChoice
{
    public const string Dark = "dark";
    public const string Light = "light";
    public const string Auto = "auto";

    public static readonly IReadOnlyList<string> All = [Dark, Light, Auto];

    /// <summary>모르는 값은 어두운 테마(docs/03의 기본).</summary>
    public static bool IsLight(string? theme, bool systemPrefersLight) => Normalize(theme) switch
    {
        Light => true,
        Auto => systemPrefersLight,
        _ => false,
    };

    public static string Normalize(string? theme)
    {
        string t = (theme ?? "").Trim().ToLowerInvariant();
        return All.Contains(t) ? t : Dark;
    }
}
