// 기능: 측정과 문제 찾기를 위한 한 줄 기록. --log를 주었을 때만 파일에 쓰고, 아니면 아무것도 하지 않는다.
// 사용법: log.Write("render", ("seq", 3), ("t", Stopwatch.GetTimestamp()));
//   줄 맨 앞에 쓴 시각(t=Stopwatch 눈금)을 붙인다. 측정 도구가 같은 PC의 같은 눈금으로 비교한다.
// 의존: Core/LogLine. 셀 값, 시트·책 이름, 경로는 넘기지 않는다(docs/04 7장). 여러 스레드에서 불러도 된다.
using System.Diagnostics;
using System.IO;
using ClockOut.Hunsu.Core;

namespace ClockOut.Hunsu.Diagnostics;

internal sealed class RunLog : IDisposable
{
    private readonly StreamWriter? _writer;
    private readonly object _gate = new();

    public RunLog(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            _writer = new StreamWriter(fs, new System.Text.UTF8Encoding(false)) { AutoFlush = true };
        }
        catch (IOException)
        {
            // 기록은 없어도 앱은 돌아야 한다(측정용 부가 기능).
            _writer = null;
        }
        catch (UnauthorizedAccessException)
        {
            _writer = null;
        }
    }

    public bool Enabled => _writer != null;

    public void Write(string name, params (string Key, object? Value)[] pairs)
    {
        if (_writer == null) return;
        var all = new (string, object?)[pairs.Length + 1];
        all[0] = ("at", Stopwatch.GetTimestamp());
        Array.Copy(pairs, 0, all, 1, pairs.Length);
        string line = LogLine.Format(name, all);
        lock (_gate)
        {
            try { _writer.WriteLine(line); }
            catch (IOException) { /* 디스크 문제로 못 써도 앱은 계속 */ }
        }
    }

    public void Dispose()
    {
        lock (_gate) _writer?.Dispose();
    }
}
