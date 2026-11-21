// 기능: 짜는 중인 수식 하나(함수 + 빈칸 값들). 빈칸 줄 만들기, 반복 묶음 늘리기·줄이기, 값 넣기, 완성 수식 글 만들기, 빠진 것·틀린 것 찾기.
// 사용법: var d = new FormulaDraft(def);  d.Set(0, "D2:D9");  d.AddRepeat("crit");  string f = d.Build();  // "=SUMIFS(D2:D9,B2:B9,\"서울\")"
//   d.Problems() → [DraftIssue("'조건 범위 1'을(를) 채워 주세요", Blocking: true)]처럼 사람이 읽는 글 목록. Blocking이 하나라도 있으면 넣기를 막는다.
//   글자 빈칸(text)은 따옴표를 붙이고 안의 따옴표는 두 번 쓴다. 조건(criteria)은 칸 주소·숫자면 그대로, 아니면 따옴표. 참·거짓(bool)은 참/거짓/TRUE/FALSE/1/0을 TRUE/FALSE로.
//   끝쪽의 빈 선택 빈칸과, 두 번째부터의 반복 묶음이 모두 비었으면 수식에서 뺀다.
// 의존: FunctionCatalog(ArgDef, FunctionDef), FormulaRef. 순수 계산(엑셀 없이 시험). 수식은 영어 함수 이름과 쉼표(Range.Formula 규칙)로 만든다.
using System.Globalization;

namespace ClockOut.Hunsu.Core.Formula;

/// <summary>화면의 빈칸 한 줄. Index는 반복 묶음의 몇 번째(1부터, 반복이 아니면 0).</summary>
public sealed class DraftSlot(ArgDef arg, int index)
{
    public ArgDef Arg { get; } = arg;
    public int Index { get; } = index;
    public string Text { get; set; } = "";
    public string Label => Index > 0 ? $"{Arg.Label} {Index}" : Arg.Label;
    public bool IsEmpty => Text.Trim().Length == 0;
}

/// <summary>검사 결과 한 줄. Blocking이면 넣기를 막고, 아니면 알림만.</summary>
public sealed record DraftIssue(string Text, bool Blocking);

public sealed class FormulaDraft
{
    public FormulaDraft(FunctionDef def)
    {
        Def = def;
        foreach (var a in def.Args) Slots.Add(new DraftSlot(a, a.Repeat.Length > 0 ? 1 : 0));
    }

    public FunctionDef Def { get; }

    /// <summary>화면에 보일 순서의 빈칸들(반복 묶음은 묶음 자리에 1, 2, 3… 차례로).</summary>
    public List<DraftSlot> Slots { get; } = [];

    public void Set(int slot, string text) => Slots[slot].Text = text;

    public int RepeatCount(string group) => Slots.Where(s => s.Arg.Repeat == group).Select(s => s.Index).DefaultIfEmpty(0).Max();

    /// <summary>반복 묶음을 하나 더한다(마지막 묶음 뒤에). 묶음 하나는 최대 MaxRepeat개.</summary>
    public bool AddRepeat(string group)
    {
        const int MaxRepeat = 30;
        int n = RepeatCount(group);
        if (n == 0 || n >= MaxRepeat) return false;
        int last = Slots.FindLastIndex(s => s.Arg.Repeat == group);
        var args = Def.Args.Where(a => a.Repeat == group).ToList();
        Slots.InsertRange(last + 1, args.Select(a => new DraftSlot(a, n + 1)));
        return true;
    }

    /// <summary>마지막 반복 묶음을 뺀다(하나는 남긴다).</summary>
    public bool RemoveRepeat(string group)
    {
        int n = RepeatCount(group);
        if (n <= 1) return false;
        Slots.RemoveAll(s => s.Arg.Repeat == group && s.Index == n);
        return true;
    }

