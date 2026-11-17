// 기능: 패널 룩(손그림 프로필) 시험: 이름 정리와 기본값(B), 설정 저장·되돌림, 룩이 밝기를 정함, 손그림 획 계획(같은 씨앗은 같은 모양, 룩마다 다른 장식, 크기 안에 머묾), 실제 룩 XAML 파일이 다 있는지.
// 사용법: dotnet test
// 의존: ClockOut.Hunsu.Core(Settings, Marks), ClockOut.Hunsu.Ui의 Theme/*.xaml 파일(텍스트로 읽음)
using ClockOut.Hunsu.Core.Marks;
using ClockOut.Hunsu.Core.Settings;

namespace ClockOut.Hunsu.Tests;

public class SkinTests
{
    [Theory]
    [InlineData(null, "penmarker")]
    [InlineData("", "penmarker")]
    [InlineData("nope", "penmarker")]
    [InlineData(" Chalk ", "chalk")]
    [InlineData("PENCILNOTE", "pencilnote")]
    [InlineData("plain", "plain")]
    public void Normalize_UnknownGoesToDefaultB(string? input, string expected) => Assert.Equal(expected, SkinChoice.Normalize(input));

    [Fact]
    public void Default_IsB_AndSettingsStoreIt()
    {
        Assert.Equal("penmarker", new HunsuSettings().Skin);
        Assert.Equal("penmarker", HunsuSettings.FromJson("{}").Skin);                       // 옛 설정 파일(skin 없음) → 기본 B
        Assert.Equal("chalk", HunsuSettings.FromJson("{\"skin\":\"chalk\"}").Skin);
        Assert.Equal("penmarker", HunsuSettings.FromJson("{\"skin\":\"???\"}").Skin);
        Assert.Contains("\"skin\": \"penmarker\"", new HunsuSettings().ToJson());
    }

    [Fact]
    public void Skins_DecideBrightness_AndPlainFollowsTheme()
    {
        Assert.Equal("light", SkinChoice.ByName("penmarker").ForcedTheme);
        Assert.Equal("light", SkinChoice.ByName("pencilnote").ForcedTheme);
        Assert.Equal("dark", SkinChoice.ByName("chalk").ForcedTheme);
        Assert.Null(SkinChoice.ByName("plain").ForcedTheme);
        Assert.Equal(SkinKind.None, SkinChoice.ByName("plain").Kind);
    }

    [Fact]
    public void EveryDictionaryNamedByASkin_ExistsAndDefinesSkinValues()
    {
        string dir = FindThemeDir();
        foreach (var spec in SkinChoice.All.Where(s => s.Dictionary != null))
        {
            string text = File.ReadAllText(Path.Combine(dir, spec.Dictionary!));
            Assert.Contains($">{spec.Kind}</sys:String>", text);                             // Hunsu.Skin.Kind가 SkinKind 이름과 같다
            foreach (var key in new[] { "Hunsu.Glass", "Hunsu.Text", "Hunsu.Accent", "Hunsu.Skin.Frame", "Hunsu.Skin.Swash", "Hunsu.FontPanelTitle", "Hunsu.Tone.Bad" })
                Assert.Contains($"x:Key=\"{key}\"", text);
        }
    }

    [Fact]
    public void Plan_SameInput_SameStrokes_DifferentSeed_Differs()
    {
        var a = SkinSketch.Plan(220, 160, 26, 60, SkinKind.Pencil, 5);
        var b = SkinSketch.Plan(220, 160, 26, 60, SkinKind.Pencil, 5);
        var c = SkinSketch.Plan(220, 160, 26, 60, SkinKind.Pencil, 6);
        Assert.Equal(a.Count, b.Count);
        Assert.Equal(a[0].Points[3], b[0].Points[3]);
        Assert.NotEqual(a[0].Points[3], c[0].Points[3]);
    }

