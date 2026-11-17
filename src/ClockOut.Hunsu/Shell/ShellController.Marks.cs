// 기능: 셀 위 표시층(ShellController의 일부). 켜진 모듈의 view.json marks[] 선언을 모아, 해당 주제 조회 결과(칸 자리)를 MarkWindow에 그린다.
//       스크롤은 엑셀 이벤트가 없어서 엑셀이 앞에 있을 때만 settings.markPollMs 간격으로 화면 자리(속성만, 매크로 없음)를 보고, 움직였을 때만 그 주제를 다시 묻는다(끄는 중이면 QueryThrottle이 미룬다).
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
        _modules.Where(m => !m.Placement.Hidden).SelectMany(m => m.Module.View.Marks.Where(s => SettingOn(s.EnabledKey)).Select(s => (m, s)));

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
        var ctx = new MarkContext(scope, excel, _markWindow.Scale, _settings);
        var shapes = new List<(MarkBox, Brush, string)>();
        foreach (var st in MarkStyles.All) st.Begin();
        foreach (var (_, spec) in specs)
        {
            if (!scope.TryGet(spec.Items, out var arr)) continue;
            string topic = _markData.Keys.Where(t => spec.Items.StartsWith(t + ".", StringComparison.Ordinal)).OrderByDescending(t => t.Length).FirstOrDefault() ?? "";
            var panes = scope.TryGet(topic + ".panes", out var p) ? MarkGeometry.Read(p) : [];
            if (MarkStyles.Find(spec.Style) is { } style) { style.Add(spec, arr, panes, ctx); continue; }
            var brush = ThemeKeys.Tone(_markWindow, spec.Tone);
            foreach (var box in MarkGeometry.Layout(MarkGeometry.Read(arr), panes, excel, _markWindow.Scale)) shapes.Add((box, brush, spec.Style));
        }
        SketchPlan? sketch = MarkStyles.All.Select(st => st.Build(ctx)).FirstOrDefault(x => x != null);
        // 움직임: 윈도우의 "애니메이션 효과" 설정이 꺼져 있으면 바로 그린다.
        _markWindow.Draw(excel, shapes, sketch, tone => ThemeKeys.Tone(_markWindow, tone), System.Windows.SystemParameters.ClientAreaAnimation);
    }

    /// <summary>표시층을 쓰는 모듈이 켜져 있으면 느린 다시 묻기를 켜고, 아니면 끄고 지운다.</summary>
    private void UpdateMarkLayer()
    {
        bool any = MarkTopics().Count > 0;
        if (!any)
        {
            _markPoll?.Stop();
            if (_target != IntPtr.Zero) _registry.ForgetView(_target);
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
        // 스크롤을 따라가려면 칸 자리만 다시 묻는다(무거운 흐름 조회는 선택이 바뀔 때만). 화면 자리를 속성으로 먼저 보고 움직였을 때만 다리를 부른다:
        // 매번 다리 매크로를 돌리면 엑셀 커서가 '계산 중'과 기본 사이를 계속 오갔다(2026-11-17 신고).
        _registry.RefreshIfViewMoved(_target, [.. MarkTopics().Where(t => t == "mark")]);
    }
}
