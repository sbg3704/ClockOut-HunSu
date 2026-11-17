// 기능: 남은 엑셀 정리(ShellController의 일부). 3초마다 ① 창이 없어진 엑셀 인스턴스의 COM 참조를 놓고(그래야 닫은 엑셀이 스스로 끝난다) ② 그래도 창 없이 남은 EXCEL.EXE 찌꺼기를 기다린 뒤 끝낸다.
//       ②는 "화면에 보이던 엑셀이 창이 없어진 채 15초 넘게 남은 것"만 자동으로 끝낸다. 처음부터 창이 없던 엑셀은 건드리지 않는다(수동 창에서만).
// 사용법: Start()가 StartExcelCleanup()을 부른다. 끄기: settings.json cleanOrphanExcel=false(설정 창 [일반]). 수동: OpenExcelCleanup()(훈 원 우클릭·알림 아이콘·설정 창).
// 의존: Excel/InstanceRegistry(ReleaseGone), Excel/ExcelProcesses, Core/Procs/OrphanTracker, Shell/ExcelCleanupWindow.
using System.Windows;
using System.Windows.Threading;
using ClockOut.Hunsu.Core.Procs;
using ClockOut.Hunsu.Excel;

namespace ClockOut.Hunsu.Shell;

internal sealed partial class ShellController
{
    private static readonly TimeSpan OrphanGrace = TimeSpan.FromSeconds(15);
    private readonly OrphanTracker _orphans = new(OrphanGrace);
    private DispatcherTimer? _cleanupTimer;
    private ExcelCleanupWindow? _cleanupWindow;

    private void StartExcelCleanup()
    {
        _cleanupTimer = new DispatcherTimer(TimeSpan.FromSeconds(3), DispatcherPriority.Background, (_, _) => CleanupTick(), Application.Current.Dispatcher);
        _cleanupTimer.Start();
    }

    private void CleanupTick()
    {
        _registry.ReleaseGone();
        if (!_settings.CleanOrphanExcel) return;
        var now = ExcelProcesses.Snapshot();
        foreach (var p in _orphans.Update(now, DateTime.UtcNow))
        {
            bool ok = ExcelProcesses.End(p.Pid, p.StartTicks);
            _log.Write("excel_end", ("pid", p.Pid), ("visible", false), ("manual", false), ("ok", ok));
            if (ok) _tray.Balloon("닫은 엑셀이 남아 있어 정리했습니다.");
        }
    }

    /// <summary>[남은 엑셀 정리] 창을 연다(이미 열려 있으면 앞으로).</summary>
    internal void OpenExcelCleanup()
    {
        if (_cleanupWindow != null) { _cleanupWindow.Activate(); return; }
        _cleanupWindow = new ExcelCleanupWindow(_log);
        _cleanupWindow.Closed += (_, _) => _cleanupWindow = null;
        _cleanupWindow.Show();
    }
}
