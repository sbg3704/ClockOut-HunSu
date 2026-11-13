// 기능: 모듈 선언 검증, 화면 선언 읽기, 데이터 연결 글 틀, 조건 한 줄 시험. 저장소의 예제 모듈(modules/hunsu-selection)도 읽어 본다.
// 사용법: dotnet test
// 의존: ClockOut.Hunsu.Core(Manifest, View)
using System.Text.Json;
using ClockOut.Hunsu.Core.Manifest;
using ClockOut.Hunsu.Core.View;

namespace ClockOut.Hunsu.Tests;

public class ManifestViewTests
{
    private const string Good = """
        {"spec":0,"id":"hunsu-test","name":"시험","version":"0.1.000","host":"excel","category":"상태","author":"a","description":"d",
         "dependencies":[],"shapes":["tab","panel"],"place":"edge-right","order":100,
         "data":[{"topic":"selection.summary","on":["sel"],"weight":"light"}]}
        """;

    [Fact]
    public void Manifest_Good()
    {
        var (m, errors) = ModuleManifest.Parse(Good);
        Assert.Empty(errors);
        Assert.Equal("hunsu-test", m!.Id);
        Assert.Equal(100, m.Order);
        Assert.Equal("selection.summary", m.Data[0].Topic);
    }

    [Theory]
    [InlineData("\"id\":\"hunsu-test\"", "\"id\":\"Test\"", "id 형식")]
    [InlineData("\"spec\":0", "\"spec\":9", "spec_newer")]
    [InlineData("\"place\":\"edge-right\"", "\"place\":\"strip\"", "place")]
    [InlineData("\"shapes\":[\"tab\",\"panel\"]", "\"shapes\":[\"chip\"]", "panel과 window")]
    [InlineData("\"weight\":\"light\"", "\"weight\":\"fast\"", "무게")]
    public void Manifest_Bad(string from, string to, string expect)
    {
        var (m, errors) = ModuleManifest.Parse(Good.Replace(from, to));
        Assert.Null(m);
        Assert.Contains(errors, e => e.Contains(expect));
    }

    [Fact]
    public void Manifest_NotJson() => Assert.Contains("manifest_bad", ModuleManifest.Parse("{").Errors[0]);

    private static DataScope Scope()
    {
        var s = new DataScope();
        using var doc = JsonDocument.Parse("""{"addr":"$A$1:$B$2","sum":1234.5,"count":4,"avg":null,"types":{"num":2,"text":0},"name":"가"}""");
        s.Set("selection.summary", doc.RootElement);
        return s;
    }

    [Theory]
    [InlineData("합 {selection.summary.sum:n}", "합 1,234.5")]
    [InlineData("{selection.summary.sum:n0}개", "1,235개")]
    [InlineData("{selection.summary.addr}", "$A$1:$B$2")]
    [InlineData("평균 {selection.summary.avg:n}", "평균 -")]
    [InlineData("{selection.summary.types.num}", "2")]
    [InlineData("{nope.x}", "-")]
    [InlineData("괄호만 {", "괄호만 {")]
    public void Template_Renders(string template, string expect) => Assert.Equal(expect, Template.Render(template, Scope()));

    [Theory]
    [InlineData("selection.summary.types.num == 2", true)]
    [InlineData("selection.summary.types.text == 0", true)]
    [InlineData("selection.summary.count > 3", true)]
    [InlineData("selection.summary.count <= 3", false)]
    [InlineData("selection.summary.avg == null", true)]
    [InlineData("selection.summary.name == \"가\"", true)]
    [InlineData("selection.summary.name != \"가\"", false)]
    [InlineData("selection.summary.sum", true)]
    [InlineData("selection.summary.types.text", false)]
    [InlineData("missing.x == 0", false)]
    [InlineData("a && b", false)]
    public void Condition_Evaluates(string expr, bool expect) => Assert.Equal(expect, Condition.Evaluate(expr, Scope()));

    [Fact]
    public void ViewSpec_UnknownItemSkipped()
    {
        var (spec, errors) = ViewSpec.Parse("""{"tab":{"title":"t"},"panel":[{"type":"stat","value":"{a}"},{"type":"hologram"},{"type":"text","tone":"neon"}]}""");
        Assert.Equal("t", spec!.Tab!.Get("title"));
        Assert.Equal(2, spec.Panel.Count);
        Assert.Contains(errors, e => e.Contains("hologram"));
        Assert.Contains(errors, e => e.Contains("neon"));
    }

    [Fact]
    public void ExampleModule_IsValid()
    {
        string dir = FindRepoDir("modules/hunsu-selection");
        var (m, mErrors) = ModuleManifest.Parse(File.ReadAllText(Path.Combine(dir, "hunsu-selection.hunsu.json")));
        Assert.Empty(mErrors);
        Assert.Equal("hunsu-selection", m!.Id);
        var (v, vErrors) = ViewSpec.Parse(File.ReadAllText(Path.Combine(dir, "view.json")));
        Assert.Empty(vErrors);
        Assert.NotNull(v!.Tab);
        Assert.All(v.Panel, i => Assert.Contains(i.Type, ViewSpec.RenderedItems));
    }

    internal static string FindRepoDir(string relative)
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !Directory.Exists(Path.Combine(d.FullName, relative))) d = d.Parent;
        return d == null ? throw new DirectoryNotFoundException(relative) : Path.Combine(d.FullName, relative);
    }
}
