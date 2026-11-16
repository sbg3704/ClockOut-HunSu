// 기능: 예기치 못한 오류를 파일에 남긴다(원인 불명으로 꺼지거나 멈출 때 나중에 볼 수 있게). 화면 스레드, 다른 스레드, 작업(Task)에서 처리되지 않은 오류를 모두 받는다.
// 사용법: App 시작 때 ErrorLog.Install(logDir)을 한 번. 일부러 잡는 곳은 catch (Exception ex) { ErrorLog.Caught("어디서", ex); }. 파일: <logDir>\error-날짜.log (14일 지난 것은 지운다).
// 의존: Core/BuildLabel(판 글자), Shell/BuildInfo. 셀 값·시트 이름은 쓰지 않는다(오류 종류, 메시지, 호출 순서만. 메시지에 경로가 섞일 수 있으니 올릴 때 확인).
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using ClockOut.Hunsu.Shell;

namespace ClockOut.Hunsu.Diagnostics;

internal static class ErrorLog
{
    private static readonly object Gate = new();
    private static string? _dir;
    private static int _burst;
    private static DateTime _burstStart = DateTime.MinValue;

    public static string? Directory => _dir;

    /// <summary>처리되지 않은 오류 받는 곳을 건다. 기록 폴더를 못 만들면 조용히 포기한다(기록이 앱을 막지 않는다).</summary>
    public static void Install(string logDir)
    {
        try { System.IO.Directory.CreateDirectory(logDir); _dir = logDir; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return; }
        Prune();
        Write("시작", null, $"판 {BuildInfo.Text}, OS {Environment.OSVersion.VersionString}, .NET {Environment.Version}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Write(e.IsTerminating ? "치명(앱 종료)" : "처리 안 된 오류", e.ExceptionObject as Exception, "AppDomain");
        };
        TaskScheduler.UnobservedTaskException += (_, e) => { Write("Task 오류", e.Exception, "Task"); e.SetObserved(); };
        if (Application.Current != null)
            Application.Current.DispatcherUnhandledException += OnDispatcherError;
    }

    /// <summary>화면 스레드 오류: 기록하고, 가끔이면 앱을 계속 돌린다. 짧은 시간에 거듭되면(10초에 5번) 그대로 종료한다.</summary>
    private static void OnDispatcherError(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Write("화면 스레드 오류", e.Exception, "Dispatcher");
        var now = DateTime.UtcNow;
        if (now - _burstStart > TimeSpan.FromSeconds(10)) { _burstStart = now; _burst = 0; }
        if (++_burst <= 5) e.Handled = true;
        else Write("반복 오류로 종료", null, "10초 안에 5번 넘게 났다");
    }

    /// <summary>일부러 잡은 오류도 어디서 났는지 남긴다(잡고 넘어가는 자리에서 부른다).</summary>
    public static void Caught(string where, Exception ex) => Write("잡은 오류", ex, where);

    private static void Write(string kind, Exception? ex, string where)
    {
        if (_dir == null) return;
        var sb = new StringBuilder();
        sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append(" [").Append(kind).Append("] ").AppendLine(where);
        if (ex != null) sb.AppendLine(ex.ToString());
        try
        {
            lock (Gate) File.AppendAllText(Path.Combine(_dir, $"error-{DateTime.Now:yyyyMMdd}.log"), sb.ToString(), new UTF8Encoding(false));
        }
        catch (Exception io) when (io is IOException or UnauthorizedAccessException) { /* 기록 실패는 무시 */ }
    }

    private static void Prune()
    {
        try
        {
            foreach (var f in System.IO.Directory.EnumerateFiles(_dir!, "error-*.log"))
                if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-14)) File.Delete(f);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
