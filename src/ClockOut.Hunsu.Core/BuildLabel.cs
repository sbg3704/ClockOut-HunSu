// 기능: 빌드에 붙은 판 문자열을 사람이 읽는 표시로 바꾼다. "0.0.020+pr20.3.abc1234" → "v0.0.020c pr20 · abc1234" (판 번호 + 이 PR의 몇 번째 작업을 a, b, c 글자로 + 꼬리표 + 짧은 커밋).
// 사용법: BuildLabel.Format(informationalVersion);  BuildLabel.Letters(3) → "c"
// 의존: 없음(순수 계산). 어셈블리 읽기는 앱(Shell/BuildInfo)이 맡는다.
namespace ClockOut.Hunsu.Core;

public static class BuildLabel
{
    public static string Format(string full)
    {
        var plus = full.Split('+', 2);
        string text = "v" + plus[0];
        if (plus.Length < 2) return text;
        var parts = plus[1].Split('.');
        string sha = parts[^1];
        int count = parts.Length >= 2 && int.TryParse(parts[^2], out int n) ? n : 0;
        string tag = parts.Length >= 3 ? parts[0] : "";
        text += Letters(count);
        if (tag.Length > 0) text += " " + tag;
        return text + " · " + sha;
    }

    /// <summary>1→a, 26→z, 27→aa. 0 이하는 글자 없음(main과 같은 판).</summary>
    public static string Letters(int n)
    {
        string s = "";
        for (; n > 0; n = (n - 1) / 26) s = (char)('a' + (n - 1) % 26) + s;
        return s;
    }
}
