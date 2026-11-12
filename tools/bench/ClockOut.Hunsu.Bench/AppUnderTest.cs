// 기능: 시험할 훈수꾼 앱을 띄우고, 앱이 남기는 기록(--log)을 따라 읽고, 등록 메시지로 앱을 움직인다(키보드·마우스 대신).
// 사용법: using var app = AppUnderTest.Launch(exe, targetHwnd, logPath, snapDir); var ready = app.WaitFor(l => l.Name == "ready", 10000);
//         app.Post(BenchMessages.OpenPanel, IntPtr.Zero, Stopwatch.GetTimestamp());
// 의존: Core/LogLine, Core/BenchMessages, Win32, Watchdog
using System.Diagnostics;
using System.Text;
using ClockOut.Hunsu.Core;

namespace ClockOut.Hunsu.Bench;

internal sealed class AppUnderTest : IDisposable
{
    private readonly Process _process;
    private readonly string _logPath;
    private FileStream? _stream;
    private StreamReader? _reader;
    private readonly StringBuilder _partial = new();
    private readonly Queue<LogLine> _pending = new();

    private AppUnderTest(Process p, string logPath)
    {
        _process = p;
        _logPath = logPath;
    }

    public IntPtr SignalHwnd { get; set; }
    public bool Alive => !_process.HasExited;
    public int Pid => _process.Id;

    public static AppUnderTest Launch(string exe, IntPtr follow, string logPath, string snapDir)
    {
        if (File.Exists(logPath)) File.Delete(logPath);
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false };
        psi.ArgumentList.Add("--follow"); psi.ArgumentList.Add(follow.ToInt64().ToString(System.Globalization.CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("--log"); psi.ArgumentList.Add(logPath);
        psi.ArgumentList.Add("--snapshot"); psi.ArgumentList.Add(snapDir);
        var p = Process.Start(psi) ?? throw new InvalidOperationException("앱을 시작하지 못했습니다: " + exe);
        Watchdog.Track(p.Id);
        return new AppUnderTest(p, logPath);
    }

    /// <summary>조건에 맞는 다음 기록 줄을 기다린다. 지나간 줄 중 맞지 않는 것은 버린다. 시간이 넘으면 null.</summary>
    public LogLine? WaitFor(Func<LogLine, bool> match, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (true)
        {
            while (_pending.Count > 0)
            {
                var l = _pending.Dequeue();
                if (match(l)) return l;
            }
            if (sw.ElapsedMilliseconds > timeoutMs || _process.HasExited && !ReadMore()) return null;
            if (!ReadMore()) Thread.Sleep(1);
        }
    }

    /// <summary>지금까지 쌓인 줄을 모두 버린다(다음 측정을 깨끗하게 시작).</summary>
    public void Drain()
    {
        while (ReadMore()) { }
        _pending.Clear();
    }

    private bool ReadMore()
    {
        if (_reader == null)
        {
            if (!File.Exists(_logPath)) return false;
            try
            {
                _stream = new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                _reader = new StreamReader(_stream, Encoding.UTF8);
            }
            catch (IOException) { return false; }
        }
        var buf = new char[4096];
        int n = _reader.Read(buf, 0, buf.Length);
        if (n <= 0) return false;
        for (int i = 0; i < n; i++)
        {
            if (buf[i] == '\n')
            {
                var line = LogLine.Parse(_partial.ToString());
                _partial.Clear();
                if (line != null) _pending.Enqueue(line);
            }
            else if (buf[i] != '\r') _partial.Append(buf[i]);
        }
        return true;
    }

    public void Post(string message, IntPtr wParam, long lParam) =>
        Win32.PostMessage(SignalHwnd, Win32.RegisterWindowMessage(message), wParam, new IntPtr(lParam));

    public void Dispose()
    {
        try
        {
            if (!_process.HasExited)
            {
                Post(BenchMessages.Quit, IntPtr.Zero, 0);
                if (!_process.WaitForExit(5000)) _process.Kill();
            }
        }
        catch (InvalidOperationException) { /* 이미 끝남 */ }
        Watchdog.EndProcess(_process.Id, TimeSpan.Zero);
        _reader?.Dispose();
        _stream?.Dispose();
        _process.Dispose();
    }
}
