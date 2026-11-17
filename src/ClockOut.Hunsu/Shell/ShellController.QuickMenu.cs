// 기능: 모듈 공용 "빠른 설정" 우클릭 메뉴(ShellController의 일부). 모듈이 manifest의 quick 선언만 쓰면 아이콘·패널·빠른 띠 우클릭에 켜기·끄기와 고르기가 자동으로 생긴다. 자세한 설정은 "자세히…"로 설정 창을 연다.
// 사용법: ModuleIconMenu가 열릴 때마다 FillQuick(menu, mv)로 맨 위 항목을 새로 채운다(현재 값이 바뀌어도 맞다). 수식 흐름 그리기는 ToggleDraw(Ctrl+Alt+D, 허브 메뉴).
// 의존: Core/Manifest/QuickSetting(선언·값 해석), ApplySetting(저장·반영). 모듈마다 메뉴 코드는 없다.
using System.Windows.Controls;
using ClockOut.Hunsu.Core.Manifest;
using ClockOut.Hunsu.Ui.Frame;

namespace ClockOut.Hunsu.Shell;

internal sealed partial class ShellController
{
    /// <summary>모듈 우클릭 메뉴 맨 위의 빠른 설정을 지금 값으로 다시 채운다(그리기는 Ui/Frame/QuickMenu). 선언이 없으면 아무것도 넣지 않는다.</summary>
    private void FillQuick(ContextMenu menu, ModuleView mv)
    {
        string json = _settings.ToJson();
        QuickMenu.Fill(menu, mv.Module.Manifest.Quick, key => QuickSettings.Current(json, key), ApplyQuick);
    }

    private void ApplyQuick(string key, object value)
    {
        ApplySetting(key, value);
        _log.Write("quick_setting", ("key", key), ("value", value.ToString() ?? ""));
    }

    /// <summary>수식 흐름 그리기 켜고 끄기(Ctrl+Alt+D, 허브·모듈 우클릭 메뉴). 끄면 그림만 멈추고 모듈은 켜진 채다.</summary>
    internal void ToggleDraw() => ApplyQuick("pencilDraw", !_settings.PencilDraw);

    /// <summary>설정이 바뀐 뒤 할 일은 모듈 선언의 quick apply가 정한다("redraw": 셀 위 그림만 다시, "refresh": 켜진 주제를 엑셀에 다시 묻고 다시). 키마다 셸 코드가 없다.</summary>
    private void OnQuickApply(string key)
    {
        string apply = _modules.Where(m => !m.Placement.Hidden).Select(m => ApplyOf(m.Module.Manifest.Quick, key)).FirstOrDefault(a => a.Length > 0) ?? "";
        if (apply.Length == 0) return;
        if (apply == "refresh" && _target != IntPtr.Zero) _registry.Refresh(_target, AllEnabledTopics());
        DrawMarks();
        RefreshWorking();
    }

    private static string ApplyOf(IReadOnlyList<QuickSetting> items, string key)
    {
        foreach (var q in items)
        {
            if (q.Key == key && q.Apply.Length > 0) return q.Apply;
            string inner = ApplyOf(q.Children, key);
            if (inner.Length > 0) return inner;
        }
        return "";
    }
}
