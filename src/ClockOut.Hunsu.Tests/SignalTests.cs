// 기능: 신호 번호 묶기·풀기와 8비트 순번 걸러내기 시험.
// 사용법: dotnet test
// 의존: ClockOut.Hunsu.Core(SignalCodec, SeqTracker)
using ClockOut.Hunsu.Core;

namespace ClockOut.Hunsu.Tests;

public class SignalTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 255)]
    [InlineData(6, 128)]
    public void EncodeDecode_RoundTrips(int code, int seq)
    {
        var (c, s) = SignalCodec.Decode(SignalCodec.Encode(code, seq));
        Assert.Equal(code, c);
        Assert.Equal(seq, s);
    }

    [Fact]
    public void Decode_MatchesBridgeLayout()
    {
        // 다리는 code Or (seq * 256)으로 보낸다.
        Assert.Equal((1, 3), SignalCodec.Decode(1 + 3 * 256));
        Assert.Equal("sel", SignalCodec.NameOf(1));
        Assert.Equal("unknown", SignalCodec.NameOf(99));
    }

    [Fact]
    public void SeqTracker_AcceptsNewer_AndWrapsAround()
    {
        var t = new SeqTracker();
        Assert.True(t.Accept(254));
        Assert.True(t.Accept(255));
        Assert.True(t.Accept(0));
        Assert.True(t.Accept(1));
        Assert.False(t.Accept(1));
        Assert.False(t.Accept(255));
        Assert.Equal(2, t.Dropped);
    }

    [Fact]
    public void SeqTracker_IsNewer_HalfWindow()
    {
        Assert.True(SeqTracker.IsNewer(10, 137));
        Assert.False(SeqTracker.IsNewer(10, 138));
    }
}
