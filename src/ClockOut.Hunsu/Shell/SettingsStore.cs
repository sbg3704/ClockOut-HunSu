// 기능: 설정 파일(settings.json)을 읽는다. 없으면 기본값으로 만들어 두어 사용자가 열어서 숫자·목록 하나만 고치면 되게 한다. 읽기 실패는 기본값으로 계속한다.
// 사용법: var s = SettingsStore.Load(SettingsStore.PathFor(options.LayoutPath), log);  파일 위치: 배치 파일(layout.json)과 같은 폴더(기본 %LOCALAPPDATA%\ClockOut\Hunsu\settings.json)
// 의존: Core/Settings/HunsuSettings, Diagnostics/RunLog. 앱이 켜질 때 한 번만 읽는다(고친 뒤 훈수꾼을 껐다 켜면 반영).
using System.IO;
using ClockOut.Hunsu.Core.Settings;
using ClockOut.Hunsu.Diagnostics;

namespace ClockOut.Hunsu.Shell;

internal static class SettingsStore
{
    public static string PathFor(string? layoutPath)
    {
        string dir = layoutPath != null
            ? System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(layoutPath)) ?? "."
            : System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClockOut", "Hunsu");
        return System.IO.Path.Combine(dir, "settings.json");
    }

    public static HunsuSettings Load(string path, RunLog log)
    {
        try
        {
            if (File.Exists(path))
            {
                var s = HunsuSettings.FromJson(File.ReadAllText(path));
                log.Write("settings_load", ("path_ok", true));
                return s;
            }
            var defaults = new HunsuSettings();
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, defaults.ToJson());
            log.Write("settings_created", ("path_ok", true));
            return defaults;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Write("settings_failed", ("path_ok", false));
            return new HunsuSettings();
        }
    }
}
