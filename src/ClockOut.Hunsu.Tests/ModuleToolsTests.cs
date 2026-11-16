// 기능: 모듈 관리자와 새 모듈(오류 셀, 시트 이동, 수식 흐름)이 기대는 순수 계산 시험: 설정 읽기, 조회 간격, 신호→주제 고르기, 순서 바꾸기, 명령 인자 만들기, 켜짐 기본값, 저장소 모듈 전부 검증.
// 사용법: dotnet test
// 의존: ClockOut.Hunsu.Core(Settings, QueryThrottle, TopicRouting, Layout, View, Manifest)
using System.Text.Json;
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Core.Manifest;
using ClockOut.Hunsu.Core.Settings;
using ClockOut.Hunsu.Core.View;

namespace ClockOut.Hunsu.Tests;

public class ModuleToolsTests
{
    [Fact]
    public void Settings_Defaults_WhenEmptyOrBroken()
    {
        Assert.Equal(150, HunsuSettings.FromJson(null).QueryDragGapMs);
        Assert.Equal(600, HunsuSettings.FromJson("{ not json").StaleAfterMs);
        Assert.True(HunsuSettings.FromJson("").IsEnabledByDefault("hunsu-selection"));
        Assert.False(HunsuSettings.FromJson("").IsEnabledByDefault("hunsu-errors"));
    }

    [Fact]
    public void Settings_ReadsValues_ClampsBadOnes_IgnoresUnknownKeys()
    {
        var s = HunsuSettings.FromJson("""{"queryDragGapMs":400,"staleAfterMs":5,"listMaxRows":30,"enabledByDefault":["hunsu-errors"],"managerOnStart":true,"zzz":1}""");
        Assert.Equal(400, s.QueryDragGapMs);
        Assert.Equal(600, s.StaleAfterMs); // 250 미만은 기본값
        Assert.Equal(30, s.ListMaxRows);
        Assert.True(s.ManagerOnStart);
        Assert.True(s.IsEnabledByDefault("hunsu-errors"));
        Assert.False(s.IsEnabledByDefault("hunsu-selection"));
    }

    [Fact]
    public void Settings_Star_EnablesAll_AndRoundTrips()
    {
        var s = HunsuSettings.FromJson("""{"enabledByDefault":["*"]}""");
        Assert.True(s.IsEnabledByDefault("hunsu-anything"));
        var again = HunsuSettings.FromJson(s.ToJson());
        Assert.Equal(["*"], again.EnabledByDefault);
        Assert.Equal(s.QueryMaxGapMs, again.QueryMaxGapMs);
    }

    [Fact]
    public void Throttle_FirstQuery_NoWait()
    {
        Assert.Equal(0, QueryThrottle.DelayMs(1000, -1, 0, true, new HunsuSettings()));
    }

    [Fact]
    public void Throttle_Normal_WaitsTwiceTheLastDuration_CappedByMax()
    {
        var s = new HunsuSettings();
        Assert.Equal(15, QueryThrottle.DelayMs(1005, 1000, 10, false, s)); // 끝난 지 5ms, 간격 20ms
        Assert.Equal(0, QueryThrottle.DelayMs(1100, 1000, 10, false, s)); // 이미 지남
        Assert.Equal(300, QueryThrottle.DelayMs(1000, 1000, 5000, false, s)); // 상한 300
    }

    [Fact]
    public void Throttle_MouseDown_UsesDragGap_OrFourTimesDuration()
    {
        var s = new HunsuSettings();
        Assert.Equal(150, QueryThrottle.DelayMs(1000, 1000, 10, true, s));
        Assert.Equal(400, QueryThrottle.DelayMs(1000, 1000, 100, true, s));
    }

    private static DataSubscription Sub(string topic, params string[] on) => new(topic, on, "light");

