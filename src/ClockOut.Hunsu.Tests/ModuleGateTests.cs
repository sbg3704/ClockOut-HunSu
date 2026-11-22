// 기능: 칼퇴가 켜 둔 하위 모듈만 불러오는 문(Core/Manifest/ModuleGate) 시험. 칼퇴 파일 우선, 깨지면 선언 기본값, 선언도 없으면 모두.
//   모듈 폴더의 id가 칼퇴 규격(영문·숫자·-·_, 64개 이하)에 맞는지도 본다(pack_app이 그대로 선언에 넣으므로).
// 사용법: dotnet test
// 의존: ClockOut.Hunsu.Core
using ClockOut.Hunsu.Core.Manifest;

namespace ClockOut.Hunsu.Tests;

public class ModuleGateTests
{
    private const string App = """
        {"spec":0,"id":"hunsu","modules":[
          {"id":"hunsu-selection","name":"선택 요약","summary":"합계","default":true},
          {"id":"hunsu-errors","name":"오류 셀","summary":"오류"},
          {"id":"hunsu-booktools","name":"책·시트 도구","summary":"실험","default":false}]}
        """;

    [Fact]
    public void ClockOutFile_Wins()
    {
        var g = ModuleGate.Decide("""{"spec":0,"enabled":["hunsu-errors"]}""", App);
        Assert.Equal("clockout", g.Source);
        Assert.True(g.Allows("hunsu-errors"));
        Assert.False(g.Allows("hunsu-selection"));
        Assert.False(g.Allows("hunsu-booktools"));
        Assert.True(g.Allows("my-own-module")); // 선언에 없는 모듈은 칼퇴가 모르므로 불러온다
    }

    [Fact]
    public void NoFile_UsesDeclaredDefault()
    {
        var g = ModuleGate.Decide(null, App);
        Assert.Equal("default", g.Source);
        Assert.True(g.Allows("hunsu-selection"));
        Assert.True(g.Allows("hunsu-errors")); // default 생략 = true
        Assert.False(g.Allows("hunsu-booktools"));
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("""{"spec":1,"enabled":["hunsu-errors"]}""")]
    [InlineData("""{"spec":0}""")]
    [InlineData("""{"spec":0,"enabled":"hunsu-errors"}""")]
    [InlineData("""{"spec":0,"enabled":[1,2]}""")]
    [InlineData("[]")]
    public void BrokenFile_FallsBackToDefault(string bad)
    {
        var g = ModuleGate.Decide(bad, App);
        Assert.Equal("default", g.Source);
        Assert.NotNull(g.Problem);
        Assert.True(g.Allows("hunsu-selection"));
        Assert.False(g.Allows("hunsu-booktools"));
    }

    [Fact]
    public void NothingAtAll_LoadsEverything()
    {
        var g = ModuleGate.Decide(null, null);
        Assert.Equal("all", g.Source);
        Assert.True(g.Allows("hunsu-booktools"));
        Assert.Equal("all", ModuleGate.Decide("{broken", "not json").Source);
    }

    [Fact]
    public void EmptyEnabled_LoadsOnlyUnknownModules()
    {
        var g = ModuleGate.Decide("""{"spec":0,"enabled":[]}""", App);
        Assert.False(g.Allows("hunsu-selection"));
        Assert.True(g.Allows("my-own-module"));
    }

    [Fact]
    public void FileWithoutDeclaration_IsStrict()
    {
        var g = ModuleGate.Decide("""{"enabled":["hunsu-errors"]}""", null); // spec 생략은 0으로 본다
        Assert.Equal("clockout", g.Source);
        Assert.True(g.Allows("hunsu-errors"));
        Assert.False(g.Allows("hunsu-selection"));
    }

    [Fact]
    public void ModuleFolders_FitClockOutSpec()
    {
        string? root = AppContext.BaseDirectory;
        while (root != null && !Directory.Exists(Path.Combine(root, "modules"))) root = Path.GetDirectoryName(root);
        Assert.NotNull(root);
        var ids = Directory.GetDirectories(Path.Combine(root!, "modules")).Select(Path.GetFileName).ToList();
        Assert.InRange(ids.Count, 1, ModuleGate.MaxModules);
        Assert.All(ids, id => Assert.True(ModuleGate.IsValidId(id), id));
        Assert.False(ModuleGate.IsValidId("한글"));
        Assert.False(ModuleGate.IsValidId(""));
    }

    // 칼퇴 계약(칼퇴 docs/11, 2026-11-22): 칼퇴가 주는 파일·선언에 모르는 칸이 더해져도 죽지 않고 무시한다.
    [Fact]
    public void UnknownFields_AreIgnored()
    {
        const string enabled = """{"spec":0,"enabled":["hunsu-errors"],"future":{"x":1},"note":"새 칸"}""";
        const string app = """
            {"spec":0,"contract_version":1,"commands":{"settings":["--settings"]},"id":"hunsu","layout":{"w":3},
             "modules":[{"id":"hunsu-errors","name":"오류","default":false,"memoryMb":3,"icon":"x"},{"id":"hunsu-selection","name":"선택"}]}
            """;
        var gate = ModuleGate.Decide(enabled, app);
        Assert.Equal("clockout", gate.Source);
        Assert.True(gate.Allows("hunsu-errors"));
        Assert.False(gate.Allows("hunsu-selection"));
        Assert.Equal("default", ModuleGate.Decide(null, app).Source);
        Assert.False(ModuleGate.Decide(null, app).Allows("hunsu-errors"));
    }
}
