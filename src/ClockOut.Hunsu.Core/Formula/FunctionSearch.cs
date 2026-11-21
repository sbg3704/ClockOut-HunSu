// 기능: 하고 싶은 일로 함수 찾기. 영어 이름 앞부분("sumi"), 뜻·낱말에 든 한글("조건 합"), 초성("ㅈㄱㅎ")으로 찾아 점수 순으로 돌려준다.
// 사용법: FunctionSearch.Search(catalog, "조건 합", recent: ["SUMIFS"], max: 5) → FunctionDef 목록.  빈 글이면 최근 → 자주 쓰는 함수 순.
//   점수: 이름이 같음 100 > 이름 앞부분 80 > 낱말이 같음 70 > 낱말·뜻에 모든 조각이 듦 50 > 초성 앞부분 40 > 초성에 듦 25. 최근 쓴 함수 +5, 자주 쓰는 함수 +2, 365 전용 −3(못 쓰면 뒤로).
// 의존: FunctionCatalog, Hangul. 순수 계산이라 화면 없이 시험한다.
namespace ClockOut.Hunsu.Core.Formula;

public static class FunctionSearch
{
    public static IReadOnlyList<FunctionDef> Search(FunctionCatalog catalog, string query, IReadOnlyList<string>? recent = null, int max = 5, bool has365 = true)
    {
        recent ??= [];
        string q = query.Trim();
        if (q.Length == 0) return Defaults(catalog, recent, max);
        var scored = new List<(FunctionDef F, int Score, int Order)>();
        int order = 0;
        foreach (var f in catalog.Functions)
        {
            int s = Score(f, q);
            if (s <= 0) { order++; continue; }
            if (recent.Contains(f.Name, StringComparer.OrdinalIgnoreCase)) s += 5;
            if (f.Popular) s += 2;
            if (f.Needs365 && !has365) s -= 3;
            scored.Add((f, s, order++));
        }
        return [.. scored.OrderByDescending(x => x.Score).ThenBy(x => x.Order).Take(max).Select(x => x.F)];
    }

    /// <summary>글자가 없을 때: 최근 쓴 함수(최근 순) 다음 자주 쓰는 함수.</summary>
    private static IReadOnlyList<FunctionDef> Defaults(FunctionCatalog catalog, IReadOnlyList<string> recent, int max)
    {
        var list = new List<FunctionDef>();
        foreach (var name in recent)
            if (catalog.Find(name) is { } f && !list.Contains(f)) list.Add(f);
        foreach (var f in catalog.Functions.Where(f => f.Popular))
            if (!list.Contains(f)) list.Add(f);
        return [.. list.Take(max)];
    }

    public static int Score(FunctionDef f, string query)
    {
        string sq = Hangul.Squash(query);
        if (sq.Length == 0) return 0;
        string name = f.Name.ToLowerInvariant().Replace(".", "");
        if (name == sq) return 100;
        if (name.StartsWith(sq, StringComparison.Ordinal)) return 80;
        var words = f.Words.Select(Hangul.Squash).ToList();
        if (words.Contains(sq)) return 70;
        var pieces = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(Hangul.Squash).Where(p => p.Length > 0).ToList();
        string hay = string.Join("|", words.Append(Hangul.Squash(f.Say)).Append(Hangul.Squash(f.Group)));
        if (!Hangul.IsInitialsOnly(query) && pieces.All(p => hay.Contains(p, StringComparison.Ordinal))) return 50;
        if (Hangul.IsInitialsOnly(query))
        {
            var inits = f.Words.Append(f.Say).Select(w => Hangul.Initials(w)).ToList();
            if (inits.Any(i => i.StartsWith(sq, StringComparison.Ordinal))) return 40;
            if (inits.Any(i => i.Contains(sq, StringComparison.Ordinal))) return 25;
        }
        return 0;
    }
}
