// 기능: ViewWatch(화면 자리가 움직였을 때만 다시 묻기) 시험. 같은 자리면 다리를 부르지 않아 엑셀 커서가 깜빡이지 않아야 한다.
// 사용법: dotnet test src/ClockOut.Hunsu.Tests
// 의존: Core/ViewWatch.
using ClockOut.Hunsu.Core;

namespace ClockOut.Hunsu.Tests;

public class ViewWatchTests
{
    [Fact]
    public void FirstKeyMovesThenSameKeyDoesNot()
    {
        var w = new ViewWatch();
        Assert.True(w.Moved("1|1|100|0|0|False|시트1"));
        for (int i = 0; i < 10; i++) Assert.False(w.Moved("1|1|100|0|0|False|시트1"));
        Assert.True(w.Moved("5|1|100|0|0|False|시트1"));
    }

    [Fact]
    public void NullKeyNeverMovesAndKeepsLast()
    {
        var w = new ViewWatch();
        Assert.False(w.Moved(null));
        Assert.True(w.Moved("a"));
        Assert.False(w.Moved(null));
        Assert.False(w.Moved("a"));
    }

    [Fact]
    public void ForgetMakesNextMove()
    {
        var w = new ViewWatch();
        w.Moved("a");
        w.Forget();
        Assert.True(w.Moved("a"));
    }
}
