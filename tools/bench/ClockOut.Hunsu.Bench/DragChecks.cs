// 기능: 큰 범위를 끌어 선택하는 상황 흉내(docs/06, 일#304). 10만 칸 표에서 선택을 15ms마다 넓혀 가며
//       ① 엑셀이 버벅이는지(선택 COM 호출이 엑셀 화면 스레드를 기다리는 시간) ② 훈수꾼 조회가 몇 번 일어나는지
//       ③ 마지막 선택이 제때 화면에 반영되는지 ④ 큰 선택 조회 한 번이 얼마나 걸리는지 잰다.
// 사용법: DragChecks.Run(rep, xl, app) — 다른 시험이 끝난 뒤 맨 끝에 부른다(시트에 큰 표를 더하므로).
// 의존: HiddenExcel, AppUnderTest, Report. 마우스 단추는 누르지 않으므로 "누른 동안 미루기"는 여기서 재지 않는다(사용자 PC에서 눈으로).
using System.Diagnostics;

namespace ClockOut.Hunsu.Bench;

internal static class DragChecks
{
    private const int Rows = 5000, Cols = 20, Steps = 40, StepMs = 15;

    public static void Run(Report rep, HiddenExcel xl, AppUnderTest app)
    {
        Console.WriteLine("큰 범위 끌기");
        xl.FillBig(Rows, Cols);
        xl.Select("B2");
        app.WaitFor(l => l.Name == "render", 3000);
        Thread.Sleep(300);

        // 큰 선택 조회 한 번(쉬는 상태에서)
        app.Drain();
        string all = $"N1:AG{Rows}";
        xl.Select(all);
        var one = app.WaitFor(l => l.Name == "render" && Same(l.Get("addr"), all), 5000);
        double single = one == null ? double.NaN : Report.Ms(one.Long("qs"), one.Long("qd"));
        rep.Check($"큰 선택({Rows * Cols:N0}칸) 조회 한 번", one != null && one.Get("ok") == "True", one == null ? "응답 없음" : $"{single:0.0}ms, count={one.Get("count")}");
        Thread.Sleep(Math.Max(300, (int)(double.IsNaN(single) ? 300 : single * 4)));

        // 끌기 흉내: 오른쪽 아래로 넓혀 간다.
        app.Drain();
        var selectMs = new List<double>();
        string last = "N1";
        long t0 = Stopwatch.GetTimestamp();
        for (int i = 1; i <= Steps; i++)
        {
            int r = Rows * i / Steps, c = Math.Max(1, Cols * i / Steps);
            last = $"N1:{Column(14 + c - 1)}{r}";
            long s = Stopwatch.GetTimestamp();
            xl.Select(last);
            selectMs.Add(Report.Ms(s, Stopwatch.GetTimestamp()));
            Thread.Sleep(StepMs);
        }
        long tEnd = Stopwatch.GetTimestamp();
        int renders = 0;
        string target = last;
        var final = app.WaitFor(l => { if (l.Name == "render") renders++; return l.Name == "render" && Same(l.Get("addr"), target); }, 5000);
        double settle = final == null ? double.NaN : Report.Ms(tEnd, final.Long("frame"));
        rep.Time("끌기 중 엑셀 선택 한 번(엑셀 화면 스레드 대기 포함)", selectMs, 50);
        rep.Check($"끌기 {Steps}걸음 동안 조회가 묶임(걸음 수의 1/3 이하)", renders <= Steps / 3, $"조회·그리기 {renders}번 / 선택 {Steps}번, 걸린 시간 {Report.Ms(t0, tEnd):0}ms");
        rep.Check("끌기를 멈추면 마지막 선택이 1초 안에 화면에 반영", final != null && settle < 1000, final == null ? "마지막 선택 안 옴" : $"{settle:0}ms");
    }

    private static bool Same(string? got, string asked) => (got ?? "").Replace("$", "") == asked;

    /// <summary>1부터 세는 열 번호 → 열 글자(14 → N).</summary>
    private static string Column(int n)
    {
        string s = "";
        while (n > 0) { int m = (n - 1) % 26; s = (char)('A' + m) + s; n = (n - 1) / 26; }
        return s;
    }
}
