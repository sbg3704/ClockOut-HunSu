// 기능: 훈수꾼 시작과 끝. 인자를 읽고 Shell/ShellController에 맡긴다.
// 사용법: ClockOut.Hunsu.exe                 보이는 엑셀 창(XLMAIN)을 찾아 따라간다(엑셀 COM에는 신호가 올 때만 붙는다)
//         ClockOut.Hunsu.exe --follow <hwnd> --log <파일> --snapshot <폴더> --layout <파일>   측정 도구가 쓰는 방식(Core/AppOptions)
// 의존: Shell/ShellController, Core/AppOptions, Core/SingleInstance(보통 모드는 하나만 켜짐)
using System.Windows;
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Shell;

namespace ClockOut.Hunsu;

public partial class App : Application
{
    private ShellController? _shell;
    private SingleInstance? _single;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var options = AppOptions.Parse(e.Args);
        // 보통 모드로 이미 켜진 훈수꾼이 있으면 이 복사본은 조용히 끝낸다(개발 빌드와 레일 설치본이 같이 켜지던 것을 막음).
        _single = SingleInstance.TryAcquire(options);
        if (_single is null) { Shutdown(0); return; }
        // 처리되지 않은 오류는 설정 파일 옆 logs 폴더에 남긴다(--layout을 주면 그 폴더 옆).
        string dataDir = options.LayoutPath != null
            ? System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(options.LayoutPath)) ?? "."
            : System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClockOut", "Hunsu");
        Diagnostics.ErrorLog.Install(System.IO.Path.Combine(dataDir, "logs"));
        _shell = new ShellController(options);
        _shell.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _shell?.Dispose();
        _single?.Dispose();
        base.OnExit(e);
    }
}
