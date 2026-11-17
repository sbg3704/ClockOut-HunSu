// 기능: "엑셀을 닫았는데 EXCEL.EXE가 작업 관리자에 남는" 찌꺼기를 고른다. 순수 계산이라 프로세스·창 없이 시험된다.
//       규칙: 화면에 보이는 엑셀 주 창이 있던 프로세스가 창이 없어진 채 grace 이상 남아 있으면 찌꺼기다.
//       처음부터 창이 없던 엑셀(다른 프로그램이 숨겨 쓰는 것)은 자동으로 끝내지 않는다(목록에만 보이고 사용자가 직접 끝낸다).
// 사용법: var t = new OrphanTracker(TimeSpan.FromSeconds(15)); var 끝낼것 = t.Update(지금_엑셀_목록, DateTime.UtcNow);  (몇 초마다 부른다)
// 의존: 없음. 프로세스 번호가 다시 쓰이는 일을 막으려고 (번호, 시작 시각)을 한 묶음으로 본다.
namespace ClockOut.Hunsu.Core.Procs;

/// <summary>실행 중인 EXCEL.EXE 하나의 겉모습. HasVisibleWindow = 화면에 보이는 XLMAIN 창이 있는가.</summary>
public sealed record ExcelProc(int Pid, long StartTicks, bool HasVisibleWindow, string Title);

public sealed class OrphanTracker
{
    private readonly HashSet<(int, long)> _seenVisible = [];
    private readonly Dictionary<(int, long), DateTime> _windowlessSince = [];
    private readonly HashSet<(int, long)> _given = [];

    public OrphanTracker(TimeSpan grace) => Grace = grace;

    /// <summary>창이 없어진 뒤 이만큼 기다렸다가 끝낸다(엑셀이 닫히는 중이거나 잠깐 숨은 경우를 건드리지 않으려고).</summary>
    public TimeSpan Grace { get; }

    /// <summary>지금 목록을 보고 끝내도 되는 찌꺼기를 돌려준다. 한 프로세스는 한 번만 돌려준다(끝내기에 실패해도 되풀이하지 않음).</summary>
    public IReadOnlyList<ExcelProc> Update(IReadOnlyCollection<ExcelProc> now, DateTime nowUtc)
    {
        var alive = new HashSet<(int, long)>(now.Select(p => (p.Pid, p.StartTicks)));
        _seenVisible.RemoveWhere(k => !alive.Contains(k));
        _given.RemoveWhere(k => !alive.Contains(k));
        foreach (var k in _windowlessSince.Keys.Where(k => !alive.Contains(k)).ToList()) _windowlessSince.Remove(k);

        var result = new List<ExcelProc>();
        foreach (var p in now)
        {
            var key = (p.Pid, p.StartTicks);
            if (p.HasVisibleWindow)
            {
                _seenVisible.Add(key);
                _windowlessSince.Remove(key);
                continue;
            }
            if (!_seenVisible.Contains(key) || _given.Contains(key)) continue;
            if (!_windowlessSince.TryGetValue(key, out var since))
            {
                _windowlessSince[key] = nowUtc;
                continue;
            }
            if (nowUtc - since < Grace) continue;
            _given.Add(key);
            result.Add(p);
        }
        return result;
    }
}
