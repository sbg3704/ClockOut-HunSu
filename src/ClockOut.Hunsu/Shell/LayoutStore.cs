// 기능: 배치(LayoutState)를 파일에 읽고 쓴다. 바뀔 때마다 바로 쓰지 않고 0.5초 모아서 한 번 쓴다. 실패해도 앱은 메모리 값으로 계속 돈다(store_failed).
// 사용법: var store = new LayoutStore(path, log); store.State.Update(...); store.SaveSoon();  끝날 때 store.Flush()
// 의존: Core/Layout/LayoutState, Diagnostics/RunLog. 기본 파일: %LOCALAPPDATA%\ClockOut\Hunsu\layout.json. 임시 파일에 쓴 뒤 바꿔 끼워 반쯤 쓴 파일을 남기지 않는다.
using System.IO;
using System.Windows.Threading;
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Diagnostics;

namespace ClockOut.Hunsu.Shell;

internal sealed class LayoutStore
{
    private readonly string _path;
    private readonly RunLog _log;
    private readonly DispatcherTimer _timer;

    public LayoutStore(string? path, RunLog log)
    {
        _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClockOut", "Hunsu", "layout.json");
        _log = log;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (_, _) => Flush();
        State = Load();
    }

    public LayoutState State { get; }

    private LayoutState Load()
    {
        string? text = null;
        try { if (File.Exists(_path)) text = File.ReadAllText(_path); }
        catch (IOException) { _log.Write("store_failed", ("op", "read")); }
        catch (UnauthorizedAccessException) { _log.Write("store_failed", ("op", "read")); }
        var (state, ok) = LayoutState.FromJson(text);
        _log.Write("layout_load", ("ok", ok), ("modules", state.Modules.Count));
        return state;
    }

    public void SaveSoon()
    {
        _timer.Stop();
        _timer.Start();
    }

    public void Flush()
    {
        _timer.Stop();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            string tmp = _path + ".tmp";
            File.WriteAllText(tmp, State.ToJson());
            File.Move(tmp, _path, overwrite: true);
            _log.Write("layout_save", ("modules", State.Modules.Count));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Write("store_failed", ("op", "write"));
        }
    }
}
