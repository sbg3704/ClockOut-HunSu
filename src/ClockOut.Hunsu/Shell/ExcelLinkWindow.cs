// 기능: [엑셀 연결] 창. 지금 실행 중인 엑셀 창(통합 문서 이름, 프로세스 번호, 시작 시각)을 목록으로 보이고, 골라서 훈수꾼이 따라갈 엑셀로 연결한다. "창 눌러 고르기"와 "자동으로 되돌리기"도 둔다.
// 사용법: shell.OpenExcelLink()가 연다(훈 원 우클릭 / 알림 영역 아이콘 / 사이드바 하단 🔗).
// 의존: Shell/ShellController.Link(연결·풀기·고르기), Excel/ExcelWindows, Shell/ManagerParts(단추). 엑셀에 COM으로 말을 걸지 않는다.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClockOut.Hunsu.Excel;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Shell;

internal sealed class ExcelLinkWindow : Window
{
    private readonly ShellController _shell;
    private readonly StackPanel _list = new();
    private readonly TextBlock _now = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
    private readonly TextBlock _status = new() { Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap };

    public ExcelLinkWindow(ShellController shell)
    {
        _shell = shell;
        Title = "훈수꾼 · 엑셀 연결";
        Width = 520;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 560;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetResourceReference(BackgroundProperty, "Hunsu.Surface");
        SetResourceReference(ForegroundProperty, ThemeKeys.Text);
        SetResourceReference(FontFamilyProperty, "Hunsu.Font");
        FontSize = (double)FindResource("Hunsu.FontBody");
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

        var root = new StackPanel { Margin = new Thickness(22, 18, 22, 16) };
        root.Children.Add(new TextBlock { Text = "엑셀 연결", FontSize = (double)FindResource("Hunsu.FontTitleSize"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
        root.Children.Add(new TextBlock
        {
            Text = "훈수꾼이 엑셀을 자동으로 못 찾거나 엉뚱한 창을 따라갈 때, 따라갈 엑셀을 직접 고르세요. 연결하면 다른 엑셀 창이 앞에 나와도 그 엑셀만 따라가고, 기억해 둡니다.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource(ThemeKeys.Muted),
            FontSize = (double)FindResource("Hunsu.FontSmall"),
            Margin = new Thickness(0, 0, 0, 10),
        });
        root.Children.Add(_now);
        root.Children.Add(new ScrollViewer { Content = _list, MaxHeight = 300, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(ManagerParts.Chip(this, "새로고침", false, Rebuild));
        buttons.Children.Add(ManagerParts.Chip(this, "창 눌러 고르기", false, PickWindow));
        buttons.Children.Add(ManagerParts.Chip(this, "자동으로 되돌리기", false, () => { _shell.UnlinkExcel(); _status.Text = "자동 찾기로 되돌렸습니다."; Rebuild(); }));
        buttons.Children.Add(ManagerParts.Chip(this, "닫기", true, Close));
        root.Children.Add(buttons);
        _status.FontSize = (double)FindResource("Hunsu.FontSmall");
        _status.Foreground = (Brush)FindResource(ThemeKeys.Muted);
        root.Children.Add(_status);
        Content = root;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        Rebuild();
    }

    private void PickWindow()
    {
        _status.Text = "연결할 엑셀 창을 누르세요(Esc 취소). 이 창은 잠시 숨습니다.";
        Hide();
        _shell.BeginPickExcelWindow(msg =>
        {
            Show();
            Activate();
            _status.Text = msg;
            Rebuild();
        });
    }

    private void Rebuild()
    {
        _now.Text = _shell.IsLinked ? "지금 연결: " + _shell.LinkedLabel : "지금 연결: 없음 (자동으로 찾는 중)";
        _now.Foreground = ThemeKeys.Tone(this, _shell.IsLinked ? "info" : "muted");
        _list.Children.Clear();
        var wins = ExcelWindows.List().OrderByDescending(w => w.Visible).ThenBy(w => w.StartTicks).ToList();
        if (wins.Count == 0)
        {
            _list.Children.Add(new TextBlock { Text = "엑셀 창이 없습니다. 엑셀을 열고 새로고침하세요. (창 없이 남은 엑셀은 「남은 엑셀 정리」에서 볼 수 있어요)", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 6) });
            return;
        }
        foreach (var w in wins) _list.Children.Add(Row(w));
    }

    private FrameworkElement Row(ExcelWin w)
    {
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        string title = w.Title.Length > 0 ? w.Title : "(제목 없음)";
        var text = new StackPanel();
        text.Children.Add(new TextBlock { Text = title + (w.Visible ? "" : " · 숨은 창"), TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = title, Foreground = ThemeKeys.Tone(this, w.Visible ? "info" : "warn") });
        string started = w.StartTicks > 0 ? $" · 시작 {new DateTime(w.StartTicks, DateTimeKind.Utc).ToLocalTime():HH:mm}" : "";
        text.Children.Add(new TextBlock { Text = $"프로세스 {w.Pid}번{started}", FontSize = (double)FindResource("Hunsu.FontCaption"), Foreground = (Brush)FindResource(ThemeKeys.Muted) });
        grid.Children.Add(text);
        var pick = ManagerParts.Chip(this, w.Hwnd == _shell.LinkedMain() ? "연결됨" : "연결", false, () => { _shell.LinkExcel(w); _status.Text = $"엑셀 {w.Pid}번에 연결했습니다."; Rebuild(); });
        Grid.SetColumn(pick, 1);
        grid.Children.Add(pick);
        return grid;
    }
}
