// 기능: 최소화 공용 동작(ShellController의 일부). 줄어든 모듈이 "일하는 중"이면 아이콘에 숨 쉬는 점을 보이고, form "mini" 모듈은 처음부터 줄여 둔다.
// 사용법: 생성 때 StartActivityWatch()를 한 번 부른다. 데이터가 오면 OnTopic이 NoteActivity(모듈)를 부른다. 모듈은 manifest에 "form": "mini"만 쓰면 된다.
// 의존: Core/Layout/Minimize(판단), Ui/Frame/ActivityDot(그림). 모듈마다 코드는 없다.
using System.Windows.Threading;
using ClockOut.Hunsu.Core.Layout;

namespace ClockOut.Hunsu.Shell;

internal sealed partial class ShellController
{
    private readonly ActivityTracker _activity = new();
    private DispatcherTimer? _activityTimer;
    private HashSet<string> _workingShown = new(StringComparer.Ordinal);

    private void StartActivityWatch()
    {
        _left.IsWorking = _right.IsWorking = IsModuleWorking;
        _activityTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => RefreshWorking(), System.Windows.Application.Current.Dispatcher);
        _activityTimer.Start();
    }

    /// <summary>줄어든(패널이 안 보이는) 모듈이 일하는 중인가: 셀 위에 표시를 그리고 있거나 방금 데이터를 받았다.</summary>
    private bool IsModuleWorking(ModuleView mv) =>
        !mv.Placement.Hidden && Minimize.IsWorking(!mv.ShowsPanel, mv.Module.View.Marks.Count > 0, _activity.IsRecent(mv.Id, DateTime.UtcNow));

    /// <summary>데이터가 온 모듈을 "방금 일했다"고 적는다. 줄어든 모듈이면 점이 바로 켜지도록 다시 그린다.</summary>
    private void NoteActivity(ModuleView mv)
    {
        _activity.Touch(mv.Id, DateTime.UtcNow);
        if (!mv.ShowsPanel) RefreshWorking();
    }

    /// <summary>일하는 모듈 모음이 바뀌었을 때만 런처와 빠른 띠를 다시 그린다(1초 점검, 같으면 아무것도 안 함).</summary>
    private void RefreshWorking()
    {
        var now = _modules.Where(IsModuleWorking).Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
        if (now.SetEquals(_workingShown)) return;
        _workingShown = now;
        _left.RefreshLauncher();
        _right.RefreshLauncher();
        UpdateQuickBar();
    }

    /// <summary>선언 form이 "mini"이고 아직 사용자가 자리를 정한 적 없는 모듈은 처음부터 줄여 둔다(한 번만). 사용자는 아이콘을 눌러 펼친다.</summary>
    private void ApplyMiniForms()
    {
        foreach (var mv in _modules)
        {
            if (!Minimize.StartsMinimized(mv.Module.Manifest.Form) || !mv.InDock || mv.Placement.Hidden || _formApplied.Contains(mv.Id) || _store.State.Modules.ContainsKey(mv.Id)) continue;
            _formApplied.Add(mv.Id);
            Save(mv, p => p with { Collapsed = true });
            mv.ApplyCollapsed(true, animate: false, done: null);
            _log.Write("form_mini", ("id", mv.Id));
        }
    }
}
