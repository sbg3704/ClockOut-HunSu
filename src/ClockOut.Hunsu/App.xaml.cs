// 기능: 훈수꾼 시작과 끝. 인자를 읽고 Shell/ShellController에 맡긴다.
// 사용법: ClockOut.Hunsu.exe                 보이는 엑셀 창(XLMAIN)을 찾아 따라간다(엑셀 COM에는 신호가 올 때만 붙는다)
//         ClockOut.Hunsu.exe --follow <hwnd> --log <파일> --snapshot <폴더> --layout <파일>   측정 도구가 쓰는 방식(Core/AppOptions)
// 의존: Shell/ShellController, Core/AppOptions
using System.Windows;
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Shell;

namespace ClockOut.Hunsu;

public partial class App : Application
{
    private ShellController? _shell;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _shell = new ShellController(AppOptions.Parse(e.Args));
        _shell.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _shell?.Dispose();
        base.OnExit(e);
    }
}
