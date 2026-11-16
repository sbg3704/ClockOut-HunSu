// 기능: 옵션 선언표(quick) 시험: 읽기·검증, 순서와 묶음 배치, 값 해석, 모든 모듈 선언이 실제 설정 키를 가리키는지(전수 점검), 그리기 단축키.
// 사용법: dotnet test
// 의존: ClockOut.Hunsu.Core(Manifest/QuickSetting, ModuleManifest, HunsuSettings, HotkeyPolicy)
using System.Text.Json;
using System.Text.Json.Nodes;
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Core.Manifest;
using ClockOut.Hunsu.Core.Settings;

namespace ClockOut.Hunsu.Tests;

public class QuickSettingsTests
{
    private static List<QuickSetting> Read(string json, List<string>? errors = null)
    {
        using var d = JsonDocument.Parse(json);
        return QuickSchema.Read(d.RootElement, errors ?? []);
    }

    [Fact]
    public void Arrange_SortsByOrderAndGroupsInOrder()
    {
        var q = Read("""[{"kind":"toggle","key":"a","label":"A","group":"G2","order":5},{"kind":"toggle","key":"b","label":"B","group":"G1","order":1},{"kind":"toggle","key":"c","label":"C","group":"G2","order":2},{"kind":"toggle","key":"d","label":"D"}]""");
        var g = QuickSchema.Arrange(q);
        Assert.Equal(["", "G1", "G2"], g.Select(x => x.Name));  // 순서 0(D), 1(B), 2(C)
        Assert.Equal(["c", "a"], g[2].Items.Select(x => x.Key));
    }

    [Fact]
    public void Read_NestedMenuAndValidation()
    {
        var errors = new List<string>();
        var q = Read("""[{"kind":"menu","label":"고급","items":[{"kind":"choice","key":"k","label":"고르기","options":[{"value":"1","label":"하나"},{"value":"2","label":"둘"}]}]},{"kind":"choice","key":"x","label":"한 개뿐","options":[{"value":"1","label":"a"}]},{"kind":"weird","key":"y","label":"y"},{"kind":"toggle","label":"키 없음"}]""", errors);
        Assert.Single(q);
        Assert.Equal("k", Assert.Single(q[0].Children).Key);
        Assert.Equal(3, errors.Count);
    }

    [Fact]
    public void Values_ParseAsTypedAndCompareCurrent()
    {
        string json = HunsuSettings.FromJson("""{"pencilLook":"marker","panelWidth":260,"pencilDraw":false}""").ToJson();
        Assert.Equal("marker", QuickSettings.Current(json, "pencilLook"));
        Assert.Equal("false", QuickSettings.Current(json, "pencilDraw"));
        Assert.False(QuickSettings.IsOn(QuickSettings.Current(json, "pencilDraw")));
        Assert.Equal(260L, QuickSettings.Typed("260"));
        Assert.Equal(true, QuickSettings.Typed("true"));
        Assert.Equal("ink", QuickSettings.Typed("ink"));
        Assert.True(QuickSettings.IsCurrent("Marker", new QuickOption("marker", "x")));
    }

    [Fact]
    public void EveryModuleQuickKey_IsARealSetting_AndChoicesAreAccepted()
    {
        string root = AppContext.BaseDirectory;
        while (root != null && !Directory.Exists(Path.Combine(root, "modules"))) root = Path.GetDirectoryName(root)!;
        var settings = JsonNode.Parse(new HunsuSettings().ToJson())!.AsObject();
        int declared = 0;
        foreach (var file in Directory.GetFiles(Path.Combine(root!, "modules"), "*.hunsu.json", SearchOption.AllDirectories))
        {
            var (m, errors) = ModuleManifest.Parse(File.ReadAllText(file));
            Assert.True(m != null, file + ": " + string.Join(";", errors));
            foreach (var q in Flatten(m!.Quick))
            {
                declared++;
                Assert.True(settings.ContainsKey(q.Key), $"{m.Id}: 설정에 없는 키 {q.Key}");
                foreach (var o in q.Options)
                {
                    // 고른 값이 설정 읽기에서 바뀌지 않고 그대로 살아야 한다(범위 밖이면 기본값으로 돌아감).
                    var node = JsonNode.Parse(new HunsuSettings().ToJson())!;
                    node[q.Key] = JsonSerializer.SerializeToNode(QuickSettings.Typed(o.Value));
                    var back = QuickSettings.Current(HunsuSettings.FromJson(node.ToJsonString()).ToJson(), q.Key);
                    Assert.True(QuickSettings.IsCurrent(back, o), $"{m.Id}.{q.Key}={o.Value} 가 설정에서 {back}로 바뀜");
                }
            }
        }
        Assert.True(declared >= 18);
    }

    private static IEnumerable<QuickSetting> Flatten(IReadOnlyList<QuickSetting> items) =>
        items.SelectMany(q => q.Kind == QuickSetting.Menu ? Flatten(q.Children) : [q]);

    [Fact]
    public void DrawHotkey_IsCtrlAltD_AndNotTheSummonKey()
    {
        Assert.Equal(0x44, HotkeyPolicy.DrawKey);
        Assert.NotEqual(HotkeyPolicy.Key, HotkeyPolicy.DrawKey);
        Assert.True(new HunsuSettings().PencilDraw);
        Assert.False(HunsuSettings.FromJson("""{"pencilDraw":false}""").PencilDraw);
    }
}
