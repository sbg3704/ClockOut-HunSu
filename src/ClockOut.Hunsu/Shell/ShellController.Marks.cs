// 기능: 셀 위 표시층(ShellController의 일부). 켜진 모듈의 view.json marks[] 선언을 모아, 해당 주제 조회 결과(칸 자리)를 MarkWindow에 그린다.
//       스크롤은 엑셀 이벤트가 없어서 엑셀이 앞에 있을 때만 settings.markPollMs 간격으로 그 주제를 다시 묻는다(끄는 중이면 QueryThrottle이 미룬다).
// 사용법: OnTopic이 결과마다 OnTopicForMarks(r)를 부르고, RefreshDocks·전부 숨김·엑셀 바뀜 때 UpdateMarkLayer()를 부른다.
// 의존: Overlay/MarkWindow(색연필은 PencilPainter), Core/Marks/MarkGeometry·FlowSketch, Core/View(MarkSpec, DataScope), Ui/Theme(ThemeKeys.Tone). 칸 값은 받지 않는다(자리만).
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Threading;
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Core.Marks;
using ClockOut.Hunsu.Core.View;
using ClockOut.Hunsu.Excel;
using ClockOut.Hunsu.Native;
using ClockOut.Hunsu.Overlay;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Shell;

internal sealed partial class ShellController
{
    private MarkWindow? _markWindow;
    private DispatcherTimer? _markPoll;
    private readonly Dictionary<string, JsonElement> _markData = new(StringComparer.Ordinal); // 주제 → 마지막 결과

    /// <summary>켜진 모듈의 marks 선언과 그 모듈.</summary>
    private IEnumerable<(ModuleView Module, MarkSpec Spec)> ActiveMarks() =>
        _modules.Where(m => !m.Placement.Hidden).SelectMany(m => m.Module.View.Marks.Select(s => (m, s)));

    private IReadOnlyList<string> MarkTopics() =>
        [.. ActiveMarks().SelectMany(x => x.Module.Module.Manifest.Data.Select(d => d.Topic).Where(t => x.Spec.Items.StartsWith(t + ".", StringComparison.Ordinal)))
            .Where(t => !TopicRouting.IsAppTopic(t)).Distinct(StringComparer.Ordinal)];

    private void OnTopicForMarks(TopicResult r)
    {
        if (r.Excel != _target || !MarkTopics().Contains(r.Topic)) return;
        _markData[r.Topic] = r.Response.Data.Clone();
        DrawMarks();
    }

    private void DrawMarks()
    {
        if (_hiddenAll || _target == IntPtr.Zero || User32.IsIconic(_target)) { _markWindow?.Clear(); return; }
        var specs = ActiveMarks().ToList();
        if (specs.Count == 0) { _markWindow?.Clear(); return; }
        _markWindow ??= new MarkWindow();
        _markWindow.SetOwner(_target);
        var excel = User32.WindowBounds(_target);
        var scope = new DataScope();
        foreach (var kv in _markData) scope.Set(kv.Key, kv.Value);
        var shapes = new List<(MarkBox, Brush, string)>();
        var roles = new Dictionary<string, (IReadOnlyList<SketchCell> Cells, string Tone)>(StringComparer.Ordinal);
        string sheet = scope.TryGet("mark.sheet", out var sh) && sh.ValueKind == System.Text.Json.JsonValueKind.String ? sh.GetString() ?? "" : "";
        var crossRefs = new Dictionary<string, IReadOnlyList<CrossRef>>(StringComparer.Ordinal);
        IReadOnlyList<string> tabNames = [];
        foreach (var (_, spec) in specs)
        {
            if (!scope.TryGet(spec.Items, out var arr)) continue;
            string topic = _markData.Keys.Where(t => spec.Items.StartsWith(t + ".", StringComparison.Ordinal)).OrderByDescending(t => t.Length).FirstOrDefault() ?? "";
            var panes = scope.TryGet(topic + ".panes", out var p) ? MarkGeometry.Read(p) : [];
            if (spec.Style == "pencil")
            {
                if (spec.Role is "crossPre" or "crossDep") crossRefs[spec.Role] = FlowSketch.ReadCross(arr);
                else if (spec.Role == "tabs") tabNames = FlowSketch.ReadTabs(arr);
                else roles[spec.Role] = (FlowSketch.ReadCells(arr, panes, excel, _markWindow.Scale), spec.Tone);
                continue;
            }
            var brush = ThemeKeys.Tone(_markWindow, spec.Tone);
            foreach (var box in MarkGeometry.Layout(MarkGeometry.Read(arr), panes, excel, _markWindow.Scale)) shapes.Add((box, brush, spec.Style));
        }
        SketchPlan? sketch = null;
        if (roles.TryGetValue("active", out var act))
        {
            (IReadOnlyList<SketchCell> Cells, string Tone) Role(string r, string tone) => roles.TryGetValue(r, out var v) ? v : ([], tone);
            var pre = Role("pre", "info");
            var dep = Role("dep", "good");
            sketch = FlowSketch.Plan(sheet, act.Cells, pre.Cells, dep.Cells, new SketchTones(act.Tone, pre.Tone, dep.Tone),
                SketchLook.ByName(_settings.PencilLook), Role("pre2", "info").Cells, Role("dep2", "good").Cells,
                BuildCross(scope, act.Cells, crossRefs, tabNames, excel, _markWindow.Scale));
        }
        // 움직임: 윈도우의 "애니메이션 효과" 설정이 꺼져 있으면 바로 그린다.
        _markWindow.Draw(excel, shapes, sketch, tone => ThemeKeys.Tone(_markWindow, tone), System.Windows.SystemParameters.ClientAreaAnimation);
    }

