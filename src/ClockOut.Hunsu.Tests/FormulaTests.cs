// 기능: 한 수(docs/23)의 순수 계산 시험: 함수 사전 읽기, 찾기(이름·낱말·초성), 수식 짜기(따옴표·반복 묶음·끝 빈칸), 검사(빠진 칸·크기), 참조 글($ 돌리기, 시트 앞붙이).
// 사용법: dotnet test
// 의존: Core/Formula, 저장소의 modules/hunsu-hansu/functions.ko.json.
using ClockOut.Hunsu.Core.Formula;

namespace ClockOut.Hunsu.Tests;

public class FormulaTests
{
    private static FunctionCatalog Real()
    {
        string dir = ManifestViewTests.FindRepoDir("modules/hunsu-hansu");
        var (cat, errors) = FunctionCatalog.Parse(File.ReadAllText(Path.Combine(dir, "functions.ko.json")));
        Assert.Empty(errors);
        return cat;
    }

    [Fact]
    public void RealCatalog_ParsesWithoutErrors_AndHasCoreFunctions()
    {
        var cat = Real();
        Assert.True(cat.Functions.Count >= 40);
        foreach (var n in new[] { "SUM", "SUMIFS", "COUNTIFS", "IF", "IFERROR", "VLOOKUP", "XLOOKUP", "INDEX", "MATCH", "ROUND", "TEXT", "DATEDIF" })
            Assert.NotNull(cat.Find(n));
        Assert.All(cat.Functions, f => Assert.False(string.IsNullOrWhiteSpace(f.Say)));
    }

    [Fact]
    public void Parse_ReportsBadEntries_AndKeepsGoodOnes()
    {
        var (cat, errors) = FunctionCatalog.Parse("""{"functions":[{"name":"SUM","args":[{"key":"n","kind":"range"}]},{"name":"SUM"},{"name":"X","args":[{"key":"a","kind":"zzz"}]},{"name":""}]}""");
        Assert.Single(cat.Functions);
        Assert.Equal(3, errors.Count);
    }

    [Theory]
    [InlineData("sumifs", "SUMIFS")]
    [InlineData("vlook", "VLOOKUP")]
    [InlineData("조건 합", "SUMIFS")]
    [InlineData("ㅈㄱㅎ", "SUMIFS")]
    [InlineData("찾아서 가져오기", "VLOOKUP")]
    [InlineData("반올림", "ROUND")]
    [InlineData("근무일", "NETWORKDAYS")]
    public void Search_FindsByNameWordsAndInitials(string query, string expectedFirst)
    {
        var found = FunctionSearch.Search(Real(), query);
        Assert.NotEmpty(found);
        Assert.Equal(expectedFirst, found[0].Name);
    }

    [Fact]
    public void Search_EmptyQuery_RecentThenPopular()
    {
        var found = FunctionSearch.Search(Real(), "", recent: ["DATEDIF"], max: 3);
        Assert.Equal("DATEDIF", found[0].Name);
        Assert.True(found[1].Popular);
    }

    [Fact]
    public void Search_Nonsense_FindsNothing() => Assert.Empty(FunctionSearch.Search(Real(), "ㅋㅋㅋㅋㅋ"));

    [Fact]
    public void Hangul_Initials()
    {
        Assert.Equal("ㅈㄱㅎ", Hangul.Initials("조건 합"));
        Assert.True(Hangul.IsInitialsOnly("ㅈ ㄱ"));
        Assert.False(Hangul.IsInitialsOnly("조건"));
        Assert.False(Hangul.IsInitialsOnly(" "));
    }

    [Fact]
    public void Draft_SumIfs_QuotesCriteria_AndAddsRepeatGroups()
    {
        var d = new FormulaDraft(Real().Find("SUMIFS")!);
        Assert.Equal(3, d.Slots.Count);
        d.Set(0, "D2:D9"); d.Set(1, "B2:B9"); d.Set(2, "서울");
        Assert.Equal("=SUMIFS(D2:D9,B2:B9,\"서울\")", d.Build());
        Assert.True(d.AddRepeat("c"));
        Assert.Equal(5, d.Slots.Count);
        Assert.Equal("조건 범위 2", d.Slots[3].Label);
        Assert.Equal("=SUMIFS(D2:D9,B2:B9,\"서울\")", d.Build()); // 빈 두 번째 묶음은 빠진다
        Assert.Empty(d.Problems());
        d.Set(3, "C2:C9"); d.Set(4, ">=5");
        Assert.Equal("=SUMIFS(D2:D9,B2:B9,\"서울\",C2:C9,\">=5\")", d.Build());
        Assert.True(d.RemoveRepeat("c"));
        Assert.False(d.RemoveRepeat("c"));
    }