    [Fact]
    public void Routing_PicksTopicsForSignal_WithoutDuplicates()
    {
        var subs = new[] { Sub("selection.summary", "sel"), Sub("flow.neighbors", "sel", "change"), Sub("errors", "change", "calc"), Sub("selection.summary", "change") };
        Assert.Equal(["selection.summary", "flow.neighbors"], TopicRouting.TopicsFor(SignalCodec.Sel, subs));
        Assert.Equal(["flow.neighbors", "errors", "selection.summary"], TopicRouting.TopicsFor(SignalCodec.Change, subs));
        Assert.Empty(TopicRouting.TopicsFor(SignalCodec.Sheet, subs));
        Assert.Equal(["selection.summary", "flow.neighbors", "errors"], TopicRouting.AllTopics(subs));
    }

    [Fact]
    public void Order_MovesOneStep_AndRenumbers()
    {
        var items = new[] { ("a", 100), ("b", 100), ("c", 300) };
        var up = OrderPlan.Move(items, "c", -1)!;
        Assert.Equal(100, up["a"]);
        Assert.Equal(200, up["c"]);
        Assert.Equal(300, up["b"]);
    }

    [Fact]
    public void Order_AtEdgeOrUnknown_ReturnsNull()
    {
        var items = new[] { ("a", 100), ("b", 200) };
        Assert.Null(OrderPlan.Move(items, "a", -1));
        Assert.Null(OrderPlan.Move(items, "b", 1));
        Assert.Null(OrderPlan.Move(items, "zzz", 1));
    }

    [Fact]
    public void CommandArgs_BuildsEscapedObject_FromRowValues()
    {
        using var doc = JsonDocument.Parse("""{"name":"매출 \"요약\"","addr":"C5"}""");
        var row = CommandArgs.RowScope(doc.RootElement);
        var props = new Dictionary<string, string> { ["type"] = "list", ["command"] = "x", ["arg.name"] = "{item.name}", ["arg.addr"] = "{item.addr}" };
        string json = CommandArgs.Build(props, row);
        using var back = JsonDocument.Parse(json);
        Assert.Equal("매출 \"요약\"", back.RootElement.GetProperty("name").GetString());
        Assert.Equal("C5", back.RootElement.GetProperty("addr").GetString());
        Assert.False(back.RootElement.TryGetProperty("type", out _)); // arg. 로 시작하는 것만
    }

    [Fact]
    public void Layout_Enabled_UserValueBeatsDefault()
    {
        var state = new LayoutState();
        string[] shapes = ["tab", "panel"];
        Assert.True(state.Effective("hunsu-errors", Places.EdgeRight, 200, shapes, enabledByDefault: false).Hidden);
        Assert.False(state.Effective("hunsu-errors", Places.EdgeRight, 200, shapes, enabledByDefault: true).Hidden);
        state.Update("hunsu-errors", p => p with { Enabled = true });
        Assert.False(state.Effective("hunsu-errors", Places.EdgeRight, 200, shapes, enabledByDefault: false).Hidden);
        var (back, ok) = LayoutState.FromJson(state.ToJson());
        Assert.True(ok);
        Assert.False(back.Effective("hunsu-errors", Places.EdgeRight, 200, shapes, enabledByDefault: false).Hidden);
        state.Update("hunsu-errors", p => p with { Enabled = false });
        Assert.True(state.Effective("hunsu-errors", Places.EdgeRight, 200, shapes).Hidden);
    }

    public static IEnumerable<object[]> RepoModules() =>
        new[] { "hunsu-selection", "hunsu-errors", "hunsu-sheets", "hunsu-flow", "hunsu-book", "hunsu-audit", "hunsu-names", "hunsu-keys", "hunsu-cell", "hunsu-comments", "hunsu-links", "hunsu-tables", "hunsu-sheetinfo", "hunsu-validation", "hunsu-condrules", "hunsu-hidden", "hunsu-composition", "hunsu-history", "hunsu-flowgraph", "hunsu-marks", "hunsu-crosshair", "hunsu-colorsum", "hunsu-coldiff", "hunsu-folder", "hunsu-valuetools", "hunsu-booktools" }.Select(id => new object[] { id });

