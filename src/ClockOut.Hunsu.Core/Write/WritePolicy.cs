// 기능: 쓰기 명령(실험 기능)의 순수 규칙. 쓰기 명령 이름(write. 접두어) 판별, 공지를 다시 보일지, 확인 창 글, 다리의 계획·실행 응답 읽기, 명령 인자에 확인 표시 더하기.
// 사용법: WritePolicy.IsWrite(cmd); WritePolicy.NeedsNotice(settings.WriteAck); var plan = WritePolicy.ReadPlan(resp); var args2 = WritePolicy.Confirmed(argsJson);
//   흐름(Shell/ShellController.Write): 공지 확인 → 인자 그대로 보내 "계획"(바꾸지 않음) → 확인 창 → 인자에 confirm=1을 더해 "실행" → 되돌릴 수 있으면 되돌리기 단추.
// 의존: Core/BridgeResponse, System.Text.Json. 화면과 엑셀 없이 시험된다. 결정 근거는 docs/01 9장 8번(2026-11-16 개정), docs/11 2장.
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClockOut.Hunsu.Core.Write;

/// <summary>다리가 "계획" 단계에서 돌려준 것: 바뀔 개수와 한 줄 설명.</summary>
public sealed record WritePlan(long Count, string Summary);

/// <summary>다리가 "실행" 단계에서 돌려준 것: 실제로 바뀐 개수, 결과 글, 되돌릴 수 있으면 그 명령과 인자.</summary>
public sealed record WriteResult(long Changed, string Message, string? UndoCommand, string? UndoArgs);

public static class WritePolicy
{
    public const string Prefix = "write.";

    /// <summary>공지 판. 공지 내용이 바뀌면 이 값을 올려서 사용자가 다시 확인하게 한다.</summary>
    public const string NoticeVersion = "2026-11-16";

    public static bool IsWrite(string? command) => command != null && command.StartsWith(Prefix, StringComparison.Ordinal) && command.Length > Prefix.Length;

    public static bool NeedsNotice(string? ack) => !string.Equals(ack, NoticeVersion, StringComparison.Ordinal);

    public const string NoticeTitle = "실험 기능입니다";

    public const string NoticeBody =
        "지금 누르려는 도구는 엑셀의 칸·시트·이름을 직접 바꿉니다.\n\n" +
        "• 아직 실험 기능이라 오류가 날 수 있습니다.\n" +
        "• 실행하면 엑셀의 실행 취소(Ctrl+Z)로 되돌릴 수 없고, 그 전의 실행 취소 기록이 지워질 수 있습니다(아직 확인하지 못했습니다).\n" +
        "• 바꾸기 전에 파일을 저장해 두세요.\n" +
        "• 실행할 때마다 무엇이 얼마나 바뀌는지 먼저 보여 드리고 확인을 받습니다.";

    public static string ConfirmBody(WritePlan plan) =>
        plan.Summary + "\n\n실행하면 되돌릴 수 없을 수 있습니다. 파일을 먼저 저장하셨나요?";

    /// <summary>명령 인자(평평한 글자 객체 JSON)에 confirm=1을 더한다. 다리는 이 표시가 있을 때만 엑셀을 바꾼다.</summary>
    public static string Confirmed(string? argsJson)
    {
        JsonObject obj;
        try { obj = JsonNode.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson) as JsonObject ?? new JsonObject(); }
        catch (JsonException) { obj = new JsonObject(); }
        obj["confirm"] = "1";
        return obj.ToJsonString();
    }

    public static WritePlan? ReadPlan(BridgeResponse r)
    {
        if (!r.Ok || r.Data.ValueKind != JsonValueKind.Object) return null;
        if (!r.Data.TryGetProperty("count", out var c) || !c.TryGetInt64(out var count)) return null;
        return new WritePlan(count, Str(r.Data, "summary"));
    }

    public static WriteResult? ReadResult(BridgeResponse r)
    {
        if (!r.Ok || r.Data.ValueKind != JsonValueKind.Object) return null;
        long changed = r.Data.TryGetProperty("changed", out var c) && c.TryGetInt64(out var v) ? v : 0;
        string undo = Str(r.Data, "undoCmd");
        string undoArgs = Str(r.Data, "undoArgs");
        return new WriteResult(changed, Str(r.Data, "message"), IsWrite(undo) ? undo : null, IsWrite(undo) ? undoArgs : null);
    }

    private static string Str(JsonElement obj, string name) => obj.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : "";
}
