// 기능: 조회 간격 판단(QueryPacer) 시험. 한 칸 고르기는 바로, 끄는 동안과 무거운 조회는 쉬어 가는지 본다.
// 사용법: dotnet test
// 의존: ClockOut.Hunsu.Core(QueryPacer)
using ClockOut.Hunsu.Core;

namespace ClockOut.Hunsu.Tests;

public class QueryPacerTests
{
    private static readonly QueryPacer P = QueryPacer.Default;

    [Fact]
    public void FirstQuery_RunsNow() => Assert.Equal(0, P.DelayMs(new PaceInput(1000, null, 0, false)));

    [Fact]
    public void FirstQuery_RunsNowEvenWhileDragging() => Assert.Equal(0, P.DelayMs(new PaceInput(1000, null, 0, true)));

    [Fact]
    public void LightQuery_NextSelectionRunsNow()
    {
        // 조회 3ms, 끝나고 10ms 뒤 다음 선택: 쉬는 시간 9ms를 이미 지났다.
        Assert.Equal(0, P.DelayMs(new PaceInput(110, 100, 3, false)));
    }

    [Fact]
    public void ShortWait_RunsNowInsteadOfTimer()
    {
        // 남은 대기 3ms(< 5ms)면 타이머를 쓰지 않는다.
        Assert.Equal(0, P.DelayMs(new PaceInput(106, 100, 3, false)));
    }

    [Fact]
    public void HeavyQuery_WaitsThreeTimesItsCost()
    {
        // 조회 400ms → 1200ms 쉰다. 끝나고 200ms 지났으면 1000ms 더.
        Assert.Equal(1000, P.DelayMs(new PaceInput(1200, 1000, 400, false)), 3);
    }

    [Fact]
    public void HeavyQuery_RunsAfterGap() => Assert.Equal(0, P.DelayMs(new PaceInput(2300, 1000, 400, false)));

    [Fact]
    public void Dragging_HeavyQuery_DefersUntilRelease()
    {
        // 무거운 조회는 끄는 동안 얼마가 지나도 미룬다(버튼 상태를 다시 본다).
        Assert.Equal(P.MousePollMs, P.DelayMs(new PaceInput(99_000, 1000, 40, true)));
    }

    [Fact]
    public void Dragging_LightQuery_ThrottledToFloor()
    {
        // 가벼운 조회(5ms)라도 끄는 동안은 100ms 간격.
        Assert.Equal(80, P.DelayMs(new PaceInput(120, 100, 5, true)), 3);
        Assert.Equal(0, P.DelayMs(new PaceInput(201, 100, 5, true)));
    }

    [Fact]
    public void Released_AfterHeavyDrag_UsesNormalGap()
    {
        // 버튼을 놓으면 보통 규칙: 조회 40ms → 120ms 쉼. 이미 지났으면 바로.
        Assert.Equal(0, P.DelayMs(new PaceInput(5000, 1000, 40, false)));
    }
}