    public string Build()
    {
        var parts = Included().Select(s => Format(s.Arg.Kind, s.Text)).ToList();
        while (parts.Count > 0 && parts[^1].Length == 0) parts.RemoveAt(parts.Count - 1);
        return "=" + Def.Name + "(" + string.Join(",", parts) + ")";
    }

    /// <summary>수식에 들어갈 빈칸들: 두 번째부터의 반복 묶음이 다 비었으면 뺀다. 끝쪽 빈 선택 칸은 Build가 뺀다.</summary>
    private IEnumerable<DraftSlot> Included()
    {
        foreach (var s in Slots)
        {
            if (s.Index > 1 && Slots.Where(x => x.Arg.Repeat == s.Arg.Repeat && x.Index == s.Index).All(x => x.IsEmpty)) continue;
            yield return s;
        }
    }

    public IReadOnlyList<DraftIssue> Problems()
    {
        var list = new List<DraftIssue>();
        var included = Included().ToList();
        int lastFilled = included.FindLastIndex(s => !s.IsEmpty);
        for (int i = 0; i < included.Count; i++)
        {
            var s = included[i];
            if (!s.IsEmpty) continue;
            if (!s.Arg.Optional) list.Add(new DraftIssue($"'{s.Label}'을(를) 채워 주세요", true));
            else if (i < lastFilled) list.Add(new DraftIssue($"'{s.Label}'이(가) 비어 있어 기본값으로 계산됩니다", false));
        }
        if (Def.Args.Any(a => a.Hint == "sameSize")) list.AddRange(SizeProblems(included));
        return list;
    }

    /// <summary>hint가 sameSize인 범위 빈칸들은 첫 범위와 크기가 같아야 한다(SUMIFS·COUNTIFS 등). 크기를 모르는 참조는 건너뛴다.</summary>
    private static IEnumerable<DraftIssue> SizeProblems(List<DraftSlot> included)
    {
        var ranges = included.Where(s => s.Arg.Kind == "range" && s.Arg.Hint == "sameSize" && !s.IsEmpty).ToList();
        if (ranges.Count < 2) yield break;
        var first = FormulaRef.Size(ranges[0].Text);
        if (first == null) yield break;
        foreach (var s in ranges.Skip(1))
        {
            var size = FormulaRef.Size(s.Text);
            if (size != null && size != first)
                yield return new DraftIssue($"'{s.Label}'({size.Value.Rows}×{size.Value.Cols})의 크기가 '{ranges[0].Label}'({first.Value.Rows}×{first.Value.Cols})와 달라 #VALUE!가 납니다", true);
        }
    }

    /// <summary>빈칸 하나의 값을 수식 조각으로.</summary>
    public static string Format(string kind, string raw)
    {
        string t = raw.Trim();
        if (t.Length == 0) return "";
        return kind switch
        {
            "text" => IsQuoted(t) || FormulaRef.IsReference(t) || t.StartsWith('=') ? Unlead(t) : Quote(t),
            "criteria" => IsQuoted(t) || FormulaRef.IsReference(t) || IsNumber(t) || t.StartsWith('=') ? Unlead(t) : Quote(t),
            "bool" => Bool(t),
            _ => Unlead(t),
        };
    }

    /// <summary>조각 앞의 '='는 "식을 그대로"라는 뜻으로 받아 뗀다(=A1&"원").</summary>
    private static string Unlead(string t) => t.StartsWith('=') ? t[1..].Trim() : t;

    private static bool IsQuoted(string t) => t.Length >= 2 && t[0] == '"' && t[^1] == '"';

    private static bool IsNumber(string t) => double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

    public static string Quote(string t) => "\"" + t.Replace("\"", "\"\"") + "\"";

    private static string Bool(string t) => t.ToUpperInvariant() switch
    {
        "참" or "TRUE" or "1" or "예" or "정확히" => "TRUE",
        "거짓" or "FALSE" or "0" or "아니오" or "비슷하게" => "FALSE",
        _ => Unlead(t),
    };
}
