// 기능: 모듈을 어디서 읽을지 정하는 순수 계산(2026-11-22, 칼퇴 docs/10 3장).
//   모듈은 두 곳에서 온다: 묶음에 든 기본 모듈(실행 파일 옆 modules\<id>\)과, 칼퇴가 카탈로그에서 받아 풀어 준 모듈
//   (환경변수 CLOCKOUT_MODULES_DIR → <모듈 id>\<판>\, 풀기가 끝난 판에는 표시 파일 .clockout-module이 있다).
//   받은 모듈은 표시 파일이 있는 판 가운데 가장 높은 판 하나만 쓴다. 기본 모듈과 id가 같으면 선언(version)이 더 높은 쪽이 이긴다(같으면 기본 모듈).
// 사용법: var picked = ModuleSources.PickDownloaded(installed);  // installed: (모듈 id, [표시 파일이 있는 판 폴더 이름...])
//   if (ModuleSources.Replaces(newVersion, oldVersion)) ...
// 의존: Core/Bridge/BridgeUpdatePlan.Compare(판 비교, 같은 계산을 두 번 짜지 않음), Core/Manifest/ModuleGate.IsValidId. 파일 읽기는 앱(Shell/ModuleCatalog)이 한다.
using ClockOut.Hunsu.Core.Bridge;

namespace ClockOut.Hunsu.Core.Manifest;

/// <summary>모듈 하나를 읽을 폴더. Origin은 "bundled"(묶음 기본) 또는 "downloaded"(칼퇴가 받아 둔 것).</summary>
public sealed record ModuleSource(string Id, string Dir, string Origin);

public static class ModuleSources
{
    /// <summary>칼퇴가 받은 모듈 폴더를 넘기는 환경변수 이름(칼퇴 ModuleStore.EnvVar와 같다).</summary>
    public const string EnvVar = "CLOCKOUT_MODULES_DIR";

    /// <summary>칼퇴가 풀기를 끝낸 판 폴더에 두는 표시 파일 이름.</summary>
    public const string Mark = ".clockout-module";

    public const string Bundled = "bundled";
    public const string Downloaded = "downloaded";

    /// <summary>모듈 id마다 가장 높은 판 하나를 고른다. id나 판 이름이 안전하지 않으면 건너뛴다. 돌려주는 값: (id, 판 폴더 이름).</summary>
    public static IReadOnlyList<(string Id, string Version)> PickDownloaded(IEnumerable<(string Id, IEnumerable<string> Versions)> installed)
    {
        var result = new List<(string, string)>();
        foreach (var (id, versions) in installed)
        {
            if (!ModuleGate.IsValidId(id)) continue;
            string? best = null;
            foreach (var v in versions)
            {
                if (!IsSafeVersion(v)) continue;
                if (best == null || CompareVersion(v, best) > 0) best = v;
            }
            if (best != null) result.Add((id, best));
        }
        return result;
    }

    /// <summary>같은 id가 이미 있을 때, 새로 읽은 선언의 판이 더 높으면 true(그쪽으로 바꾼다).</summary>
    public static bool Replaces(string? newVersion, string? oldVersion) => BridgeUpdatePlan.Compare(newVersion, oldVersion) > 0;

    // X.Y.Z로 읽히면 숫자로, 아니면 글자 순서로 견준다(어느 쪽이든 늘 같은 답).
    private static int CompareVersion(string a, string b)
    {
        int c = BridgeUpdatePlan.Compare(a, b);
        return c != 0 ? c : string.CompareOrdinal(a, b);
    }

    // 판 폴더 이름: 영문·숫자·.·-·_ 1~32자, ".."는 안 됨(경로 탈출 막기).
    private static bool IsSafeVersion(string? v) =>
        !string.IsNullOrEmpty(v) && v.Length <= 32 && !v.Contains("..") && v.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '.' or '-' or '_');
}