    [Fact]
    public void Plan_EachKindHasItsOwnDecoration()
    {
        var pen = SkinSketch.Plan(220, 160, 26, 50, SkinKind.Pen, 1).Select(s => s.Role).ToHashSet();
        var pencil = SkinSketch.Plan(220, 160, 26, 50, SkinKind.Pencil, 1).Select(s => s.Role).ToHashSet();
        var chalk = SkinSketch.Plan(220, 160, 26, 50, SkinKind.Chalk, 1).Select(s => s.Role).ToHashSet();
        Assert.Contains(SkinRole.Swash, pen); Assert.DoesNotContain(SkinRole.Hatch, pen); Assert.DoesNotContain(SkinRole.FrameEcho, pen);
        Assert.Contains(SkinRole.Hatch, pencil); Assert.Contains(SkinRole.FrameEcho, pencil); Assert.DoesNotContain(SkinRole.Swash, pencil);
        Assert.Contains(SkinRole.TitleLine, chalk); Assert.Contains(SkinRole.FrameEcho, chalk); Assert.DoesNotContain(SkinRole.Hatch, chalk);
    }

    [Fact]
    public void Plan_None_Or_TooSmall_IsEmpty()
    {
        Assert.Empty(SkinSketch.Plan(220, 160, 26, 50, SkinKind.None, 1));
        Assert.Empty(SkinSketch.Plan(20, 10, 26, 50, SkinKind.Pen, 1));
    }

    [Theory]
    [InlineData(SkinKind.Pen)]
    [InlineData(SkinKind.Pencil)]
    [InlineData(SkinKind.Chalk)]
    public void Plan_StrokesStayNearThePanel(SkinKind kind)
    {
        const double w = 220, h = 160, margin = 8; // 모서리에서 조금 지나치는 손맛은 허용
        foreach (var s in SkinSketch.Plan(w, h, 26, 50, kind, 9))
        {
            Assert.True(s.Points.Count >= 2);
            foreach (var p in s.Points)
            {
                Assert.InRange(p.X, -margin, w + margin);
                Assert.InRange(p.Y, -margin, h + margin);
            }
        }
    }

    [Fact]
    public void Plan_Swash_StopsAtTitleText()
    {
        var shortTitle = SkinSketch.Plan(220, 160, 26, 30, SkinKind.Pen, 1).First(s => s.Role == SkinRole.Swash).Points[^1].X;
        var longTitle = SkinSketch.Plan(220, 160, 26, 120, SkinKind.Pen, 1).First(s => s.Role == SkinRole.Swash).Points[^1].X;
        Assert.True(longTitle > shortTitle + 60);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Outline_IsStableAndStaysNearTheElement(bool circle)
    {
        var a = SkinSketch.Plan(0, 0, 0, 0, SkinKind.None, 1); // 빈 계획은 그대로 비어 있다
        Assert.Empty(a);
        var o1 = SkinSketch.Outline(40, 40, circle, SkinKind.Pencil, 3);
        var o2 = SkinSketch.Outline(40, 40, circle, SkinKind.Pencil, 3);
        Assert.Equal(2, o1.Count);                                   // 연필: 선 + 덧그림
        Assert.Equal(o1[0].Points[5], o2[0].Points[5]);
        Assert.Single(SkinSketch.Outline(40, 40, circle, SkinKind.Pen, 3)); // 펜: 한 줄
        foreach (var s in o1) foreach (var p in s.Points) { Assert.InRange(p.X, -8, 48); Assert.InRange(p.Y, -8, 48); }
        Assert.Empty(SkinSketch.Outline(40, 40, circle, SkinKind.None, 3));
        Assert.Empty(SkinSketch.Outline(4, 4, circle, SkinKind.Pen, 3));
    }

    private static string FindThemeDir()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !Directory.Exists(Path.Combine(d.FullName, "ClockOut.Hunsu.Ui", "Theme"))) d = d.Parent;
        Assert.NotNull(d);
        return Path.Combine(d!.FullName, "ClockOut.Hunsu.Ui", "Theme");
    }
}
