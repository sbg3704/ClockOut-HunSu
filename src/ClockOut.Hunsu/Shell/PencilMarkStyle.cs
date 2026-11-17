// 기능: 색연필 수식 흐름 그리기 방식(style "pencil"). role(active·pre·dep·pre2·dep2·crossPre·crossDep·tabs)별 칸을 모아 FlowSketch 계획을 만든다. 다른 시트 흐름은 시트 탭 줄 어림 자리로 화살표를 잇는다.
// 사용법: MarkStyles.All에 들어 있다. 모듈은 view.json marks[]에 {"style":"pencil","role":"active", "items":"mark.active"}처럼 적기만 하면 된다(docs/12).
// 의존: Core/Marks(FlowSketch, MarkGeometry, SketchLook), Shell/MarkStyles. 셸 코드에 색연필만을 위한 분기가 없도록 여기로 모았다.
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Core.Marks;
using ClockOut.Hunsu.Core.View;

namespace ClockOut.Hunsu.Shell;

internal sealed class PencilMarkStyle : IMarkStyle
{
    public string Name => "pencil";

    private readonly Dictionary<string, (IReadOnlyList<SketchCell> Cells, string Tone)> _roles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<CrossRef>> _cross = new(StringComparer.Ordinal);
    private IReadOnlyList<string> _tabs = [];

    public void Begin() { _roles.Clear(); _cross.Clear(); _tabs = []; }

    public void Add(MarkSpec spec, System.Text.Json.JsonElement arr, IReadOnlyList<PixelRect> panes, MarkContext ctx)
    {
        if (spec.Role is "crossPre" or "crossDep") _cross[spec.Role] = FlowSketch.ReadCross(arr);
        else if (spec.Role == "tabs") _tabs = FlowSketch.ReadTabs(arr);
        else _roles[spec.Role] = (FlowSketch.ReadCells(arr, panes, ctx.Excel, ctx.Scale), spec.Tone);
    }

    public SketchPlan? Build(MarkContext ctx)
    {
        if (!_roles.TryGetValue("active", out var act)) return null;
        (IReadOnlyList<SketchCell> Cells, string Tone) Role(string r, string tone) => _roles.TryGetValue(r, out var v) ? v : ([], tone);
        var pre = Role("pre", "info");
        var dep = Role("dep", "good");
        string sheet = ctx.Scope.TryGet("mark.sheet", out var sh) && sh.ValueKind == System.Text.Json.JsonValueKind.String ? sh.GetString() ?? "" : "";
        return FlowSketch.Plan(sheet, act.Cells, pre.Cells, dep.Cells, new SketchTones(act.Tone, pre.Tone, dep.Tone),
            SketchLook.ByName(ctx.Settings.PencilLook), Role("pre2", "info").Cells, Role("dep2", "good").Cells,
            BuildCross(ctx.Scope, act.Cells, ctx.Excel, ctx.Scale));
    }

    /// <summary>다른 시트 흐름. flow.graph가 지금 고른 칸(시트·주소가 같음)의 것일 때만 쓴다(선택을 옮긴 직후 옛 결과가 남아 있을 수 있음).
    /// 시트 탭 줄 자리는 엑셀이 알려 주지 않아 격자 맨 아래(스크롤 막대 줄)에서 어림한다: 왼쪽은 격자 왼쪽 + 탭 이동 단추 폭, 높이는 한 줄.</summary>
    private SketchCross? BuildCross(DataScope scope, IReadOnlyList<SketchCell> active, PixelRect excel, double scale)
    {
        if (active.Count == 0 || _cross.Count == 0 || _tabs.Count == 0) return null;
        if (!scope.TryGet("flow.graph.addr", out var fa) || !scope.TryGet("flow.graph.sheet", out var fs)) return null;
        if (fa.GetString() != active[0].Addr || fs.GetString() != (scope.TryGet("mark.sheet", out var ms) ? ms.GetString() : null)) return null;
        var panes = scope.TryGet("mark.panes", out var p) ? MarkGeometry.Read(p) : [];
        if (panes.Count == 0) return null;
        int left = panes.Min(x => x.X), bottom = panes.Max(x => x.Bottom);
        var strip = new MarkBox((left - excel.X) / scale + 74, (bottom - excel.Y) / scale + 1, Math.Max(200, (excel.Width / scale)), 22);
        var cross = new SketchCross(_cross.GetValueOrDefault("crossPre") ?? [], _cross.GetValueOrDefault("crossDep") ?? [], _tabs, strip);
        return cross.IsEmpty ? null : cross;
    }
}
