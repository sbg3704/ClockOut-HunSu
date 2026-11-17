// 기능: 모듈 공용 라이브러리 시험: hud 선언(글 만들기·붙는 곳), 처음 창 크기 size, quick apply, 표시층 enabledKey, 실제 모듈이 독 왼쪽 기본·선언만으로 일하는지.
// 사용법: dotnet test
// 의존: ClockOut.Hunsu.Core(Manifest/HudSpec, QuickSetting, View)
using System.Text.Json;
using ClockOut.Hunsu.Core.Manifest;
using ClockOut.Hunsu.Core.View;

namespace ClockOut.Hunsu.Tests;

public class ModuleLibraryTests
{
    private const string Hud = """{"anchor":"cursor","enabledKey":"cursorHud","anchorKey":"hudAnchor","when":"s.count > 0","parts":[{"text":"합계 {s.sum:n} · {s.nonEmpty:n0}개","when":"s.sum != null"},{"text":"{s.count:n0}칸","when":"s.sum == null"},{"text":"오류 {s.errors:n0}","when":"s.errors > 0"}]}""";

    private static HudSpec ReadHud(string json = Hud)
    {
        var errors = new List<string>();
        using var d = JsonDocument.Parse(json);
        var h = HudSpec.Read(d.RootElement, errors);
        Assert.Empty(errors);
        return h!;
    }

    private static DataScope Scope(string json)
    {
        var scope = new DataScope();
        using var d = JsonDocument.Parse(json);
        scope.Set("s", d.RootElement);
        return scope;
    }

    [Fact]
    public void Hud_RendersSumAndErrors()
    {
        var h = ReadHud();
        Assert.Equal("합계 1,234.5 · 3개 · 오류 1", h.Render(Scope("""{"count":4,"nonEmpty":3,"sum":1234.5,"errors":1}""")));
        Assert.Equal("합계 10 · 2개", h.Render(Scope("""{"count":2,"nonEmpty":2,"sum":10,"errors":0}""")));
    }

    [Fact]
    public void Hud_TextOnlyRange_ShowsCount_AndConditionHides()
    {
        var h = ReadHud();
        Assert.Equal("5칸", h.Render(Scope("""{"count":5,"nonEmpty":5,"sum":null,"errors":0}""")));
        Assert.Null(h.Render(Scope("""{"count":0,"sum":null,"errors":0}""")));
    }

    [Fact]
    public void Hud_AnchorSettingWinsWhenKnown()
    {
        var h = ReadHud();
        Assert.Equal("cursor", h.EffectiveAnchor(null));
        Assert.Equal("cell", h.EffectiveAnchor("cell"));
        Assert.Equal("cursor", h.EffectiveAnchor("elsewhere"));
    }

    [Fact]
    public void Hud_BadDeclarations_AreReported()
    {
        foreach (var bad in new[] { """{"anchor":"moon","parts":[{"text":"a"}]}""", """{"anchor":"cursor","parts":[]}""", """{"parts":[{"text":""}]}""" })
        {
            var errors = new List<string>();
            using var d = JsonDocument.Parse(bad);
            Assert.Null(HudSpec.Read(d.RootElement, errors));
            Assert.NotEmpty(errors);
        }
    }

    private static string Manifest(string extra) => """{"spec":0,"id":"hunsu-x","name":"x","version":"0.1.000","host":"excel","category":"c","author":"a","description":"d","dependencies":[],"shapes":["panel","window"],"place":"edge-left","order":1,"data":[],"commands":[]""" + extra + "}";

    [Fact]
    public void Manifest_ReadsHudAndSize_AndRejectsBadSize()
    {
        var ok = ModuleManifest.Parse(Manifest(",\"size\":{\"width\":260,\"height\":120},\"hud\":" + Hud)).Manifest!;
        Assert.Equal(new FormSize(260, 120), ok.Size);
        Assert.NotNull(ok.Hud);
        Assert.NotEmpty(ModuleManifest.Parse(Manifest(",\"size\":{\"width\":5,\"height\":120}")).Errors);
        Assert.Null(ModuleManifest.Parse(Manifest("")).Manifest!.Hud);
    }

    [Fact]
    public void Quick_ApplyIsReadAndValidated()
    {
        string q = ",\"quick\":[{\"kind\":\"toggle\",\"key\":\"k\",\"label\":\"켜기\",\"apply\":\"refresh\"}]";
        Assert.Equal("refresh", ModuleManifest.Parse(Manifest(q)).Manifest!.Quick.Single().Apply);
        Assert.NotEmpty(ModuleManifest.Parse(Manifest(q.Replace("refresh", "boom"))).Errors);
    }

    [Fact]
    public void ViewMarks_ReadEnabledKey()
    {
        var (v, errors) = ViewSpec.Parse("""{"tab":{"title":"t"},"marks":[{"items":"a.b","tone":"info","style":"outline","enabledKey":"x"},{"items":"a.c","tone":"info","style":"fill"}],"panel":[]}""");
        Assert.Empty(errors);
        Assert.Equal("x", v!.Marks[0].EnabledKey);
        Assert.Equal("", v.Marks[1].EnabledKey);
    }

    private static string Root()
    {
        string root = AppContext.BaseDirectory;
        while (root != null && !Directory.Exists(Path.Combine(root, "modules"))) root = Path.GetDirectoryName(root)!;
        Assert.NotNull(root);
        return Path.Combine(root!, "modules");
    }

    [Fact]
    public void RealModules_AllPlacedLeftByDefault_AndSelectionDeclaresItsHud()
    {
        foreach (var dir in Directory.GetDirectories(Root()))
        {
            string id = Path.GetFileName(dir);
            var m = ModuleManifest.Parse(File.ReadAllText(Path.Combine(dir, id + ".hunsu.json")));
            Assert.True(m.Manifest != null, id + ": " + string.Join("; ", m.Errors));
            Assert.Equal("edge-left", m.Manifest!.Place);
        }
        var sel = ModuleManifest.Parse(File.ReadAllText(Path.Combine(Root(), "hunsu-selection", "hunsu-selection.hunsu.json"))).Manifest!;
        Assert.Equal("cursor", sel.Hud!.Anchor);
        Assert.Equal("합계 1,234.5 · 3개", sel.Hud.Render(SelectionScope("""{"count":4,"nonEmpty":3,"sum":1234.5,"errors":0}""")));
    }

    private static DataScope SelectionScope(string json)
    {
        var scope = new DataScope();
        using var d = JsonDocument.Parse(json);
        scope.Set("selection.summary", d.RootElement);
        return scope;
    }

    [Fact]
    public void Pencilflow_DeclaresDrawKeyAndApply()
    {
        string dir = Path.Combine(Root(), "hunsu-pencilflow");
        var m = ModuleManifest.Parse(File.ReadAllText(Path.Combine(dir, "hunsu-pencilflow.hunsu.json"))).Manifest!;
        Assert.Equal("refresh", m.Quick.First(q => q.Key == "pencilDraw").Apply);
        var (v, _) = ViewSpec.Parse(File.ReadAllText(Path.Combine(dir, "view.json")));
        Assert.All(v!.Marks, mk => Assert.Equal("pencilDraw", mk.EnabledKey));
    }
}
