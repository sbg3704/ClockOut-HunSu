// 기능: 8비트 순번(0~255에서 되돌아감)으로 낡은 신호를 걸러 낸다(docs/02 4장 "틀도 순번을 보고 낡은 것은 버린다").
// 사용법: var t = new SeqTracker(); if (t.Accept(seq)) { ...처리... }
// 의존: 없음(순수 계산). 앞선 것보다 1~127 앞서면 새것, 그 밖은 낡은 것(또는 중복)으로 본다.
namespace ClockOut.Hunsu.Core;

public sealed class SeqTracker
{
    private int _last = -1;

    public int Dropped { get; private set; }

    public bool Accept(int seq)
    {
        seq &= 0xFF;
        if (_last < 0 || IsNewer(_last, seq))
        {
            _last = seq;
            return true;
        }
        Dropped++;
        return false;
    }

    public static bool IsNewer(int previous, int next)
    {
        int diff = (next - previous) & 0xFF;
        return diff is >= 1 and <= 127;
    }
}