    [Theory]
    [MemberData(nameof(RepoModules))]
    public void RepoModule_IsConsistent(string id)
    {
        string dir = ManifestViewTests.FindRepoDir("modules/" + id);
        var (m, mErrors) = ModuleManifest.Parse(File.ReadAllText(Path.Combine(dir, id + ".hunsu.json")));
        Assert.Empty(mErrors);
        var (v, vErrors) = ViewSpec.Parse(File.ReadAllText(Path.Combine(dir, "view.json")));
        Assert.Empty(vErrors);
        Assert.All(v!.Panel, i => Assert.Contains(i.Type, ViewSpec.RenderedItems));
        // 단추·목록이 쓰는 명령은 모듈 선언의 commands에 있어야 하고, 목록이 읽는 경로는 구독한 주제에서 시작해야 한다.
        foreach (var item in v.Panel)
        {
            string command = item.Get("command");
            if (command.Length > 0) Assert.Contains(command, m!.Commands);
            string items = item.Get("items");
            if (items.Length > 0) Assert.Contains(m!.Data, d => items.StartsWith(d.Topic + ".", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void ErrorsModule_RendersFromSampleData()
    {
        string dir = ManifestViewTests.FindRepoDir("modules/hunsu-errors");
        var (v, _) = ViewSpec.Parse(File.ReadAllText(Path.Combine(dir, "view.json")));
        var scope = new DataScope();
        using var doc = JsonDocument.Parse("""{"sheet":"매출","count":3,"shown":3,"items":[{"addr":"C5","sheet":"매출","kind":"div0","label":"#DIV/0!","formula":"=A5/B5"}],"truncated":false}""");
        scope.Set("errors", doc.RootElement);
        Assert.Equal("3", Template.Render(v!.Tab!.Get("badge"), scope));
        Assert.True(Condition.Evaluate("errors.count > 0", scope));
        Assert.False(Condition.Evaluate("errors.count == 0", scope));
        Assert.True(scope.TryGet("errors.items", out var arr));
        Assert.Equal(JsonValueKind.Array, arr.ValueKind);
    }

    [Fact]
    public void ScenarioWorkbook_CoversEveryModule()
    {
        // 사용자가 눈으로 확인하는 시나리오 통합 문서의 [안내] 시트에 모든 모듈 id가 있어야 한다(CLAUDE.md "시험 부탁할 때").
        string dir = ManifestViewTests.FindRepoDir("tools/scenarios");
        string path = Path.Combine(dir, "훈수꾼_시나리오.xlsx");
        Assert.True(File.Exists(path), "시나리오 문서가 없음: " + path);
        using var zip = System.IO.Compression.ZipFile.OpenRead(path);
        string strings = new StreamReader(zip.GetEntry("xl/sharedStrings.xml")!.Open()).ReadToEnd();
        string modules = ManifestViewTests.FindRepoDir("modules");
        foreach (string d in Directory.GetDirectories(modules))
        {
            string id = Path.GetFileName(d);
            Assert.True(strings.Contains(id, StringComparison.Ordinal), $"시나리오 문서에 모듈 {id}의 시나리오가 없음(tools/make_scenarios.py의 SCENARIOS)");
        }
    }

    [Fact]
    public void CrosshairModule_DeclaresBandMarks()
    {
        // 십자 음영은 mark 조회의 rowband·colband를 옅은 칠로 그린다(코드 없이 선언만).
        string dir = ManifestViewTests.FindRepoDir("modules/hunsu-crosshair");
        var (v, errors) = ViewSpec.Parse(File.ReadAllText(Path.Combine(dir, "view.json")));
        Assert.Empty(errors);
        Assert.Contains(v!.Marks, m => m.Items == "mark.rowband" && m.Style == "fill");
        Assert.Contains(v.Marks, m => m.Items == "mark.colband" && m.Style == "fill");
    }

    [Fact]
    public void ColorSumModule_RendersFromSampleData()
    {
        string dir = ManifestViewTests.FindRepoDir("modules/hunsu-colorsum");
        var (v, _) = ViewSpec.Parse(File.ReadAllText(Path.Combine(dir, "view.json")));
        var scope = new DataScope();
        using var doc = JsonDocument.Parse("""{"sheet":"색깔","scanned":9,"truncated":false,"fillKinds":3,"fontKinds":1,"otherCount":0,"fill":[{"label":"#FFC000","count":4,"nums":4,"sum":100}],"font":[]}""");
        scope.Set("color.sum", doc.RootElement);
        Assert.Equal("3", Template.Render(v!.Tab!.Get("badge"), scope));
        Assert.False(Condition.Evaluate("color.sum.truncated == true", scope));
        Assert.True(scope.TryGet("color.sum.fill", out var fill));
        Assert.Equal(1, fill.GetArrayLength());
    }

    [Fact]
    public void ColDiffModule_RendersReadyAndNotReady()
    {
        string dir = ManifestViewTests.FindRepoDir("modules/hunsu-coldiff");
        var (v, _) = ViewSpec.Parse(File.ReadAllText(Path.Combine(dir, "view.json")));
        var ready = new DataScope();
        using var d1 = JsonDocument.Parse("""{"ready":true,"sheet":"비교","rows":10,"diffCount":5,"truncated":false,"items":[{"addr":"A3","label":"3행","a":"banana","b":"Banana"}]}""");
        ready.Set("coldiff", d1.RootElement);
        Assert.Equal("5", Template.Render(v!.Tab!.Get("badge"), ready));
        Assert.True(Condition.Evaluate("coldiff.ready == true", ready));
        Assert.False(Condition.Evaluate("coldiff.ready == false", ready));
        var notReady = new DataScope();
        using var d2 = JsonDocument.Parse("""{"ready":false,"hint":"나란한 두 열을 고르세요"}""");
        notReady.Set("coldiff", d2.RootElement);
        Assert.True(Condition.Evaluate("coldiff.ready == false", notReady));
        Assert.Equal("나란한 두 열을 고르세요", Template.Render("{coldiff.hint}", notReady));
    }

    [Fact]
    public void FolderModule_RendersBooksAndOthers()
    {
        string dir = ManifestViewTests.FindRepoDir("modules/hunsu-folder");
        var (v, _) = ViewSpec.Parse(File.ReadAllText(Path.Combine(dir, "view.json")));
        var scope = new DataScope();
        using var doc = JsonDocument.Parse("""{"ready":true,"folder":"보고서","count":2,"bookCount":1,"truncated":false,"books":[{"name":"a.xlsx","ext":"xlsx","sizeKb":35,"modified":"2026-11-16 09:30"}],"others":[{"name":"메모.txt","ext":"txt","sizeKb":1,"modified":"2026-11-16 09:31"}]}""");
        scope.Set("folder.files", doc.RootElement);
        Assert.Equal("1", Template.Render(v!.Tab!.Get("badge"), scope));
        Assert.True(scope.TryGet("folder.files.books", out var books));
        Assert.Equal(1, books.GetArrayLength());
        Assert.True(scope.TryGet("folder.files.others", out var others));
        Assert.Equal(1, others.GetArrayLength());
    }

    [Fact]
    public void SelectionModule_ShowsHiddenCellsNote()
    {
        string dir = ManifestViewTests.FindRepoDir("modules/hunsu-selection");
        var (v, _) = ViewSpec.Parse(File.ReadAllText(Path.Combine(dir, "view.json")));
        var scope = new DataScope();
        using var doc = JsonDocument.Parse("""{"addr":"$A$2:$G$14","count":91,"nonEmpty":50,"sum":3780,"avg":75.6,"min":10,"max":136,"errors":0,"hiddenCells":41,"types":{"num":50,"text":0,"formula":0}}""");
        scope.Set("selection.summary", doc.RootElement);
        Assert.Contains(v!.Panel, i => i.Get("showWhen") == "selection.summary.hiddenCells > 0");
        Assert.True(Condition.Evaluate("selection.summary.hiddenCells > 0", scope));
        Assert.Equal("숨은 줄·열의 41칸은 뺐습니다", Template.Render(v.Panel.First(i => i.Get("showWhen") == "selection.summary.hiddenCells > 0").Get("text"), scope));
    }
}
