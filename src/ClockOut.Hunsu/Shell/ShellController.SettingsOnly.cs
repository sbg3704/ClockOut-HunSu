// 기능: "설정만" 모드(ShellController의 일부, 2026-11-22). 칼퇴 [설정창 열기]가 꺼진 훈수꾼을 `--settings`로 켜면 설정 창 하나만 띄운다.
//   도크·핀 창·알림 영역 아이콘을 띄우지 않고, 엑셀을 따라가거나 묻지 않는다. 설정 창을 닫으면 그대로 끝난다.
//   그 사이 훈수꾼을 켜면(칼퇴 [켜기] = 보통 실행이 잠금에 걸려 AppMessages.Start를 넘김, 또는 Ctrl+Alt+H) 끝내지 않고 이어서 켠다(LeaveSettingsOnly).
// 사용법: Start()가 _settingsOnly면 OpenSettingsOnly()를 부른다. 다른 동작은 _settingsOnly를 보고 창을 건드리지 않는다(RefreshDocks, SetEnabled, UpdateSidebar 등).
// 의존: ShellController.cs(StartOverlay, Quit), ShellController.Manager.cs(OpenManager), Shell/ManagerWindow.
namespace ClockOut.Hunsu.Shell;

internal sealed partial class ShellController
{
    private bool _settingsOnly;

    /// <summary>설정만 모드인지(설정 창이 머리글에 안내를 보인다).</summary>
    internal bool SettingsOnly => _settingsOnly;

    /// <summary>칼퇴에서 꺼서 불러오지 않은 모듈 id(설정 창 [모듈]이 수를 알린다).</summary>
    internal IReadOnlyList<string> SkippedModules => _skippedModules;

    private void OpenSettingsOnly()
    {
        _log.Write("settings_only", ("modules", _modules.Count), ("skipped", _skippedModules.Count));
        OpenManager("general");
        _manager!.Closed += (_, _) => { if (_settingsOnly) Quit(); };
    }

    /// <summary>설정만 띄운 채로 있다가 훈수꾼을 켠다: 도크를 띄우고 엑셀을 따라가기 시작한다. 설정 창은 열린 채 둔다.</summary>
    internal void LeaveSettingsOnly(string why)
    {
        if (!_settingsOnly) return;
        _settingsOnly = false;
        _log.Write("settings_only_leave", ("why", why));
        _tray.Visible = true;
        StartOverlay();
        UpdateQuickBar();
        ModulesChanged?.Invoke(); // 설정 창 머리글의 "설정만" 안내를 지운다
    }
}
