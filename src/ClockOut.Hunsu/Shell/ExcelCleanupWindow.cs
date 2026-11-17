// 기능: [남은 엑셀 정리] 창. 지금 실행 중인 EXCEL.EXE를 목록으로 보이고(화면에 보이는 것 / 창이 없는 것), 골라서 강제로 끝내는 단추를 둔다.
//       창 없는 엑셀 일괄 끝내기, 목록 새로고침. 끝내기 전에 "저장 안 한 작업은 사라진다"는 확인을 늘 거친다(보이는 엑셀은 더 강하게).
// 사용법: new ExcelCleanupWindow(log).Show()  (훈 원 우클릭 / 알림 영역 아이콘 / 설정 창 [일반]의 "남은 엑셀 정리…")
// 의존: Excel/ExcelProcesses, Core/Procs/ExcelProc, Shell/WriteDialog(확인), Shell/ManagerParts(단추), Diagnostics/RunLog. 엑셀에 COM으로 말을 걸지 않는다.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClockOut.Hunsu.Core.Procs;
using ClockOut.Hunsu.Diagnostics;
using ClockOut.Hunsu.Excel;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Shell;

internal sealed class ExcelCleanupWindow : Window
{
    private readonly RunLog _log;
    private readonly StackPanel _list = new();
    private readonly TextBlock _status = new() { Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap };

    public ExcelCleanupWindow(RunLog log)
    {
        _log = log;
        Title = "훈수꾼 · 남은 엑셀 정리";
        Width = 520;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 560;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = (Brush)FindResource("Hunsu.Surface");
        Foreground = (Brush)FindResource(ThemeKeys.Text);
        FontFamily = (FontFamily)FindResource("Hunsu.Font");
        FontSize = (double)FindResource("Hunsu.FontBody");
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

        var root = new StackPanel { Margin = new Thickness(22, 18, 22, 16) };
        root.Children.Add(new TextBlock { Text = "남은 엑셀 정리", FontSize = (double)FindResource("Hunsu.FontTitleSize"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
        root.Children.Add(new TextBlock
        {
            Text = "엑셀을 닫았는데 작업 관리자에 EXCEL.EXE가 남아 있을 때 쓰세요. 창이 없는 엑셀은 보통 찌꺼기입니다. 강제로 끝내면 저장하지 않은 작업은 사라집니다.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource(ThemeKeys.Muted),
            FontSize = (double)FindResource("Hunsu.FontSmall"),
            Margin = new Thickness(0, 0, 0, 10),
        });
        root.Children.Add(new ScrollViewer { Content = _list, MaxHeight = 300, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(ManagerParts.Chip(this, "새로고침", false, Rebuild));
        buttons.Children.Add(ManagerParts.Chip(this, "창 없는 엑셀 모두 끝내기", false, EndAllWindowless));
        buttons.Children.Add(ManagerParts.Chip(this, "닫기", true, Close));
        root.Children.Add(buttons);
        _status.FontSize = (double)FindResource("Hunsu.FontSmall");
        _status.Foreground = (Brush)FindResource(ThemeKeys.Muted);
        root.Children.Add(_status);
        Content = root;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        Rebuild();
    }

    private void Rebuild()
    {
        _list.Children.Clear();
        var procs = ExcelProcesses.Snapshot().OrderByDescending(p => p.HasVisibleWindow).ThenBy(p => p.StartTicks).ToList();
        if (procs.Count == 0)
        {
            _list.Children.Add(new TextBlock { Text = "실행 중인 엑셀이 없습니다.", Margin = new Thickness(0, 6, 0, 6) });
            return;
        }
        foreach (var p in procs) _list.Children.Add(Row(p));
    }

    private FrameworkElement Row(ExcelProc p)
    {
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var started = new DateTime(p.StartTicks, DateTimeKind.Utc).ToLocalTime();
        string what = p.HasVisibleWindow
            ? "화면에 보이는 엑셀" + (p.Title.Length > 0 ? " · " + p.Title : "")
            : "창 없는 엑셀 (닫고 남은 찌꺼기일 수 있음)";
        var text = new StackPanel();
        text.Children.Add(new TextBlock { Text = what, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = what, Foreground = ThemeKeys.Tone(this, p.HasVisibleWindow ? "info" : "warn") });
        text.Children.Add(new TextBlock
        {
            Text = $"번호 {p.Pid} · 시작 {started:HH:mm}",
            FontSize = (double)FindResource("Hunsu.FontCaption"),
            Foreground = (Brush)FindResource(ThemeKeys.Muted),
        });
        grid.Children.Add(text);
        var end = ManagerParts.Chip(this, "강제로 끝내기", false, () => EndOne(p));
        Grid.SetColumn(end, 1);
        grid.Children.Add(end);
        return grid;
    }

    private void EndOne(ExcelProc p)
    {
        string body = p.HasVisibleWindow
            ? "지금 화면에 열려 있는 엑셀입니다. 강제로 끝내면 저장하지 않은 통합 문서가 모두 사라집니다. 정말 끝낼까요?"
            : "창이 없는 엑셀입니다. 저장하지 않은 작업이 있으면 사라집니다. 끝낼까요?";
        if (WriteDialog.Ask($"엑셀 {p.Pid}번 강제로 끝내기", body, "끝내기", "취소", null, p.HasVisibleWindow ? "bad" : "warn") != WriteDialog.Choice.Ok) return;
        bool ok = ExcelProcesses.End(p.Pid, p.StartTicks);
        _log.Write("excel_end", ("pid", p.Pid), ("visible", p.HasVisibleWindow), ("manual", true), ("ok", ok));
        _status.Text = ok ? $"엑셀 {p.Pid}번을 끝냈습니다." : $"엑셀 {p.Pid}번을 끝내지 못했습니다(권한이 없을 수 있습니다).";
        Rebuild();
    }

    private void EndAllWindowless()
    {
        var targets = ExcelProcesses.Snapshot().Where(p => !p.HasVisibleWindow).ToList();
        if (targets.Count == 0) { _status.Text = "창 없는 엑셀이 없습니다."; return; }
        if (WriteDialog.Ask("창 없는 엑셀 모두 끝내기", $"창 없는 엑셀 {targets.Count}개를 강제로 끝냅니다. 다른 프로그램이 몰래 쓰는 엑셀이면 그 작업이 중단됩니다. 끝낼까요?", "끝내기", "취소") != WriteDialog.Choice.Ok) return;
        int done = 0;
        foreach (var p in targets)
        {
            bool ok = ExcelProcesses.End(p.Pid, p.StartTicks);
            _log.Write("excel_end", ("pid", p.Pid), ("visible", false), ("manual", true), ("ok", ok));
            if (ok) done++;
        }
        _status.Text = $"{done}개를 끝냈습니다." + (done < targets.Count ? $" {targets.Count - done}개는 끝내지 못했습니다." : "");
        Rebuild();
    }
}
