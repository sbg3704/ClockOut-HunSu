// 기능: 엑셀 조회를 언제 다시 할지 정한다. 범위를 끌거나 Shift+방향키를 누르고 있으면 선택 신호가 쉬지 않고 오는데,
//       그때마다 조회하면 엑셀 화면 스레드가 Application.Run에 묶여 버벅인다(docs/06). 그래서
//       ① 지난 조회 시간의 몇 배만큼 쉬고 나서 다음 조회를 하고(조회가 무거울수록 더 쉰다)
//       ② 마우스 왼쪽 버튼이 눌린 동안은 조회가 무거우면 놓을 때까지 미루고, 가벼우면 간격만 넓힌다.
//       한 칸씩 고를 때는 바로 조회하므로 "선택→화면 50ms"(docs/04 1장)는 그대로다.
// 사용법: double wait = QueryPacer.Default.DelayMs(new PaceInput(nowMs, lastEndMs, lastQueryMs, mouseDown)); 0이면 지금, 아니면 그만큼 기다렸다 다시 묻는다.
// 의존: 없음(순수 계산). 시각은 모두 같은 시계의 ms.
namespace ClockOut.Hunsu.Core;

/// <summary>조회 간격 판단에 쓰는 지금 상태. LastEndMs가 null이면 아직 조회한 적 없음.</summary>
public readonly record struct PaceInput(double NowMs, double? LastEndMs, double LastQueryMs, bool MouseDown);

public sealed class QueryPacer
{
    /// <summary>쉬는 시간 = 지난 조회 시간 × 이 값. 3이면 엑셀 화면 스레드를 많아야 1/4만 쓴다.</summary>
    public double GapFactor { get; init; } = 3;

    /// <summary>마우스로 끄는 동안 조회 사이 최소 간격.</summary>
    public double DragFloorMs { get; init; } = 100;

    /// <summary>조회가 이보다 오래 걸리면 끄는 동안은 아예 미룬다(버튼을 놓으면 한 번).</summary>
    public double HeavyQueryMs { get; init; } = 30;

    /// <summary>미루는 동안 버튼 상태를 다시 보는 간격.</summary>
    public double MousePollMs { get; init; } = 30;

    /// <summary>이보다 짧게 기다려야 하면 그냥 지금 한다(타이머 해상도가 약 15ms라 짧은 대기는 오히려 늦다).</summary>
    public double RunNowBelowMs { get; init; } = 5;

    public static QueryPacer Default { get; } = new();

    /// <summary>0이면 지금 조회, 양수면 그만큼(ms) 기다렸다가 다시 물어본다.</summary>
    public double DelayMs(PaceInput s)
    {
        if (s.LastEndMs is not { } lastEnd) return 0;
        double last = Math.Max(0, s.LastQueryMs);
        if (s.MouseDown && last >= HeavyQueryMs) return MousePollMs;
        double gap = last * GapFactor;
        if (s.MouseDown) gap = Math.Max(gap, DragFloorMs);
        double wait = lastEnd + gap - s.NowMs;
        return wait < RunNowBelowMs ? 0 : wait;
    }
}