    /// <summary>다른 시트 흐름. flow.graph가 지금 고른 칸(시트·주소가 같음)의 것일 때만 쓴다(선택을 옮긴 직후 옛 결과가 남아 있을 수 있음).
    /// 시트 탭 줄 자리는 엑셀이 알려 주지 않아 격자 맨 아래(스크롤 막대 줄)에서 어림한다: 왼쪽은 격자 왼쪽 + 탭 이동 단추 폭, 높이는 한 줄.</summary>
    private static SketchCross? BuildCross(DataScope scope, IReadOnlyList<SketchCell> active, IReadOnlyDictionary<string, IReadOnlyList<CrossRef>> refs, IReadOnlyList<string> tabs, PixelRect excel, double scale)
    {
        if (active.Count == 0 || refs.Count == 0 || tabs.Count == 0) return null;
        if (!scope.TryGet("flow.graph.addr", out var fa) || !scope.TryGet("flow.graph.sheet", out var fs)) return null;
        if (fa.GetString() != active[0].Addr || fs.GetString() != (scope.TryGet("mark.sheet", out var ms) ? ms.GetString() : null)) return null;
        var panes = scope.TryGet("mark.panes", out var p) ? MarkGeometry.Read(p) : [];
        if (panes.Count == 0) return null;
        int left = panes.Min(x => x.X), bottom = panes.Max(x => x.Bottom);
        var strip = new MarkBox((left - excel.X) / scale + 74, (bottom - excel.Y) / scale + 1, Math.Max(200, (excel.Width / scale)), 22);
        var cross = new SketchCross(refs.GetValueOrDefault("crossPre") ?? [], refs.GetValueOrDefault("crossDep") ?? [], tabs, strip);
        return cross.IsEmpty ? null : cross;
    }

    /// <summary>표시층을 쓰는 모듈이 켜져 있으면 느린 다시 묻기를 켜고, 아니면 끄고 지운다.</summary>
    private void UpdateMarkLayer()
    {
        bool any = MarkTopics().Count > 0;
        if (!any)
        {
            _markPoll?.Stop();
            _markData.Clear();
            _markWindow?.Clear();
            return;
        }
        if (_settings.MarkPollMs > 0)
        {
            _markPoll ??= new DispatcherTimer(TimeSpan.FromMilliseconds(_settings.MarkPollMs), DispatcherPriority.Background, (_, _) => PollMarks(), System.Windows.Application.Current.Dispatcher);
            _markPoll.Start();
        }
        DrawMarks();
    }

    private void PollMarks()
    {
        if (_hiddenAll || _target == IntPtr.Zero) return;
        if (User32.GetForegroundWindow() != _target) return; // 다른 창을 쓰는 동안 엑셀을 바쁘게 하지 않는다
        _registry.Refresh(_target, [.. MarkTopics().Where(t => t == "mark")]); // 스크롤을 따라가려면 칸 자리만 다시 묻는다(무거운 흐름 조회는 선택이 바뀔 때만)
    }
}
