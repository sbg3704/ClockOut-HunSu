// 기능: 설정 창의 [일반] 쪽. settings.json에 있던 값(화면 테마, 패널 너비, 배치 잠금, 목록 줄 수, 조회 간격, 시작할 때 설정 창 열기, 새 모듈 처음부터 켜기)을 파일을 열지 않고 단추로 바꾼다.
//       고르면 바로 저장된다. 배치 잠금과 목록 줄 수는 바로 반영되고, 나머지는 훈수꾼을 껐다 켜면 반영된다(칸마다 안내).
// 사용법: new GeneralPage(shell) 를 ManagerWindow가 [일반] 단추에서 보인다. 값은 shell.Settings를 읽고 shell.ApplySetting(키, 값)으로 바꾼다.
// 의존: Shell/ShellController(Settings, ApplySetting, ResetGeneralSettings, SettingsChanged), Shell/ManagerParts. 값은 Theme 이름으로만 쓴다.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Shell;

internal sealed class GeneralPage : ScrollViewer
{
    private readonly ShellController _shell;
    private readonly StackPanel _root = new() { Margin = new Thickness(22, 4, 22, 12) };

    public GeneralPage(ShellController shell)
    {
        _shell = shell;
        Content = _root;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        Loaded += (_, _) => _shell.SettingsChanged += Rebuild;
        Unloaded += (_, _) => _shell.SettingsChanged -= Rebuild;
    }

    internal void Rebuild()
    {
        var s = _shell.Settings;
        _root.Children.Clear();

        Section("화면 테마", "껐다 켜면 바뀝니다");
        Choices("theme", s.Theme, [("dark", "어둡게"), ("light", "밝게"), ("auto", "Windows 따라")]);

        Section("패널 너비", "껐다 켜면 바뀝니다");
        Choices("panelWidth", s.PanelWidth, [(200, "좁게 200"), (240, "기본 240"), (300, "300"), (360, "360"), (480, "넓게 480")]);

        Section("패널 위치 잠금", "바로 반영");
        Choices("layoutLock", s.LayoutLock, [("locked", "잠금 (Shift+끌기로 옮김)"), ("free", "자유 (그냥 끌어서 옮김)")]);

        Section("목록에 한 번에 보일 줄 수", "바로 반영");
        Choices("listMaxRows", s.ListMaxRows, [(20, "20"), (50, "50 기본"), (100, "100"), (200, "200")]);

        Section("범위를 끄는 동안 숫자 갱신 간격", "껐다 켜면 바뀝니다. 클수록 엑셀이 덜 바쁩니다");
        Choices("queryDragGapMs", s.QueryDragGapMs, [(70, "70ms"), (150, "150ms 기본"), (300, "300ms"), (600, "600ms")]);

        Section("엑셀이 느릴 때 바쁨 표시 시작", "껐다 켜면 바뀝니다");
        Choices("staleAfterMs", s.StaleAfterMs, [(300, "0.3초"), (600, "0.6초 기본"), (1500, "1.5초"), (3000, "3초")]);

        Section("셀 위 표시 다시 맞추는 간격", "껐다 켜면 바뀝니다. 0은 엑셀 신호 때만");
        Choices("markPollMs", s.MarkPollMs, [(0, "신호 때만"), (200, "200ms"), (400, "400ms 기본"), (1000, "1초")]);

        Section("패널 투명도", "패널마다 투명도는 [모듈] 쪽 줄의 ◐ 단추나 패널 제목줄의 투명도 단추로 바꿉니다");
        _root.Children.Add(Row(ManagerParts.Switch(this, s.HoverSharpen, () => _shell.ApplySetting("hoverSharpen", !s.HoverSharpen)), "마우스를 올리면 선명하게 (끄면 정한 투명도 그대로)"));

        Section("시작할 때", "다음 실행부터");
        _root.Children.Add(Row(ManagerParts.Switch(this, s.ManagerOnStart, () => _shell.ApplySetting("managerOnStart", !s.ManagerOnStart)), "설정 창을 바로 연다"));
        bool all = s.EnabledByDefault.Contains("*");
        _root.Children.Add(Row(ManagerParts.Switch(this, all, () => _shell.ApplySetting("enabledByDefault", all ? new[] { "hunsu-selection" } : new[] { "*" })), "처음 쓰는 모듈은 모두 켠다 (이미 켜고 끈 모듈은 그대로)"));

        var buttons = new WrapPanel { Margin = new Thickness(0, 18, 0, 0) };
        buttons.Children.Add(ManagerParts.Chip(this, "일반 설정 처음으로", false, _shell.ResetGeneralSettings));
        buttons.Children.Add(ManagerParts.Chip(this, "설정 파일 열기 (고급)", false, _shell.OpenSettingsFile));
        _root.Children.Add(buttons);
    }

    private void Section(string title, string note)
    {
        var head = new StackPanel { Orientation = Orientation.Horizontal };
        head.Children.Add(ManagerParts.Caption(this, title));
        head.Children.Add(new TextBlock
        {
            Text = "  " + note,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 2),
            FontSize = (double)FindResource("Hunsu.FontCaption"),
            Foreground = (Brush)FindResource(ThemeKeys.Muted),
            Opacity = 0.8,
        });
        _root.Children.Add(head);
    }

    private void Choices<T>(string key, T current, (T Value, string Name)[] options) where T : notnull
    {
        var wrap = new WrapPanel();
        foreach (var (value, name) in options)
        {
            var chip = ManagerParts.Chip(this, name, EqualityComparer<T>.Default.Equals(current, value), () => _shell.ApplySetting(key, value));
            chip.Margin = new Thickness(0, 0, 6, 6);
            wrap.Children.Add(chip);
        }
        _root.Children.Add(wrap);
    }

    private FrameworkElement Row(FrameworkElement control, string text)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        control.Margin = new Thickness(0, 0, 12, 0);
        row.Children.Add(control);
        row.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource(ThemeKeys.Muted), FontSize = (double)FindResource("Hunsu.FontSmall") });
        return row;
    }
}
