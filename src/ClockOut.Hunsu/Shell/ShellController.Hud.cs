// 기능: 모듈이 선언한 "따라다니는 글 HUD"(manifest hud)를 보이는 공용 일. 커서 옆(범위를 끄는 동안) 또는 고른 칸 옆(계속)에 글을 띄운다. 어떤 글을 언제 보일지는 모듈 선언이 정하고, 셸은 주제 이름·합계 같은 특정 모듈 지식이 없다.
//       설정 값 읽기(SettingValue·SettingOn)도 여기에 둔다: 선언이 말하는 설정 키를 지금 설정에서 찾는다(표시층 enabledKey, quick, hud가 같이 쓴다).
// 사용법: OnTopic이 결과마다 RefreshHuds()를, 엑셀이 뒤로 가면 HideHuds()를, 설정 바뀜이 OnHudSettingChanged(key)를 부른다. 고른 칸 옆 붙이기는 mark 조회가 필요해 ExtraSubscriptions()가 자동으로 구독을 더한다.
// 의존: Core/Manifest(HudSpec), Core/View(DataScope), Overlay/CursorHud, Native/User32. 모듈마다 창을 하나씩 갖는다.
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Core.Manifest;
using ClockOut.Hunsu.Core.Settings;
using ClockOut.Hunsu.Native;
using ClockOut.Hunsu.Overlay;

namespace ClockOut.Hunsu.Shell;

internal sealed partial class ShellController
{
    private readonly Dictionary<string, CursorHud> _huds = new(StringComparer.Ordinal);
    private DateTime _lastDragSeen = DateTime.MinValue;
    private PixelRect? _activeCell;
    private HunsuSettings? _settingsNodeFor;
    private System.Text.Json.Nodes.JsonNode? _settingsNode;

    private const int HudDragGraceMs = 700, HudCellGap = 6;

    /// <summary>설정 값(소문자 글). 설정이 바뀌어야만 JSON을 다시 만든다. 없으면 빈 글.</summary>
    private string SettingValue(string key)
    {
        if (!ReferenceEquals(_settingsNodeFor, _settings)) { _settingsNode = System.Text.Json.Nodes.JsonNode.Parse(_settings.ToJson()); _settingsNodeFor = _settings; }
        return _settingsNode?[key]?.ToString().ToLowerInvariant() ?? "";
    }

    /// <summary>켜고 끄는 설정 키가 켜져 있나. 키가 비면 늘 켜짐.</summary>
    private bool SettingOn(string key) => key.Length == 0 || SettingValue(key) == "true";

    private IEnumerable<(ModuleView Module, HudSpec Hud)> ActiveHuds() =>
        _modules.Where(m => !m.Placement.Hidden && m.Module.Manifest.Hud is { } h && SettingOn(h.EnabledKey)).Select(m => (m, m.Module.Manifest.Hud!));

    private string HudAnchorOf(HudSpec h) => h.EffectiveAnchor(h.AnchorKey.Length == 0 ? null : SettingValue(h.AnchorKey));

    /// <summary>고른 칸 옆 HUD가 하나라도 있으면 mark 조회(고른 칸 자리)를 함께 받는다. 켜진 모듈의 구독에 더해 신호마다 다시 묻는다.</summary>
    private IEnumerable<DataSubscription> ExtraSubscriptions() =>
        ActiveHuds().Any(x => HudAnchorOf(x.Hud) == HudSpec.Cell) ? [new DataSubscription("mark", ["sel", "change", "sheet", "calc"], "normal")] : [];

    /// <summary>mark 조회 결과에서 고른 칸 자리를 읽어 두고 HUD를 갱신한다.</summary>
    private void OnMarkForHud(System.Text.Json.JsonElement data)
    {
        _activeCell = data.ValueKind == System.Text.Json.JsonValueKind.Object && data.TryGetProperty("active", out var a)
            ? Core.Marks.MarkGeometry.Read(a).Cast<PixelRect?>().FirstOrDefault() : null;
        RefreshHuds();
    }

    /// <summary>선언이 있는 모든 HUD를 지금 값으로 보이거나 숨긴다.</summary>
    private void RefreshHuds()
    {
        var live = new HashSet<string>(StringComparer.Ordinal);
        if (!_hiddenAll && _target != IntPtr.Zero)
            foreach (var (mv, hud) in ActiveHuds())
            {
                string? text = hud.Render(mv.Scope);
                if (text == null) { HideHud(mv.Id); continue; }
                live.Add(mv.Id);
                if (HudAnchorOf(hud) == HudSpec.Cell) ShowAtCell(mv.Id, text);
                else ShowAtCursor(mv.Id, text);
            }
        foreach (var id in _huds.Keys.Where(k => !live.Contains(k)).ToList()) HideHud(id);
    }

    private void ShowAtCell(string id, string text)
    {
        if (_activeCell is not { } c) { HideHud(id); return; }
        Hud(id).ShowAt(text, c.X + c.Width + HudCellGap, c.Y);
    }

    /// <summary>범위를 끄는 동안(또는 놓은 직후 잠깐) 글을 커서 옆에 보인다.</summary>
    private void ShowAtCursor(string id, string text)
    {
        var now = DateTime.UtcNow;
        if (User32.LeftButtonDown()) _lastDragSeen = now;
        else if (now - _lastDragSeen > TimeSpan.FromMilliseconds(HudDragGraceMs)) return;
        Hud(id).Show(text);
    }

    private CursorHud Hud(string id) => _huds.TryGetValue(id, out var h) ? h : _huds[id] = new CursorHud();

    private void HideHud(string id) { if (_huds.TryGetValue(id, out var h)) h.HideNow(); }

    private void HideHuds() { foreach (var h in _huds.Values) h.HideNow(); }

    /// <summary>HUD가 쓰는 설정이 바뀌면 고른 칸 자리를 다시 받고 글을 갱신한다.</summary>
    private void OnHudSettingChanged(string key)
    {
        if (!_modules.Any(m => m.Module.Manifest.Hud is { } h && (h.EnabledKey == key || h.AnchorKey == key))) return;
        _activeCell = null;
        RefreshHuds();
        if (_target != IntPtr.Zero) _registry.Refresh(_target, AllEnabledTopics());
    }
}
