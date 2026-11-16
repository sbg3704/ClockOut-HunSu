// 기능: 켤 때 테마(밝은/어두운)와 도크 패널 너비를 앱 자원에 넣는다. 창을 만들기 전에 불러야 StaticResource가 새 값을 받는다.
// 사용법: ThemePicker.Apply(Application.Current, settings.Theme, settings.PanelWidth);  → 고른 테마 이름("dark"/"light")
// 의존: Theme/ThemeLight.xaml, Core/Settings/ThemeChoice. Windows 앱 모드는 HKCU\...\Themes\Personalize\AppsUseLightTheme(없으면 어두움).
using System.Windows;
using ClockOut.Hunsu.Core.Settings;
using Microsoft.Win32;

namespace ClockOut.Hunsu.Ui.Theme;

public static class ThemePicker
{
    private const string LightSource = "/ClockOut.Hunsu.Ui;component/Theme/ThemeLight.xaml";

    public static string Apply(Application app, string? theme, double panelWidth)
    {
        bool light = ThemeChoice.IsLight(theme, SystemPrefersLight());
        if (light && !app.Resources.MergedDictionaries.Any(d => d.Source?.OriginalString == LightSource))
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(LightSource, UriKind.Relative) });
        app.Resources["Hunsu.PanelWidth"] = panelWidth;
        return light ? ThemeChoice.Light : ThemeChoice.Dark;
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
