// 기능: 켤 때 테마(밝은/어두운), 패널 룩(손그림 프로필 skin), 도크 패널 너비를 앱 자원에 넣는다. 창을 만들기 전에 불러야 StaticResource가 새 값을 받는다.
// 사용법: ThemePicker.Apply(Application.Current, settings.Theme, settings.PanelWidth, settings.Skin);  → 고른 테마 이름("dark"/"light")
//   룩이 plain이 아니면 룩이 밝기를 정한다(밝은 룩은 ThemeLight 위에 Skin*.xaml을 얹음). 손글씨 글꼴(Nanum Pen Script, OFL)은 제목 글꼴로 넣는다.
// 의존: Theme/ThemeLight.xaml, Theme/Skin*.xaml, Core/Settings/ThemeChoice·SkinChoice, 앱의 Assets/Fonts(없으면 맑은 고딕). Windows 앱 모드는 HKCU\...\Themes\Personalize\AppsUseLightTheme(없으면 어두움).
using System.Windows;
using System.Windows.Media;
using ClockOut.Hunsu.Core.Settings;
using Microsoft.Win32;

namespace ClockOut.Hunsu.Ui.Theme;

public static class ThemePicker
{
    private const string LightSource = "/ClockOut.Hunsu.Ui;component/Theme/ThemeLight.xaml";

    public static string Apply(Application app, string? theme, double panelWidth, string? skin = null)
    {
        var spec = SkinChoice.ByName(skin ?? SkinChoice.Plain);
        bool light = spec.ForcedTheme is { } forced ? forced == ThemeChoice.Light : ThemeChoice.IsLight(theme, SystemPrefersLight());
        if (light) Merge(app, LightSource);
        if (spec.Dictionary is { } dict)
        {
            Merge(app, $"/ClockOut.Hunsu.Ui;component/Theme/{dict}");
            if (spec.HandTitle)
                app.Resources["Hunsu.FontTitle"] = new FontFamily(new Uri("pack://application:,,,/"), "./Assets/Fonts/#Nanum Pen Script, Malgun Gothic");
        }
        app.Resources["Hunsu.PanelWidth"] = panelWidth;
        return light ? ThemeChoice.Light : ThemeChoice.Dark;
    }

    private static void Merge(Application app, string source)
    {
        if (!app.Resources.MergedDictionaries.Any(d => d.Source?.OriginalString == source))
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source, UriKind.Relative) });
    }

    private static bool SystemPrefersLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v != 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
        {
            return false;
        }
    }
}
