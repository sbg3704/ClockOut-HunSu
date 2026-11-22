// 기능: 한글 초성 뽑기와 찾기용 글 다듬기. "조건 합" → "ㅈㄱㅎ", 초성만 친 글("ㅈㄱㅎ")인지 알아보기.
// 사용법: Hangul.Initials("조건 합") == "ㅈㄱㅎ";  Hangul.IsInitialsOnly("ㅈㄱ") == true;  Hangul.Squash(" Sum IFS ") == "sumifs"
// 의존: 없음(유니코드 계산만). 오픈소스를 쓸 만큼 크지 않아 직접 둔다(docs/23 3장).
namespace ClockOut.Hunsu.Core.Formula;

public static class Hangul
{
    private const int SyllableFirst = 0xAC00, SyllableLast = 0xD7A3, PerInitial = 21 * 28;
    private const string InitialJamo = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";

    /// <summary>글자마다 초성만 남긴다(한글 음절이 아닌 글자는 빼고, 이미 초성인 글자는 그대로).</summary>
    public static string Initials(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (c >= SyllableFirst && c <= SyllableLast) sb.Append(InitialJamo[(c - SyllableFirst) / PerInitial]);
            else if (InitialJamo.Contains(c)) sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>빈칸을 뺀 글이 모두 초성 자모인지.</summary>
    public static bool IsInitialsOnly(string text)
    {
        bool any = false;
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c)) continue;
            if (!InitialJamo.Contains(c)) return false;
            any = true;
        }
        return any;
    }

    /// <summary>빈칸과 점·밑줄을 빼고 소문자로(찾기 비교용).</summary>
    public static string Squash(string text) =>
        new([.. text.Where(c => !char.IsWhiteSpace(c) && c != '.' && c != '_' && c != '·').Select(char.ToLowerInvariant)]);
}
