// 기능: 최근 이동 기록(ShellController의 일부). 선택 요약 결과에서 통합 문서·시트·주소를 받아 Core/Nav/NavHistory에 쌓고, 앱 주제 app.history로 모듈에 넘긴다.
//       모듈의 단추·줄이 보내는 앱 명령(app.history.back, app.history.forward, app.history.jump)을 받아 다리 goto로 그 자리에 간다.
// 사용법: OnTopic이 selection.summary 결과마다 ObserveHistory(r)를 부르고, OnCommand가 "app."으로 시작하는 명령을 OnAppCommand로 넘긴다.
// 의존: Core/Nav/NavHistory, Core/TopicRouting(app. 주제), Excel/InstanceRegistry(DoAsync goto). 주소만 기억하고 셀 값은 담지 않는다.
using System.Text.Json;
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Core.Nav;
using ClockOut.Hunsu.Excel;

namespace ClockOut.Hunsu.Shell;

internal sealed partial class ShellController
{
    public const string HistoryTopic = "app.history";
    private readonly NavHistory _history = new();
    private string? _historyBook;

    private void ObserveHistory(TopicResult r)
    {
        var d = r.Response.Data;
        if (d.ValueKind != JsonValueKind.Object) return;
        string book = Str(d, "book"), sheet = Str(d, "sheet"), addr = Str(d, "addr");
        _historyBook = book;
        if (_history.Observe(book, sheet, addr, Environment.TickCount64)) PublishHistory();
    }

    private void PublishHistory()
    {
        if (!_modules.Any(m => !m.Placement.Hidden && m.Wants(HistoryTopic))) return;
        using var doc = JsonDocument.Parse(_history.ToJson(_historyBook, Environment.TickCount64));
        ForTopic(HistoryTopic, m => m.SetData(HistoryTopic, doc.RootElement));
    }

    /// <summary>앱 명령: 기록에서 자리를 고르고 다리 goto로 간다. 모르는 앱 명령은 기록만 남긴다.</summary>
    private async void OnAppCommand(ModuleView mv, string command, string argsJson)
    {
        NavEntry? target = command switch
        {
            "app.history.back" => _history.Back(),
            "app.history.forward" => _history.Forward(),
            "app.history.jump" => int.TryParse(ArgStr(argsJson, "index"), out int i) ? _history.Jump(i) : null,
            _ => null,
        };
        _log.Write("app_command", ("module", mv.Id), ("cmd", command), ("moved", target != null));
        if (target == null || _target == IntPtr.Zero) return;
        PublishHistory();
        string args = JsonSerializer.Serialize(new Dictionary<string, string> { ["sheet"] = target.Sheet, ["addr"] = target.Addr });
        var call = await _registry.DoAsync(_target, "goto", args);
        var resp = call.Ok ? BridgeResponse.Parse(call.Json) : BridgeResponse.Failed(call.ErrorCode, "엑셀 호출 실패");
        _log.Write("command", ("module", mv.Id), ("cmd", "goto"), ("from", command), ("ok", resp.Ok), ("code", resp.Code), ("ms", Math.Round(call.Ms, 1)));
    }

    private static string Str(JsonElement o, string name) =>
        o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static string ArgStr(string json, string name)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object ? Str(doc.RootElement, name) : "";
        }
        catch (JsonException)
        {
            return "";
        }
    }
}
