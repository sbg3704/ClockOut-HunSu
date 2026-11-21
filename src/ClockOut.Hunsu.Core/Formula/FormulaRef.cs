// 기능: 수식에 넣을 칸 주소 글 만들기. 다른 시트·다른 책이면 앞에 붙이고('시트'!A1, '[책.xlsx]시트'!A1), $ 고정을 F4처럼 돌리고, 범위 크기(행×열)를 센다.
// 사용법: FormulaRef.Reference("B2:B9", "매출", "a.xlsx", targetSheet: "요약", targetBook: "a.xlsx") == "매출!B2:B9" (따옴표는 필요할 때만)
//   FormulaRef.CycleAbs("B2") == "$B$2" → "B$2" → "$B2" → "B2"   /   FormulaRef.Size("B2:C9") == (8, 2)
// 의존: System.Text.RegularExpressions. 엑셀을 모른다. 여러 영역(A1:A3,C1:C3)은 영역마다 앞을 붙이고 괄호로 묶는다.
using System.Text.RegularExpressions;

namespace ClockOut.Hunsu.Core.Formula;

public static partial class FormulaRef
{
    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_.]*$")]
    private static partial Regex PlainSheet();

    [GeneratedRegex(@"^\$?[A-Za-z]{1,3}\$?\d+$")]
    private static partial Regex CellLike();

    [GeneratedRegex(@"(\$?)([A-Za-z]{1,3})(\$?)(\d+)(?![\d(])")]
    private static partial Regex CellRef();

    [GeneratedRegex(@"^(?:(?:'[^']*(?:''[^']*)*'|[^'!\s(),]+)!)?\$?[A-Za-z]{1,3}\$?\d+(?::\$?[A-Za-z]{1,3}\$?\d+)?$|^(?:(?:'[^']*(?:''[^']*)*'|[^'!\s(),]+)!)?\$?[A-Za-z]{1,3}:\$?[A-Za-z]{1,3}$|^(?:(?:'[^']*(?:''[^']*)*'|[^'!\s(),]+)!)?\$?\d+:\$?\d+$")]
    private static partial Regex RefLike();

    /// <summary>시트 이름에 따옴표가 필요하면 붙인다(영문·숫자·밑줄만이고 칸 주소처럼 생기지 않았으면 그대로).</summary>
    public static string QuoteSheet(string sheet)
    {
        if (PlainSheet().IsMatch(sheet) && !CellLike().IsMatch(sheet) && !sheet.Equals("TRUE", StringComparison.OrdinalIgnoreCase) && !sheet.Equals("FALSE", StringComparison.OrdinalIgnoreCase))
            return sheet;
        return "'" + sheet.Replace("'", "''") + "'";
    }

    /// <summary>고른 칸 주소를 넣을 칸 기준의 참조 글로. 같은 시트면 주소만.</summary>
    public static string Reference(string addr, string sheet, string book, string targetSheet, string targetBook)
    {
        addr = addr.Trim();
        if (addr.Length == 0) return "";
        string prefix;
        bool sameBook = book.Length == 0 || targetBook.Length == 0 || string.Equals(book, targetBook, StringComparison.OrdinalIgnoreCase);
        if (sameBook && string.Equals(sheet, targetSheet, StringComparison.Ordinal)) prefix = "";
        else if (sameBook) prefix = QuoteSheet(sheet) + "!";
        else prefix = "'[" + book.Replace("'", "''") + "]" + sheet.Replace("'", "''") + "'!";
        var areas = addr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (areas.Length <= 1) return prefix + addr;
        return "(" + string.Join(",", areas.Select(a => prefix + a)) + ")";
    }

    /// <summary>칸·범위 참조처럼 생겼는지(시트 앞붙이 허용). 조건·글자 칸에서 따옴표를 붙일지 정할 때 쓴다.</summary>
    public static bool IsReference(string text) => RefLike().IsMatch(text.Trim());

    /// <summary>F4처럼 $ 고정을 돌린다: 상대 → 절대 → 행 고정 → 열 고정 → 상대. 따옴표 안 글자와 시트 이름은 건드리지 않는다. 첫 칸 주소의 상태를 보고 모두 같은 상태로 맞춘다.</summary>
    public static string CycleAbs(string text)
    {
        int bang = text.LastIndexOf('!');
        string head = bang >= 0 ? text[..(bang + 1)] : "", body = bang >= 0 ? text[(bang + 1)..] : text;
        if (body.Contains('"')) return text;
        var first = CellRef().Match(body);
        if (!first.Success) return text;
        bool col = first.Groups[1].Value == "$", row = first.Groups[3].Value == "$";
        (bool nc, bool nr) = (col, row) switch
        {
            (false, false) => (true, true),
            (true, true) => (false, true),
            (false, true) => (true, false),
            _ => (false, false),
        };
        string next = CellRef().Replace(body, m => (nc ? "$" : "") + m.Groups[2].Value + (nr ? "$" : "") + m.Groups[4].Value);
        return head + next;
    }

    /// <summary>A1·A1:B9 모양 범위의 (행 수, 열 수). 열 전체·행 전체·여러 영역·이름은 null(크기를 모름).</summary>
    public static (long Rows, long Cols)? Size(string text)
    {
        string t = text.Trim();
        int bang = t.LastIndexOf('!');
        if (bang >= 0) t = t[(bang + 1)..];
        t = t.Replace("$", "");
        var parts = t.Split(':');
        if (parts.Length > 2 || parts.Any(p => !CellLike().IsMatch(p))) return null;
        var a = Cell(parts[0]);
        var b = parts.Length == 2 ? Cell(parts[1]) : a;
        return (Math.Abs(b.Row - a.Row) + 1, Math.Abs(b.Col - a.Col) + 1);
    }

    private static (long Row, long Col) Cell(string a1)
    {
        int i = 0;
        long col = 0;
        while (i < a1.Length && char.IsAsciiLetter(a1[i])) col = col * 26 + (char.ToUpperInvariant(a1[i++]) - 'A' + 1);
        return (long.Parse(a1[i..], System.Globalization.CultureInfo.InvariantCulture), col);
    }
}
