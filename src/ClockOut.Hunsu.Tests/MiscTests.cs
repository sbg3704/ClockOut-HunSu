// 기능: 재시도 규칙, HRESULT 이름, 측정 통계, 한 줄 기록, 실행 인자 시험.
// 사용법: dotnet test
// 의존: ClockOut.Hunsu.Core
using ClockOut.Hunsu.Core;

namespace ClockOut.Hunsu.Tests;

public class MiscTests
{
    [Fact]
    public void RetryPolicy_FiveTimesFiftyMs()
    {
        var p = RetryPolicy.Default;
        for (int i = 0; i < 5; i++) Assert.Equal(50, p.NextDelayMs(i));
        Assert.Equal(-1, p.NextDelayMs(5));
        Assert.Equal(250, p.WorstCaseWaitMs);
    }

    [Fact]
    public void HResult_NamesAndClasses()
    {
        Assert.Equal("RPC_E_CALL_REJECTED", HResultNames.Name(unchecked((int)0x80010001)));
        Assert.True(HResultNames.IsBusy(HResultNames.RpcServerCallRetryLater));
        Assert.True(HResultNames.IsGone(HResultNames.RpcServerUnavailable));
        Assert.Equal("excel_busy", HResultNames.ToErrorCode(HResultNames.ExcelVbaIgnore));
        Assert.Equal("bridge_missing", HResultNames.ToErrorCode(HResultNames.ExcelRunFailed)); // 이 엑셀 창에 다리가 없음(매크로를 못 찾음)
        Assert.Equal("EXCEL_1004", HResultNames.Name(HResultNames.ExcelRunFailed));
        Assert.Equal("0x80004005", HResultNames.Name(unchecked((int)0x80004005)));
    }

    [Fact]
    public void LatencyStats_P95NearestRank()
    {
        var s = LatencyStats.From(Enumerable.Range(1, 100).Select(i => (double)i));
        Assert.Equal(100, s.Count);
        Assert.Equal(95, s.P95);
        Assert.Equal(100, s.Max);
        Assert.Equal(50.5, s.Mean, 3);
        Assert.True(s.Meets(95));
        Assert.False(s.Meets(94));
        Assert.Equal(0, LatencyStats.From([]).Count);
    }

    [Fact]
    public void LogLine_RoundTrip()
    {
        string text = LogLine.Format("render", ("seq", 3), ("t", 123456789L), ("ms", 1.5), ("addr", "$A$1 x"), ("n", null));
        Assert.Equal("render seq=3 t=123456789 ms=1.5 addr=$A$1_x n=-", text);
        var l = LogLine.Parse(text)!;
        Assert.Equal("render", l.Name);
        Assert.Equal(123456789L, l.Long("t"));
        Assert.Equal(1.5, l.Double("ms"));
        Assert.Null(l.Get("none"));
    }

    [Fact]
    public void AppOptions_Parse()
    {
        var o = AppOptions.Parse(["--follow", "0x1A2B", "--log", "a.log", "--layout", "l.json", "--bogus"]);
        Assert.Equal("l.json", o.LayoutPath);
        Assert.Equal(0x1A2B, o.FollowHwnd);
        Assert.Equal("a.log", o.LogPath);
        Assert.Equal(["--bogus"], o.Errors);
        Assert.Equal(0, AppOptions.Parse([]).FollowHwnd);
        Assert.False(o.OpenSettings);
    }

    // 칼퇴 버튼 탭(칼퇴 docs/11 4장): --settings 뒤에 쪽 이름을 붙이면 그 쪽을 연다. 모르는 낱말은 쪽으로 먹지 않는다.
    [Fact]
    public void AppOptions_SettingsPage()
    {
        Assert.Equal("general", AppOptions.Parse(["--settings"]).SettingsPage);
        var look = AppOptions.Parse(["--settings", "look"]);
        Assert.True(look.OpenSettings);
        Assert.Equal("look", look.SettingsPage);
        Assert.Empty(look.Errors);
        var bad = AppOptions.Parse(["--settings", "nope"]);
        Assert.Equal("general", bad.SettingsPage);
        Assert.Equal(["nope"], bad.Errors);
        Assert.Equal("wizard", AppOptions.Parse(["--settings", "wizard", "--log", "a.log"]).SettingsPage);
    }

    [Fact]
    public void SettingsPages_CodeRoundTrip_ZeroIsGeneral()
    {
        Assert.Equal("general", SettingsPages.Name(0)); // 옛 칼퇴는 wParam 0으로 보낸다
        Assert.Equal("general", SettingsPages.Name(99));
        Assert.Equal("general", SettingsPages.Name(-1));
        Assert.Equal(0, SettingsPages.Code("모름"));
        foreach (var p in SettingsPages.All) Assert.Equal(p, SettingsPages.Name(SettingsPages.Code(p)));
    }

    [Fact]
    public void AppOptions_Settings_IsNormalLaunch()
    {
        var o = AppOptions.Parse([AppMessages.SettingsArg]);
        Assert.True(o.OpenSettings);
        Assert.Empty(o.Errors);
        Assert.True(SingleInstance.IsNormalLaunch(o)); // 설정으로 켜도 단일 실행 잠금을 탄다
    }
}
