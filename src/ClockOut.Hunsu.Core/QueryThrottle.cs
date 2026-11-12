// 기능: 엑셀 조회 사이에 얼마나 기다릴지 정한다(범위를 끄는 동안 엑셀이 버벅이던 문제, docs/06). 첫 조회는 바로, 이어지는 조회는 간격을 둔다.
// 사용법: int wait = QueryThrottle.DelayMs(nowMs, lastEndMs, lastDurMs, mouseDown, settings);  wait>0이면 그만큼 기다렸다가 가장 새 신호로 조회한다.
//   lastEndMs가 0 미만이면 아직 조회한 적 없음(기다리지 않음).
//   마우스를 누른 채: max(QueryDragGapMs, 직전 조회 시간 × 4).  평소: min(QueryMaxGapMs, 직전 조회 시간 × QueryGapFactor).
// 의존: Settings/HunsuSettings. 순수 계산(시계나 마우스를 직접 읽지 않음).
using ClockOut.Hunsu.Core.Settings;

namespace ClockOut.Hunsu.Core;

public static class QueryThrottle
{
    public static int DelayMs(double nowMs, double lastEndMs, double lastDurMs, bool mouseDown, HunsuSettings s)
    {
        if (lastEndMs < 0) return 0;
        double gap = mouseDown
            ? Math.Max(s.QueryDragGapMs, lastDurMs * 4)
            : Math.Min(s.QueryMaxGapMs, lastDurMs * s.QueryGapFactor);
        double wait = lastEndMs + gap - nowMs;
        return wait <= 0 ? 0 : (int)Math.Ceiling(wait);
    }
}
