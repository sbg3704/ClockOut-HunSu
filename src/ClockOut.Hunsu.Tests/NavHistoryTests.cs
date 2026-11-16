// 기능: 최근 이동 기록(NavHistory) 시험: 쌓기, 끌기 합치기, 뒤로·앞으로, 되돌아온 선택 무시, 같은 칸 한 번만, 책 거르기, 상한.
// 사용법: dotnet test
// 의존: ClockOut.Hunsu.Core(Nav)
using System.Text.Json;
using ClockOut.Hunsu.Core.Nav;

namespace ClockOut.Hunsu.Tests;

public class NavHistoryTests
{
    private static NavHistory Three()
    {
        var h = new NavHistory(settleMs: 1000);
        h.Observe("b", "S1", "$A$1", 0);
        h.Observe("b", "S1", "$B$2", 2000);
        h.Observe("b", "S2", "$C$3", 4000);
        return h;
    }

    [Fact]
    public void Observe_Stacks_AndIgnoresSamePlace()
    {
        var h = Three();
        Assert.Equal(3, h.Count);
        Assert.False(h.Observe("b", "S2", "$C$3", 9000));
        Assert.Equal("$C$3", h.Current!.Addr);
    }

    [Fact]
    public void QuickChanges_MergeIntoLastRow()
    {
        var h = new NavHistory(settleMs: 1000);
        h.Observe("b", "S", "$A$1", 0);
        h.Observe("b", "S", "$A$1:$A$2", 2000);
        h.Observe("b", "S", "$A$1:$A$3", 2100);
        h.Observe("b", "S", "$A$1:$A$9", 2200);
        Assert.Equal(2, h.Count);
        Assert.Equal("$A$1:$A$9", h.Current!.Addr);
    }

    [Fact]
    public void Back_Forward_AndReturningSelectionIsNotRecorded()
    {
        var h = Three();
        var e = h.Back();
        Assert.Equal("$B$2", e!.Addr);
        Assert.False(h.Observe("b", "S1", "$B$2", 5000)); // goto가 일으킨 선택
        Assert.Equal(3, h.Count);
        Assert.True(h.CanForward);
        Assert.Equal("$C$3", h.Forward()!.Addr);
        Assert.Null(h.Forward());
    }

    [Fact]
    public void NewMove_AfterBack_DropsForwardRows()
    {
        var h = Three();
        h.Back();
        h.Observe("b", "S1", "$B$2", 5000);
        h.Observe("b", "S1", "$Z$9", 8000);
        Assert.Equal(3, h.Count);
        Assert.False(h.CanForward);
        Assert.Equal("$Z$9", h.Current!.Addr);
    }

    [Fact]
    public void Revisit_MovesOldRowToEnd()
    {
        var h = Three();
        h.Observe("b", "S1", "$A$1", 9000);
        Assert.Equal(3, h.Count);
        Assert.Equal("$A$1", h.Current!.Addr);
    }

    [Fact]
    public void Capacity_DropsOldest()
    {
        var h = new NavHistory(capacity: 3, settleMs: 0);
        for (int i = 0; i < 6; i++) h.Observe("b", "S", $"$A${i + 1}", i * 10);
        Assert.Equal(3, h.Count);
        Assert.Equal("$A$6", h.Current!.Addr);
    }

    [Fact]
    public void ToJson_NewestFirst_FiltersBook_MarksCurrent()
    {
        var h = Three();
        h.Observe("other", "X", "$A$1", 9000);
        using var doc = JsonDocument.Parse(h.ToJson("b", 10_000));
        var items = doc.RootElement.GetProperty("items");
        Assert.Equal(3, items.GetArrayLength());
        Assert.Equal("S2!C3", items[0].GetProperty("label").GetString());
        Assert.False(items[0].GetProperty("current").GetBoolean()); // 지금 자리는 다른 책
        Assert.Equal(3, doc.RootElement.GetProperty("count").GetInt32());
        Assert.True(doc.RootElement.GetProperty("canBack").GetBoolean());
    }

    [Fact]
    public void Jump_MovesIndex_WithoutReordering()
    {
        var h = Three();
        Assert.Equal("$A$1", h.Jump(0)!.Addr);
        Assert.False(h.Observe("b", "S1", "$A$1", 5000));
        Assert.Equal(0, h.Index);
        Assert.Null(h.Jump(7));
    }

    [Theory]
    [InlineData(0, "방금")]
    [InlineData(15_000, "15초 전")]
    [InlineData(120_000, "2분 전")]
    [InlineData(7_200_000, "2시간 전")]
    public void Ago_Text(long ms, string text) => Assert.Equal(text, NavHistory.Ago(ms));
}

public class AppTopicRoutingTests
{
    [Fact]
    public void AppTopics_AreNeverAskedOfBridge()
    {
        var subs = new[]
        {
            new ClockOut.Hunsu.Core.Manifest.DataSubscription("selection.summary", ["sel"], "light"),
            new ClockOut.Hunsu.Core.Manifest.DataSubscription("app.history", ["sel"], "light"),
        };
        Assert.Equal(["selection.summary"], ClockOut.Hunsu.Core.TopicRouting.TopicsFor(ClockOut.Hunsu.Core.SignalCodec.Sel, subs));
        Assert.Equal(["selection.summary"], ClockOut.Hunsu.Core.TopicRouting.AllTopics(subs));
    }
}
