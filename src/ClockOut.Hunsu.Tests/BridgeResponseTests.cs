// 기능: 다리 응답 읽기와 선택 요약 글 만들기 시험.
// 사용법: dotnet test
// 의존: ClockOut.Hunsu.Core(BridgeResponse, SelectionSummary)
using ClockOut.Hunsu.Core;

namespace ClockOut.Hunsu.Tests;

public class BridgeResponseTests
{
    private const string SummaryJson = "{\"ok\":true,\"proto\":0,\"data\":{\"addr\":\"$B$2:$C$3\",\"sheet\":\"S\",\"book\":\"B\",\"count\":4,\"nonEmpty\":3,\"sum\":1234.5,\"avg\":617.25,\"min\":0.5,\"max\":1234,\"errors\":1,\"types\":{\"num\":2,\"text\":0,\"formula\":1}}}";

    [Fact]
    public void Parse_Ok()
    {
        var r = BridgeResponse.Parse(SummaryJson);
        Assert.True(r.Ok);
        Assert.Equal(0, r.Proto);
        var s = SelectionSummary.From(r.Data);
        Assert.NotNull(s);
        Assert.Equal("$B$2:$C$3", s!.Addr);
        Assert.Equal(4, s.Count);
        Assert.Equal(2, s.Numbers);
        Assert.Equal(1, s.Formulas);
        Assert.Equal(1234.5, s.Sum);
        Assert.Equal("합 1,234.5 · 평균 617.25 · 개수 3", s.ShortText());
    }

    [Fact]
    public void Parse_Fail()
    {
        var r = BridgeResponse.Parse("{\"ok\":false,\"proto\":0,\"code\":\"query_unknown\",\"error\":\"없음\"}");
        Assert.False(r.Ok);
        Assert.Equal("query_unknown", r.Code);
        Assert.Equal("없음", r.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    public void Parse_Bad_NeverThrows(string? json)
    {
        var r = BridgeResponse.Parse(json);
        Assert.False(r.Ok);
        Assert.Equal("query_failed", r.Code);
    }

    [Fact]
    public void Summary_TextOnly_ShowsCount()
    {
        var r = BridgeResponse.Parse("{\"ok\":true,\"proto\":0,\"data\":{\"addr\":\"$A$1\",\"count\":1,\"nonEmpty\":1,\"sum\":null,\"avg\":null,\"min\":null,\"max\":null,\"errors\":0,\"types\":{\"num\":0,\"text\":1,\"formula\":0}}}");
        var s = SelectionSummary.From(r.Data)!;
        Assert.Null(s.Sum);
        Assert.Equal("개수 1", s.ShortText());
    }
}
