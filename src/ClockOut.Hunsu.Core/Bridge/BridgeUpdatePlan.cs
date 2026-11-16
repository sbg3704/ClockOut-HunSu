// 기능: 엑셀에 설치된 다리와 훈수꾼 묶음에 들어 있는 다리를 견주어 "바꿀지"를 정한다(docs/02, 사용자 지적 2026-11-16: 다리 판이 올라가도 자동으로 안 바뀌어 지우고 다시 설치해야 했다).
// 사용법: BridgeUpdatePlan.Decide(설치됨?, 설치된 판, 설치된 해시, 묶음 판, 묶음 해시) → Update 일 때만 바꾼다.
//   판이 높으면 바꾼다. 판이 같아도 내용(해시)이 다르면 바꾼다(번호를 올리는 걸 잊은 경우). 판이 낮은 묶음은 바꾸지 않는다(낮은 판으로 되돌리지 않음).
// 의존: 없음(순수 계산). 파일 작업은 앱(Shell/BridgeUpdater)이 맡는다.
namespace ClockOut.Hunsu.Core.Bridge;

public enum BridgeAction { NotInstalled, UpToDate, Update, SkipDowngrade }

public static class BridgeUpdatePlan
{
    public static BridgeAction Decide(bool installed, string? installedVersion, string? installedHash, string? bundledVersion, string? bundledHash)
    {
        if (!installed) return BridgeAction.NotInstalled;
        if (installedHash != null && bundledHash != null && string.Equals(installedHash, bundledHash, StringComparison.OrdinalIgnoreCase)) return BridgeAction.UpToDate;
        int c = Compare(bundledVersion, installedVersion);
        if (c < 0) return BridgeAction.SkipDowngrade;
        return BridgeAction.Update; // 더 높거나 같은 판인데 내용이 다르거나, 판을 모르는 경우
    }

    /// <summary>X.Y.Z 숫자 비교. 한쪽이 비었거나 읽을 수 없으면 0(같음으로 본다).</summary>
    public static int Compare(string? a, string? b)
    {
        var pa = Parse(a);
        var pb = Parse(b);
        if (pa == null || pb == null) return 0;
        for (int i = 0; i < 3; i++)
            if (pa[i] != pb[i]) return pa[i].CompareTo(pb[i]);
        return 0;
    }

    private static int[]? Parse(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var parts = v.Trim().TrimStart('v', 'V').Split('.');
        if (parts.Length != 3) return null;
        var r = new int[3];
        for (int i = 0; i < 3; i++)
            if (!int.TryParse(parts[i], out r[i])) return null;
        return r;
    }

    /// <summary>다리 선언(clockout-hunsu.clockout.json)에서 version 값을 읽는다. 못 읽으면 null.</summary>
    public static string? ReadManifestVersion(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("version", out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() : null;
        }
        catch (System.Text.Json.JsonException) { return null; }
    }
}
