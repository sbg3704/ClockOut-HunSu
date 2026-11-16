// 기능: 작업 프로필(Core/Layout/Profiles)의 계산 시험. 설치된 모듈만 켜고 순서를 주며, 나머지는 끈다.
// 사용법: dotnet test. 의존: Core/Layout/Profiles, 저장소 modules 폴더(프로필의 모듈 id가 실제로 있는지).
using ClockOut.Hunsu.Core.Layout;
using Xunit;

namespace ClockOut.Hunsu.Tests;

public class ProfilesTests
{
    [Fact]
    public void Plan_EnablesProfileModulesInOrder_AndDisablesTheRest()
    {
        var p = new Profile("t", "t", "", ["hunsu-b", "hunsu-a", "hunsu-missing"]);
        var plan = Profiles.Plan(p, ["hunsu-a", "hunsu-b", "hunsu-c"]);
        Assert.Equal((true, (int?)100), plan["hunsu-b"]);
        Assert.Equal((true, (int?)110), plan["hunsu-a"]);
        Assert.Equal((false, (int?)null), plan["hunsu-c"]);
        Assert.False(plan.ContainsKey("hunsu-missing"));
    }

    [Fact]
    public void Find_IsCaseInsensitive_AndUnknownIsNull()
    {
        Assert.Equal("accounting", Profiles.Find("Accounting")?.Id);
        Assert.Null(Profiles.Find("nope"));
        Assert.Null(Profiles.Find(null));
    }

    [Fact]
    public void EveryPresetModuleExistsInTheRepo()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "modules"))) dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);
        var ids = Directory.GetDirectories(Path.Combine(dir!, "modules")).Select(Path.GetFileName).ToHashSet();
        foreach (var profile in Profiles.All)
            foreach (var id in profile.Modules)
                Assert.True(ids.Contains(id), $"{profile.Id}: 모듈 {id} 없음");
    }
}
