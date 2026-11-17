// 기능: 남은 엑셀 찌꺼기 고르기 규칙 시험(창이 사라진 엑셀만, 기다린 뒤에, 한 번만).
// 사용법: dotnet test
// 의존: ClockOut.Hunsu.Core(OrphanTracker)
using ClockOut.Hunsu.Core.Procs;

namespace ClockOut.Hunsu.Tests;

public class OrphanTrackerTests
{
    private static readonly DateTime T0 = new(2026, 11, 17, 9, 0, 0, DateTimeKind.Utc);
    private static ExcelProc Shown(int pid = 10) => new(pid, 1, true, "통합 문서1 - Excel");
    private static ExcelProc Hidden(int pid = 10) => new(pid, 1, false, "");

    [Fact]
    public void Visible_Excel_IsNeverEnded()
    {
        var t = new OrphanTracker(TimeSpan.FromSeconds(15));
        Assert.Empty(t.Update([Shown()], T0));
        Assert.Empty(t.Update([Shown()], T0.AddMinutes(10)));
    }

    [Fact]
    public void WindowGone_EndsOnlyAfterGrace()
    {
        var t = new OrphanTracker(TimeSpan.FromSeconds(15));
        t.Update([Shown()], T0);
        Assert.Empty(t.Update([Hidden()], T0.AddSeconds(3)));
        Assert.Empty(t.Update([Hidden()], T0.AddSeconds(10)));
        var r = t.Update([Hidden()], T0.AddSeconds(19));
        Assert.Single(r);
        Assert.Equal(10, r[0].Pid);
    }

    [Fact]
    public void GivenOnce_NotAgain()
    {
        var t = new OrphanTracker(TimeSpan.FromSeconds(5));
        t.Update([Shown()], T0);
        t.Update([Hidden()], T0.AddSeconds(1));
        Assert.Single(t.Update([Hidden()], T0.AddSeconds(10)));
        Assert.Empty(t.Update([Hidden()], T0.AddSeconds(20)));
    }

    [Fact]
    public void NeverVisible_IsLeftAlone()
    {
        var t = new OrphanTracker(TimeSpan.FromSeconds(5));
        t.Update([Hidden(20)], T0);
        Assert.Empty(t.Update([Hidden(20)], T0.AddMinutes(5)));
    }

    [Fact]
    public void WindowComesBack_ResetsWait()
    {
        var t = new OrphanTracker(TimeSpan.FromSeconds(15));
        t.Update([Shown()], T0);
        t.Update([Hidden()], T0.AddSeconds(1));
        t.Update([Shown()], T0.AddSeconds(10));
        t.Update([Hidden()], T0.AddSeconds(12));
        Assert.Empty(t.Update([Hidden()], T0.AddSeconds(20)));
        Assert.Single(t.Update([Hidden()], T0.AddSeconds(30)));
    }

    [Fact]
    public void ReusedPid_WithOtherStart_IsNotTheOldOne()
    {
        var t = new OrphanTracker(TimeSpan.FromSeconds(5));
        t.Update([Shown()], T0);
        t.Update([], T0.AddSeconds(2));
        var other = new ExcelProc(10, 2, false, "");
        t.Update([other], T0.AddSeconds(3));
        Assert.Empty(t.Update([other], T0.AddSeconds(30)));
    }
}
