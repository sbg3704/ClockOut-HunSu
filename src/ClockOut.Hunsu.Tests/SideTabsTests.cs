// 기능: 사이드바 묶음 탭 계산(SideTabs)과 사이드바 기본값 안내(WithSidebarDefault), 배치 저장의 탭·높이 시험.
// 사용법: dotnet test
// 의존: ClockOut.Hunsu.Core(Layout, Settings)
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Core.Settings;

namespace ClockOut.Hunsu.Tests;

public class SideTabsTests
{
    private static readonly SideTabModule[] Mods =
    [
        new("sel", "선택 요약", 100, "sum"),
        new("color", "색깔별 합계", 150, "brush"),
        new("err", "오류", 200, "audit"),
        new("flow", "수식 흐름", 400, "flow"),
    ];

    private static IReadOnlyList<SideTab> Default() => SideTabs.Build(Mods, null);

    [Fact]
    public void Build_GroupsByOrderRange_SingleTabUsesModuleIcon()
    {
        var t = Default();
        Assert.Equal(["status", "check", "formula"], t.Select(x => x.Key));
        Assert.Equal(["sel", "color"], t[0].Members);
        Assert.Equal("sum", t[0].Icon);           // 여럿이면 묶음 아이콘
        Assert.Equal("audit", t[1].Icon);         // 하나면 그 모듈 아이콘
        Assert.Equal("상태", t[0].Name);
    }

    [Fact]
    public void MoveModule_ToOtherTab_BeforeTarget_DropsEmptyTab()
    {
        var t = SideTabs.MoveModule(Default(), "err", "status", before: "color");
        Assert.Equal(["status", "formula"], t.Select(x => x.Key));
        Assert.Equal(["sel", "err", "color"], t[0].Members);
    }

    [Fact]
    public void MoveTab_AndMergeTab()
    {
        var t = SideTabs.MoveTab(Default(), "formula", before: "status");
        Assert.Equal(["formula", "status", "check"], t.Select(x => x.Key));
        var m = SideTabs.MergeTab(t, "check", "formula");
        Assert.Equal(["formula", "status"], m.Select(x => x.Key));
        Assert.Equal(["flow", "err"], m[0].Members);
    }

    [Fact]
    public void NewTab_PullsModuleOut_WithFreshKey()
    {
        var t = SideTabs.NewTab(Default(), "color");
        Assert.Equal(["status", "check", "formula", "my-1"], t.Select(x => x.Key));
        Assert.Equal(["sel"], t[0].Members);
        Assert.Equal("내 묶음 1", t[3].Name);
        // 이미 혼자인 탭은 새로 만들지 않고 순서만
        var u = SideTabs.NewTab(t, "flow", before: "status");
        Assert.Equal(["formula", "status", "check", "my-1"], u.Select(x => x.Key));
    }

    [Fact]
    public void Build_FollowsSaved_NewModulesGoToDefaultTab()
    {
        var saved = SideTabs.Persist(null, SideTabs.MergeTab(Default(), "status", "formula"));
        var more = Mods.Append(new SideTabModule("sheets", "시트 이동", 300, "sheets")).Append(new SideTabModule("sel2", "또 요약", 120, "sum"));
        var t = SideTabs.Build(more, saved);
        // 합친 탭(formula)이 상태 묶음을 가져갔으므로 새 상태 모듈(sel2)은 상태 탭을 새로 만들어 뒤에 붙는다
        Assert.Equal(["check", "formula", "status", "move"], t.Select(x => x.Key));
        Assert.Equal(["flow", "sel", "color"], t[1].Members);
        Assert.Equal(["sel2"], t[2].Members);
    }

    [Fact]
    public void Persist_KeepsHiddenModulesInTheirTab()
    {
        var all = SideTabs.Persist(null, SideTabs.MoveModule(Default(), "err", "status", null));
        // err를 끄면 보이는 탭에서 빠진다 → 저장하면 원래 탭 끝에 남아 다시 켜면 같은 자리
        var visible = SideTabs.Build(Mods.Where(m => m.Id != "err"), all);
        var again = SideTabs.Persist(all, SideTabs.MoveTab(visible, "formula", "status"));
        Assert.Equal(["sel", "color", "err"], again.First(s => s.Key == "status").Members);
        var back = SideTabs.Build(Mods, again);
        Assert.Equal(["formula", "status"], back.Select(x => x.Key));
    }

    [Fact]
    public void LayoutState_RoundTripsTabsAndHeights()
    {
        var s = new LayoutState { SideTabs = SideTabs.Persist(null, Default()) };
        s.Update("sel", p => p with { SideHeight = 180 });
        var (r, ok) = LayoutState.FromJson(s.ToJson());
        Assert.True(ok);
        Assert.Equal(3, r.SideTabs.Count);
        Assert.Equal(180, r.Get("sel").SideHeight);
        Assert.Empty(LayoutState.FromJson("{}").State.SideTabs);
    }

    [Fact]
    public void SidebarDefault_MovesOldOffOnce_ThenRespectsOff()
    {
        var (s, changed) = HunsuSettings.FromJson("""{"sidebar":"off"}""").WithSidebarDefault();
        Assert.True(changed);
        Assert.Equal(("right", 1), (s.Sidebar, s.SidebarIntro));
        var (s2, changed2) = HunsuSettings.FromJson("""{"sidebar":"off","sidebarIntro":1}""").WithSidebarDefault();
        Assert.False(changed2);
        Assert.Equal("off", s2.Sidebar);
        var (s3, _) = HunsuSettings.FromJson("""{"sidebar":"left"}""").WithSidebarDefault();
        Assert.Equal("left", s3.Sidebar);
        Assert.False(s3.SidebarCollapsed);
        Assert.True(s3.SidebarStay); // 기본 자리 고정
        Assert.False(HunsuSettings.FromJson("""{"sidebarStay":false}""").SidebarStay);
        Assert.True(HunsuSettings.FromJson(HunsuSettings.FromJson("""{"sidebarCollapsed":true}""").ToJson()).SidebarCollapsed);
    }
}
