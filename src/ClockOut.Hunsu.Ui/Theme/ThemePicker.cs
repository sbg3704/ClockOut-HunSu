// 기능: 테마(밝은/어두운), 패널 룩(손그림 프로필 skin), 도크 패널 너비를 앱 자원에 넣는다. 켤 때와 설정 창에서 룩·테마를 바꿀 때 같은 길(Apply)이고, 바꾸면 껐다 켜지 않고 바로 반영된다(SkinChanged).
// 사용법: ThemePicker.Apply(Application.Current, settings.Theme, settings.PanelWidth, settings.Skin);  → 고른 테마 이름("dark"/"light")
//   룩이 plain이 아니면 룩이 밝기를 정한다(Theme.xaml 위에 ThemeLight, 그 위에 Skin*.xaml의 값을 겹침). 손글씨 글꼴(Nanum Pen Script, OFL)은 제목 글꼴로 넣는다.
//   즉시 반영의 비결: 붓·그림자는 같은 개체의 색만 바꾸고(이미 쥔 곳이 따라감), 모서리·글꼴·크기는 앱 자원을 덮어써 DynamicResource/SetResourceReference로 읽는 곳이 따라간다.
// 의존: Theme/ThemeLight.xaml, Theme/Skin*.xaml, Core/Settings/ThemeChoice·SkinChoice, 앱의 Assets/Fonts(없으면 맑은 고딕). Windows 앱 모드는 HKCU\...\Themes\Personalize\AppsUseLightTheme(없으면 어두움).
using System.Windows;
using System.Windows.Media;
using ClockOut.Hunsu.Core.Settings;
using Microsoft.Win32;

namespace ClockOut.Hunsu.Ui.Theme;

public static class ThemePicker
{
    private const string Base = "/ClockOut.Hunsu.Ui;component/Theme/Theme.xaml";
    private const string LightSource = "/ClockOut.Hunsu.Ui;component/Theme/ThemeLight.xaml";

    /// <summary>룩·테마를 바꾼 직후(자원 값을 다 밀어 넣은 뒤) 알린다. 손그림 겹과 패널이 다시 그린다.</summary>
    public static event Action? SkinChanged;

    /// <summary>켤 때와 룩·테마를 바꿀 때 같은 길로 부른다. 새 사전을 읽어 앱 자원의 값에 밀어 넣는다: 붓(Brush)과 그림자는 **같은 개체의 색만** 바꿔서 이미 만들어진 창·패널도 바로 따라가고,
    /// 모서리·글꼴·크기 같은 값은 앱 자원을 덮어써 DynamicResource로 읽는 곳이 따라간다. 사전을 더하거나 빼지 않는다(이미 쥔 붓이 끊기지 않게).</summary>
    public static string Apply(Application app, string? theme, double panelWidth, string? skin = null)
    {
        var spec = SkinChoice.ByName(skin ?? SkinChoice.Plain);
        bool light = spec.ForcedTheme is { } forced ? forced == ThemeChoice.Light : ThemeChoice.IsLight(theme, SystemPrefersLight());
        // 어두운 기본값 위에 밝은 값, 그 위에 룩 값을 겹친다(룩이 정하지 않은 값은 기본으로 돌아간다).
        var target = new Dictionary<object, object>();
        Overlay(target, Base);
        if (light) Overlay(target, LightSource);
        if (spec.Dictionary is { } dict) Overlay(target, $"/ClockOut.Hunsu.Ui;component/Theme/{dict}");
        if (spec.HandTitle)
            target["Hunsu.FontTitle"] = new FontFamily(new Uri("pack://application:,,,/"), "./Assets/Fonts/#Nanum Pen Script, Malgun Gothic");
        foreach (var (key, value) in target) Push(app, key, value);
        app.Resources["Hunsu.PanelWidth"] = panelWidth;
        SkinChanged?.Invoke();
        return light ? ThemeChoice.Light : ThemeChoice.Dark;
    }

    private static void Overlay(Dictionary<object, object> target, string source)
    {
        var dict = new ResourceDictionary { Source = new Uri(source, UriKind.Relative) };
        foreach (System.Collections.DictionaryEntry e in dict)
            if (e.Key is string k && e.Value is SolidColorBrush or Color or System.Windows.Media.Effects.DropShadowEffect or CornerRadius or double or string or FontFamily or Thickness or Duration)
                target[k] = e.Value!;
    }

    private static void Push(Application app, object key, object value)
    {
        var current = app.TryFindResource(key);
        try
        {
            if (current is SolidColorBrush cb && value is SolidColorBrush nb && !cb.IsFrozen) { cb.Color = nb.Color; return; }
            if (current is System.Windows.Media.Effects.DropShadowEffect ce && value is System.Windows.Media.Effects.DropShadowEffect ne && !ce.IsFrozen)
            {
                ce.Color = ne.Color; ce.Opacity = ne.Opacity; ce.BlurRadius = ne.BlurRadius; ce.ShadowDepth = ne.ShadowDepth;
                return;
            }
        }
        catch (InvalidOperationException) { /* 읽기 전용 개체면 아래에서 새 개체로 덮어쓴다 */ }
        app.Resources[key] = value;
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
