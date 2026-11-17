// 기능: 따라가는 엑셀 창에서 다리(clockout-hunsu.xlam)를 부르지 못할 때(다리가 안 열린 엑셀, 매크로가 막힌 엑셀) 이유를 작업 기록에 남기고, 알림 영역 풍선으로 한 번만 알린다(ShellController의 일부).
//       전에는 이때 패널이 그냥 빈 채로 있어 "이 파일에서만 반응이 없다"로 보였다(2026-11-17 확인용 엑셀 신고). 엑셀 안으로 들어가 다리를 억지로 열지는 않는다(사용자 엑셀은 손대지 않는 규칙).
// 사용법: OnTopic이 조회 실패마다 NoteBridgeProblem(결과)을 부른다. 같은 엑셀 창·같은 오류는 한 번만 기록하고, 풍선은 엑셀 창당 한 번.
// 의존: Excel/InstanceRegistry(TopicResult), Core/HResultNames, Shell/TrayIcon(Balloon), Diagnostics/RunLog.
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Excel;

namespace ClockOut.Hunsu.Shell;

internal sealed partial class ShellController
{
    private readonly HashSet<string> _toldProblems = [];
    private readonly HashSet<IntPtr> _toldBridgeMissing = [];

    private void NoteBridgeProblem(TopicResult r)
    {
        string code = r.Response.Code ?? "";
        if (_toldProblems.Add(r.Excel.ToInt64() + "|" + code))
            _log.Write("topic_failed", ("excel", r.Excel.ToInt64()), ("topic", r.Topic), ("code", code), ("hr", HResultNames.Name(r.Call.HResult)), ("attach", r.Call.Attach));
        if (code != "bridge_missing" || r.Excel != _target || !_toldBridgeMissing.Add(r.Excel)) return;
        _tray.Balloon("이 엑셀 창에는 훈수꾼 다리가 없거나 매크로가 막혀 있어 내용을 못 읽습니다. 알림 영역 훈수꾼 아이콘 우클릭 → 「엑셀 연결…」에서 「이 엑셀 점검」과 「브리지 열기」를 쓰거나, 이 파일을 다리가 열린 엑셀 창에서 여세요.");
    }
}
