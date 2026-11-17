// 기능: 설정 파일(settings.json)을 읽는다. 없으면 기본값으로 만들어 두어 사용자가 열어서 숫자·목록 하나만 고치면 되게 한다. 읽기 실패는 기본값으로 계속한다.
// 사용법: var s = SettingsStore.Load(SettingsStore.PathFor(options.LayoutPath), log);  파일 위치: 배치 파일(layout.json)과 같은 폴더(기본 %LOCALAPPDATA%\ClockOut\Hunsu\settings.json)
// 의존: Core/Settings/HunsuSettings, Diagnostics/RunLog. 앱이 켜질 때 한 번만 읽는다(손으로 고친 값은 껐다 켜면 반영). 읽을 때 사이드바 기본값 안내(WithSidebarDefault)를 한 번 적용한다.
//   설정 창 [모양]에서 고른 값은 Save로 바로 쓴다(이때 파일은 지금 설정 전체로 다시 쓰이고, 모르는 키는 빠진다).
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

    /// <summary>설정 전체를 파일에 쓴다(설정 창에서 고른 뒤). 실패해도 앱은 계속한다.</summary>
    public static void Save(string path, HunsuSettings settings, RunLog log)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, settings.ToJson());
            File.Move(tmp, path, overwrite: true);
            log.Write("settings_saved", ("path_ok", true));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Write("settings_save_failed", ("path_ok", false));
        }
    }

    public static HunsuSettings Load(string path, RunLog log)
    {
        try
        {
            if (File.Exists(path))
            {
                var (s, moved) = HunsuSettings.FromJson(File.ReadAllText(path)).WithSidebarDefault();
                log.Write("settings_load", ("path_ok", true), ("sidebar_default", moved));
                if (moved) Save(path, s, log); // 예전 설정은 한 번만 오른쪽 사이드바로(2026-11-17 사용자 요청, docs/19)
                return s;
            }
            var defaults = new HunsuSettings().WithSidebarDefault().Settings;
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
