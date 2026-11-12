// 기능: 측정 결과(시간 표, 통과/실패 확인 목록, 관찰 메모)를 모아 마크다운과 JSON으로 저장하고 화면에 보인다.
// 사용법: var rep = new Report(); rep.Time("선택→화면", samples, 50); rep.Check("클릭 통과", ok, "자세히"); rep.Save(dir)
// 의존: Core/LatencyStats. 셀 값은 넣지 않는다(주소와 개수, 시간만).
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using ClockOut.Hunsu.Core;

namespace ClockOut.Hunsu.Bench;

internal sealed class Report
{
    private readonly List<(string Label, LatencyStats Stats, double Target)> _times = [];
    private readonly List<(string Name, bool Ok, string Detail)> _checks = [];
    private readonly List<string> _notes = [];

    public static double Ms(long from, long to) => (to - from) * 1000.0 / Stopwatch.Frequency;

    public void Time(string label, IEnumerable<double> samples, double targetMs)
    {
        var s = LatencyStats.From(samples);
        _times.Add((label, s, targetMs));
        Console.WriteLine(s.ToRow(label, targetMs));
    }

    public void Check(string name, bool ok, string detail = "")
    {
        _checks.Add((name, ok, detail));
        Console.WriteLine($"  [{(ok ? "통과" : "실패")}] {name} {detail}");
    }

    public void Note(string text)
    {
        _notes.Add(text);
        Console.WriteLine("  - " + text);
    }

    public int Failed => _checks.Count(c => !c.Ok);

    public string Markdown()
    {
        var c = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine(string.Create(c, $"# 0단계 측정 결과 ({DateTime.Now:yyyy-MM-dd HH:mm})"));
        sb.AppendLine();
        sb.AppendLine(string.Create(c, $"PC: {Environment.MachineName.Length} 글자 이름(가림), 논리 CPU {Environment.ProcessorCount}, {Environment.OSVersion.VersionString}, .NET {Environment.Version}"));
        sb.AppendLine();
        sb.AppendLine("| 항목 | 목표(P95) | n | 평균 ms | P95 ms | 최대 ms | 판정 |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var (label, stats, target) in _times) sb.AppendLine(stats.ToRow(label, target));
        sb.AppendLine();
        sb.AppendLine("| 확인 | 결과 | 자세히 |");
        sb.AppendLine("|---|---|---|");
        foreach (var (name, ok, detail) in _checks) sb.AppendLine($"| {name} | {(ok ? "통과" : "실패")} | {detail.Replace("|", "/")} |");
        if (_notes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("관찰:");
            foreach (var n in _notes) sb.AppendLine("- " + n);
        }
        return sb.ToString();
    }

    public void Save(string dir)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "stage0_result.md"), Markdown(), new UTF8Encoding(false));
        var json = new
        {
            times = _times.Select(t => new { label = t.Label, target = t.Target, count = t.Stats.Count, mean = Round(t.Stats.Mean), p95 = Round(t.Stats.P95), max = Round(t.Stats.Max) }),
            checks = _checks.Select(x => new { name = x.Name, ok = x.Ok, detail = x.Detail }),
            notes = _notes,
        };
        File.WriteAllText(Path.Combine(dir, "stage0_result.json"), JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
    }

    private static double? Round(double v) => double.IsNaN(v) ? null : Math.Round(v, 3);
}
