// 기능: 엑셀이 호출을 거절할 때(편집 중, 바쁨) 다시 시도할지와 얼마나 기다릴지 정한다(docs/02 3장: 50ms 간격, 최대 5회).
// 사용법: int wait = policy.NextDelayMs(retriesSoFar); if (wait < 0) 포기; else wait ms 뒤 다시
// 의존: 없음(순수 계산). 메시지 필터(앱 Excel/OleMessageFilter)와 관리 코드 재시도가 같은 값을 쓴다.
namespace ClockOut.Hunsu.Core;

public sealed record RetryPolicy(int IntervalMs = 50, int MaxRetries = 5)
{
    public static RetryPolicy Default { get; } = new();

    /// <summary>지금까지 다시 시도한 횟수를 받아 다음 대기 시간(ms)을, 포기하면 -1을 준다.</summary>
    public int NextDelayMs(int retriesSoFar) => retriesSoFar < MaxRetries ? IntervalMs : -1;

    /// <summary>포기하기까지 걸릴 수 있는 가장 긴 대기 합(호출 시간은 빼고).</summary>
    public int WorstCaseWaitMs => IntervalMs * MaxRetries;
}
