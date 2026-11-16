// 기능: "최소화" 공용 판단. 패널을 아이콘으로 줄여도 모듈은 계속 일한다. 줄어든 모듈이 "일하는 중"인지(깜빡일지)와, 선언(form "mini")으로 처음부터 줄여 둘지를 정한다.
// 사용법: 모듈은 manifest에 "form": "mini"만 쓰면 된다(코드 없음). 셸은 데이터가 올 때 tracker.Touch(id, 지금)을, 아이콘을 그릴 때 Minimize.IsWorking(...)을 부른다.
// 의존: 없음(순수 계산, 화면·엑셀 없이 시험된다). 그리기는 Ui/Frame/ActivityDot.
namespace ClockOut.Hunsu.Core.Layout;

/// <summary>최근에 데이터를 받은 모듈을 기억한다(받은 지 Window 안이면 "방금 일했다").</summary>
public sealed class ActivityTracker
{
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromSeconds(4);
    private readonly Dictionary<string, DateTime> _last = new(StringComparer.Ordinal);
    private readonly TimeSpan _window;

    public ActivityTracker(TimeSpan? window = null) => _window = window ?? DefaultWindow;

    /// <summary>모듈이 방금 일했다고 적는다.</summary>
    public void Touch(string id, DateTime now) => _last[id] = now;

    public bool IsRecent(string id, DateTime now) => _last.TryGetValue(id, out var t) && now - t <= _window && now >= t;

    /// <summary>지금 "방금 일한" 모듈 id 모음(상태 비교용).</summary>
    public IReadOnlySet<string> Recent(DateTime now) => _last.Where(kv => now - kv.Value <= _window && now >= kv.Value).Select(kv => kv.Key).ToHashSet(StringComparer.Ordinal);
}

public static class Minimize
{
    /// <summary>선언 form이 처음부터 줄여 두는 모양인가.</summary>
    public static bool StartsMinimized(string form) => form == "mini";

    /// <summary>줄어든(패널을 안 보여 주는) 모듈이 일하는 중인가: 셀 위에 표시를 그리고 있거나, 방금 데이터를 받았다.</summary>
    public static bool IsWorking(bool minimized, bool drawsMarks, bool recentData) => minimized && (drawsMarks || recentData);
}
