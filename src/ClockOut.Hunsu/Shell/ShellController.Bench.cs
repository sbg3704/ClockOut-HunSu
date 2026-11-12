// 기능: 측정 도구 메시지 처리(ShellController의 일부). 패널 펼치기·접기, 도크 ↔ 핀 창, hello 호출, 끝내기. 키보드·마우스 대신 쓴다.
// 사용법: SignalWindow.Bench 이벤트로 불린다. 기록 줄: panel(sent recv first done), pin/unpin(sent recv done maxGap skipped frames), probe.
// 의존: Core/BenchMessages, Diagnostics/Frames. 보통 사용 때는 아무도 이 메시지를 보내지 않는다.
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Diagnostics;

namespace ClockOut.Hunsu.Shell;

internal sealed partial class ShellController
{
    private ModuleView? First => _modules.Count > 0 ? _modules[0] : null;

    private async void OnBench(string kind, IntPtr wParam, IntPtr lParam, long at)
    {
        var first = First;
        switch (kind)
        {
            case BenchMessages.OpenPanel when first != null:
                long firstFrame = 0;
                Frames.Next(t => firstFrame = t);
                var probe = Frames.StartProbe();
                SetCollapsed(first, false, done =>
                {
                    double mean = probe.MeanGapMs;
                    var (gap, steady, skipped, frames) = probe.Stop();
                    _log.Write("panel", ("sent", lParam.ToInt64()), ("recv", at), ("first", firstFrame), ("done", done), ("maxGap", gap), ("steadyGap", steady), ("meanGap", Math.Round(mean, 2)), ("skipped", skipped), ("frames", frames));
                });
                break;
            case BenchMessages.ClosePanel when first != null:
                SetCollapsed(first, true, null);
                break;
            case BenchMessages.TogglePin when first != null:
                bool pinning = first.InDock;
                var pinProbe = Frames.StartProbe();
                void Done(long done)
                {
                    double mean = pinProbe.MeanGapMs;
                    var (gap, steady, skipped, frames) = pinProbe.Stop();
                    _log.Write(pinning ? "pin" : "unpin", ("sent", lParam.ToInt64()), ("recv", at), ("done", done), ("maxGap", gap), ("steadyGap", steady), ("meanGap", Math.Round(mean, 2)), ("skipped", skipped), ("frames", frames),
                        ("pinHwnd", _pins.TryGetValue(first.Id, out var w) ? w.Handle.ToInt64() : 0), ("rect", w?.Rect.ToString() ?? "-"));
                }
                if (pinning) Pin(first, Done);
                else Unpin(first, Done);
                break;
            case BenchMessages.Probe:
                var call = await _registry.ProbeAsync(wParam);
                _log.Write("probe", ("excel", wParam.ToInt64()), ("sent", lParam.ToInt64()), ("recv", at), ("ok", call.Ok), ("hr", HResultNames.Name(call.HResult)),
                    ("code", call.ErrorCode), ("retries", call.Retries), ("reject", call.RejectType), ("ms", Math.Round(call.Ms, 2)), ("attach", call.Attach));
                break;
            case BenchMessages.Quit:
                Quit();
                break;
        }
    }
}
