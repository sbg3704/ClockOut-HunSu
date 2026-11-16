// 기능: 쓰기 명령(실험 기능, docs/11 2장) 처리(ShellController의 일부). 공지(처음 한 번, 공지 판이 바뀌면 다시) → 계획 조회(바뀌는 개수) → 확인 창 → 실행 → 결과 창(되돌릴 수 있으면 되돌리기 단추).
// 사용법: OnCommand가 write. 로 시작하는 명령을 OnWriteCommand로 넘긴다. 모듈 선언의 commands에 있는 쓰기 명령만 여기까지 온다.
// 의존: Core/Write/WritePolicy, Shell/WriteDialog, Shell/SettingsStore, Excel/InstanceRegistry(DoAsync). 셀 값은 앱이 보지 않는다(개수와 설명 글만).
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Core.Write;
using ClockOut.Hunsu.Excel;

namespace ClockOut.Hunsu.Shell;

internal sealed partial class ShellController
{
    private bool _writing;

    private async Task OnWriteCommand(ModuleView mv, string command, string argsJson)
    {
        if (_target == IntPtr.Zero || _writing) return;
        _writing = true;
        try
        {
            if (WritePolicy.NeedsNotice(_settings.WriteAck))
            {
                if (WriteDialog.Ask(WritePolicy.NoticeTitle, WritePolicy.NoticeBody, "이해했습니다", "취소") != WriteDialog.Choice.Ok)
                {
                    _log.Write("write_notice_declined", ("module", mv.Id), ("cmd", command));
                    return;
                }
                _settings = _settings.WithWriteAck(WritePolicy.NoticeVersion);
                SettingsStore.Save(SettingsPath, _settings, _log);
                _log.Write("write_notice_ack", ("version", WritePolicy.NoticeVersion));
            }
            var planCall = await _registry.DoAsync(_target, command, argsJson);
            var planResp = planCall.Ok ? BridgeResponse.Parse(planCall.Json) : BridgeResponse.Failed(planCall.ErrorCode, "엑셀 호출 실패");
            var plan = WritePolicy.ReadPlan(planResp);
            if (plan == null)
            {
                _log.Write("write_plan_failed", ("module", mv.Id), ("cmd", command), ("code", planResp.Code));
                WriteDialog.Ask("할 수 없습니다", planResp.Error ?? "엑셀이 계획을 돌려주지 않았습니다.", "닫기", tone: "bad");
                return;
            }
            if (plan.Count == 0)
            {
                WriteDialog.Ask("바꿀 것이 없습니다", plan.Summary.Length > 0 ? plan.Summary : "바뀌는 것이 없습니다.", "닫기", tone: "info");
                return;
            }
            if (WriteDialog.Ask($"{plan.Count:N0}건이 바뀝니다", WritePolicy.ConfirmBody(plan), "바꾸기", "취소") != WriteDialog.Choice.Ok)
            {
                _log.Write("write_declined", ("module", mv.Id), ("cmd", command), ("count", plan.Count));
                return;
            }
            var applied = await ApplyWrite(command, WritePolicy.Confirmed(argsJson), mv.Id);
            if (applied?.UndoCommand is { } undo && applied.UndoArgs is { } undoArgs
                && WriteDialog.Ask("끝났습니다", $"{applied.Message}\n\n바로 되돌릴 수 있습니다.", "닫기", undoText: "되돌리기", tone: "good") == WriteDialog.Choice.Undo)
            {
                var undone = await ApplyWrite(undo, WritePolicy.Confirmed(undoArgs), mv.Id);
                if (undone != null) WriteDialog.Ask("되돌렸습니다", undone.Message, "닫기", tone: "info");
            }
            else if (applied != null && applied.UndoCommand == null)
                WriteDialog.Ask("끝났습니다", applied.Message, "닫기", tone: "good");
        }
        finally { _writing = false; }
    }

    /// <summary>확인을 받은 쓰기 명령 하나를 보내고 결과를 읽는다. 실패하면 이유를 창으로 보이고 null.</summary>
    private async Task<WriteResult?> ApplyWrite(string command, string argsJson, string moduleId)
    {
        var call = await _registry.DoAsync(_target, command, argsJson);
        var resp = call.Ok ? BridgeResponse.Parse(call.Json) : BridgeResponse.Failed(call.ErrorCode, "엑셀 호출 실패");
        var result = WritePolicy.ReadResult(resp);
        _log.Write("write", ("module", moduleId), ("cmd", command), ("ok", resp.Ok), ("code", resp.Code), ("changed", result?.Changed), ("ms", Math.Round(call.Ms, 1)));
        if (result == null) WriteDialog.Ask("실패했습니다", resp.Error ?? "엑셀이 결과를 돌려주지 않았습니다.", "닫기", tone: "bad");
        return result;
    }
}
