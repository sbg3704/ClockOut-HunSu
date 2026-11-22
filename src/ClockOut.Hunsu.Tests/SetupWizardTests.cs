// 기능: 설정 마법사(Core/Setup/SetupWizard)의 순수 계산 시험: 처음 켤 때 보이기, 단계 이동 범위, wizard 쪽 이름, 요약 줄, setupSeen 읽기·쓰기.
// 사용법: dotnet test. 의존: Core/Setup/SetupWizard, Core/Settings/HunsuSettings, Core/Layout/Profiles.
using ClockOut.Hunsu.Core.Settings;
using ClockOut.Hunsu.Core.Setup;
using Xunit;

namespace ClockOut.Hunsu.Tests;

public class SetupWizardTests
{
    [Fact]
    public void ShowsOnFirstRun_UntilSeenIsSaved()
    {
        var fresh = new HunsuSettings();
        Assert.True(SetupWizard.ShouldShowOnStart(fresh));
        var seen = HunsuSettings.FromJson($$"""{"setupSeen":{{SetupWizard.Version}}}""");
        Assert.False(SetupWizard.ShouldShowOnStart(seen));
        Assert.Equal(SetupWizard.Version, HunsuSettings.FromJson(seen.ToJson()).SetupSeen); // 저장 뒤에도 남는다
        Assert.Equal(0, HunsuSettings.FromJson("""{"setupSeen":-3}""").SetupSeen);
    }

    [Fact]
    public void Move_StaysInsideSteps()
    {
        int last = SetupWizard.Steps.Count - 1;
        Assert.Equal(0, SetupWizard.Move(0, -1));
        Assert.Equal(1, SetupWizard.Move(0, 1));
        Assert.Equal(last, SetupWizard.Move(last, 1));
        Assert.Equal("hello", SetupWizard.Steps[0].Id);
        Assert.Equal("done", SetupWizard.Steps[last].Id);
        Assert.Equal(SetupWizard.Steps.Count, SetupWizard.Steps.Select(s => s.Id).Distinct().Count());
    }

    [Fact]
    public void WizardPageName_IsRecognized()
    {
        Assert.True(SetupWizard.IsWizardPage("wizard"));
        Assert.True(SetupWizard.IsWizardPage(" Wizard "));
        Assert.False(SetupWizard.IsWizardPage("general"));
        Assert.False(SetupWizard.IsWizardPage(null));
    }

    [Fact]
    public void Summary_NamesProfilePlaceSkinAndHelpers()
    {
        var s = HunsuSettings.FromJson("""{"profile":"accounting","sidebar":"off","dockShape":"fan","skin":"chalk","iconProfile":"lucide","quickBar":false,"cursorHud":true,"pencilDraw":false}""");
        var lines = SetupWizard.Summary(s).ToDictionary(l => l.Label, l => l.Value);
        Assert.Equal("정산·회계 마감", lines["하는 일"]);
        Assert.Equal("엑셀 안 도크 · 부채", lines["자리"]);
        Assert.StartsWith("C ", lines["패널 룩"]);
        Assert.Equal("Lucide", lines["아이콘"]);
        Assert.Equal("범위를 끌면 합계", lines["켜 둔 도움"]);
        Assert.StartsWith("직접 고름", SetupWizard.Summary(new HunsuSettings()).First().Value);
    }

    [Fact]
    public void EveryTipSwitch_IsAKnownSetting()
    {
        var json = new HunsuSettings().ToJson();
        foreach (var t in SetupWizard.Tips.Where(t => t.SettingKey != null))
            Assert.Contains($"\"{t.SettingKey}\"", json);
    }
}
