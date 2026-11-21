// 기능: 한 수(hunsu-hansu, docs/22) 창 열기와 잇기(ShellController의 일부). app.hansu.open → 한 수 창, 선택 요약이 올 때마다 창에 알림, [넣기] → 쓰기 흐름(write.formula.set: 공지 → 계획(값 미리 보기) → 확인 → 실행 → 되돌리기).
// 사용법: OnCommand가 "app.hansu."으로 시작하는 명령을 OnHansuCommand로, OnTopic이 selection.summary 결과를 FeedHansu로 넘긴다.
// 의존: Shell/HansuWindow, Core/Formula/FunctionCatalog(모듈 폴더의 functions.ko.json), ShellController.Write(OnWriteCommand). 셀 값은 보지 않는다(주소와 만든 수식 글만).
using System.IO;
using System.Text.Json;
using ClockOut.Hunsu.Core.Formula;
using ClockOut.Hunsu.Excel;

namespace ClockOut.Hunsu.Shell;

internal sealed partial class ShellController
{
    private const string HansuCatalogFile = "functions.ko.json";
    private HansuWindow? _hansu;
    private ModuleView? _hansuModule;
    private FunctionCatalog? _hansuCatalog;
    private readonly List<string> _hansuRecent = [];

    private static bool IsHansuCommand(string command) => command.StartsWith("app.hansu.", StringComparison.Ordinal);

    private void OnHansuCommand(ModuleView mv, string command)
    {
        if (command != "app.hansu.open") return;
        _hansuModule = mv;
        _hansuCatalog ??= LoadHansuCatalog(mv);
        if (_hansu is { IsLoaded: true })
        {
            _hansu.Activate();
            return;
        }
        _hansu = new HansuWindow(_hansuCatalog, _hansuRecent);
        _hansu.InsertRequested += OnHansuInsert;
        _hansu.Closed += (_, _) => _hansu = null;
        if (mv.Scope.TryGet("selection.summary", out var d) && d.ValueKind == JsonValueKind.Object)
            _hansu.SetTarget(Str(d, "book"), Str(d, "sheet"), Str(d, "addr"));
        _hansu.Show();
        _log.Write("hansu_open", ("functions", _hansuCatalog.Functions.Count));
    }

    private FunctionCatalog LoadHansuCatalog(ModuleView mv)
    {
        string path = Path.Combine(mv.Module.Dir, HansuCatalogFile);
        try
        {
            var (catalog, errors) = FunctionCatalog.Parse(File.ReadAllText(path));
            foreach (var e in errors) _log.Write("hansu_catalog", ("error", e));
            return catalog;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Write("hansu_catalog", ("error", ex.GetType().Name));
            return FunctionCatalog.Empty;
        }
    }

    /// <summary>따라가는 엑셀의 선택이 바뀌었다: 한 수 창이 떠 있으면 알린다.</summary>
    private void FeedHansu(TopicResult r)
    {
        if (_hansu == null || r.Excel != _target) return;
        var d = r.Response.Data;
        if (d.ValueKind != JsonValueKind.Object) return;
        _hansu.OnSelection(Str(d, "book"), Str(d, "sheet"), Str(d, "addr"));
    }

    private async void OnHansuInsert(string sheet, string addr, string formula, string name)
    {
        if (_hansuModule == null) return;
        string args = JsonSerializer.Serialize(new Dictionary<string, string> { ["sheet"] = sheet, ["addr"] = addr, ["formula"] = formula });
        _log.Write("hansu_insert", ("fn", name), ("len", formula.Length));
        await OnWriteCommand(_hansuModule, "write.formula.set", args);
    }
}