    [Fact]
    public void Draft_Problems_MissingAndSizeMismatch()
    {
        var d = new FormulaDraft(Real().Find("SUMIFS")!);
        Assert.Contains(d.Problems(), p => p.Blocking && p.Text.Contains("더할 범위"));
        d.Set(0, "D2:D9"); d.Set(1, "B2:B8"); d.Set(2, "서울");
        var p = Assert.Single(d.Problems());
        Assert.True(p.Blocking);
        Assert.Contains("크기", p.Text);
    }

    [Fact]
    public void Draft_TrailingOptionalOmitted_BoolNormalized()
    {
        var d = new FormulaDraft(Real().Find("VLOOKUP")!);
        d.Set(0, "A2"); d.Set(1, "$F$2:$H$50"); d.Set(2, "2");
        Assert.Equal("=VLOOKUP(A2,$F$2:$H$50,2)", d.Build());
        d.Set(3, "거짓");
        Assert.Equal("=VLOOKUP(A2,$F$2:$H$50,2,FALSE)", d.Build());
    }

    [Theory]
    [InlineData("text", "a\"b", "\"a\"\"b\"")]
    [InlineData("text", "\"이미\"", "\"이미\"")]
    [InlineData("text", "A2", "A2")]
    [InlineData("criteria", "100", "100")]
    [InlineData("criteria", "<>0", "\"<>0\"")]
    [InlineData("criteria", "매출!B2", "매출!B2")]
    [InlineData("criteria", "=\">\"&A1", "\">\"&A1")]
    [InlineData("any", "  D2>=100 ", "D2>=100")]
    [InlineData("bool", "참", "TRUE")]
    public void Format_PerKind(string kind, string raw, string expected) => Assert.Equal(expected, FormulaDraft.Format(kind, raw));

    [Fact]
    public void Reference_AddsSheetAndBookOnlyWhenNeeded()
    {
        Assert.Equal("B2:B9", FormulaRef.Reference("B2:B9", "요약", "a.xlsx", "요약", "a.xlsx"));
        Assert.Equal("'매출 1'!B2", FormulaRef.Reference("B2", "매출 1", "a.xlsx", "요약", "a.xlsx"));
        Assert.Equal("Data!B2", FormulaRef.Reference("B2", "Data", "a.xlsx", "요약", "a.xlsx"));
        Assert.Equal("'[b.xlsx]원본'!A1", FormulaRef.Reference("A1", "원본", "b.xlsx", "요약", "a.xlsx"));
        Assert.Equal("(Data!A1:A3,Data!C1:C3)", FormulaRef.Reference("A1:A3,C1:C3", "Data", "a.xlsx", "요약", "a.xlsx"));
        Assert.Equal("'A1'!B2", FormulaRef.Reference("B2", "A1", "", "요약", ""));
    }

    [Fact]
    public void CycleAbs_LikeF4()
    {
        string s = "D2:D9";
        s = FormulaRef.CycleAbs(s); Assert.Equal("$D$2:$D$9", s);
        s = FormulaRef.CycleAbs(s); Assert.Equal("D$2:D$9", s);
        s = FormulaRef.CycleAbs(s); Assert.Equal("$D2:$D9", s);
        s = FormulaRef.CycleAbs(s); Assert.Equal("D2:D9", s);
        Assert.Equal("'매출 1'!$B$2", FormulaRef.CycleAbs("'매출 1'!B2"));
        Assert.Equal("\"A1\"", FormulaRef.CycleAbs("\"A1\""));
    }

    [Fact]
    public void Size_AndIsReference()
    {
        Assert.Equal((8L, 2L), FormulaRef.Size("$B$2:C9"));
        Assert.Equal((1L, 1L), FormulaRef.Size("매출!A1"));
        Assert.Null(FormulaRef.Size("B:B"));
        Assert.True(FormulaRef.IsReference("'매출 1'!B2:B9"));
        Assert.True(FormulaRef.IsReference("B:B"));
        Assert.False(FormulaRef.IsReference("서울"));
        Assert.False(FormulaRef.IsReference("SUM1X"));
    }
}
