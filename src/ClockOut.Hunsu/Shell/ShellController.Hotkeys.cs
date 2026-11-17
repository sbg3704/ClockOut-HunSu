// 기능: 모듈이 quick 선언의 hotkey로 요구한 단축키를 공용으로 등록하고(엑셀이 앞일 때만), 눌리면 그 설정을 켜고 끈다. 허브 우클릭 메뉴에도 같은 항목이 자동으로 생긴다. 셸에는 어떤 모듈의 단축키인지 지식이 없다.
// 사용법: RefreshDocks가 UpdateModuleHotkeys()를 부르면 켜진 모듈의 선언에서 목록을 다시 만들어 HotkeyService에 넣는다. 눌림은 OnModuleHotkey, 메뉴는 HubToggles.
// 의존: Core/HotkeyChord, Core/Manifest/QuickSetting(Hotkey), Shell/HotkeyService. 두 모듈이 같은 단축키를 선언하면 앞선 모듈 것만 쓴다.
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Core.Manifest;

namespace ClockOut.Hunsu.Shell;

internal sealed partial class ShellController
{
    private IReadOnlyList<(string Key, string Label, HotkeyChord Chord)> _moduleHotkeys = [];

    private static IEnumerable<QuickSetting> Flat(IReadOnlyList<QuickSetting> items) => items.SelectMany(q => q.Kind == QuickSetting.Menu ? Flat(q.Children) : [q]);

    private void UpdateModuleHotkeys()
    {
        var list = new List<(string, string, HotkeyChord)>();
        foreach (var mv in _modules.Where(m => !m.Placement.Hidden).OrderBy(m => m.Placement.Order))
            foreach (var q in Flat(mv.Module.Manifest.Quick).Where(q => q.Hotkey.Length > 0))
                if (HotkeyChord.TryParse(q.Hotkey, out var chord) && !list.Any(x => x.Item3.Mods == chord.Mods && x.Item3.Key == chord.Key) && !list.Any(x => x.Item1 == q.Key))
                    list.Add((q.Key, q.Label, chord));
        _moduleHotkeys = list;
        _hotkeys.SetModuleChords([.. list.Select(x => x.Item3)]);
    }

    private void OnModuleHotkey(int index)
    {
        if (index < 0 || index >= _moduleHotkeys.Count) return;
        ToggleQuick(_moduleHotkeys[index].Key);
    }

    /// <summary>켜고 끄기 설정을 뒤집는다(단축키·허브 메뉴 공용). 끄면 그 기능의 그림만 멈추고 모듈은 켜진 채다.</summary>
    private void ToggleQuick(string key) => ApplyQuick(key, !SettingOn(key));

    /// <summary>허브 우클릭 메뉴에 넣을 단축키 있는 켜고 끄기 항목들.</summary>
    private IReadOnlyList<(string Label, string Gesture, Action Act)> HubToggles() =>
        [.. _moduleHotkeys.Select(h => (h.Label, h.Chord.Text, (Action)(() => ToggleQuick(h.Key))))];
}
