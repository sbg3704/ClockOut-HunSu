// 기능: 모듈 관리자 창. 설치된 모듈을 카드로 보여 주고 켜기·끄기, 자리(왼쪽 도크·오른쪽 도크·떼어 냄), 순서(위·아래)를 바꾸고, 배치를 처음으로 되돌리거나 설정 파일·모듈 폴더를 연다.
//       새 모듈을 받는 일은 칼퇴 왼쪽 레일(카탈로그 modules 구역)이 맡고, 여기서는 받은 모듈을 다룬다(docs/07).
// 사용법: 알림 영역 아이콘 → "모듈 관리…", 오른쪽 알약 우클릭 → "모듈 관리…", 또는 settings.json의 managerOnStart. new ManagerWindow(shell).Show()
// 의존: Shell/ShellController(SetEnabled, MoveOrder, PlaceFromManager, ResetLayout, Modules, ModulesChanged), Ui Theme. 값은 Theme 이름으로만 쓴다.
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Shell;

internal sealed class ManagerWindow : Window
{
    private readonly ShellController _shell;
    private readonly StackPanel _cards = new();
    private readonly TextBlock _summary = new();

    public ManagerWindow(ShellController shell)
    {
        _shell = shell;
        Title = "훈수꾼 · 모듈 관리";
        Width = 580;
        Height = 660;
        MinWidth = 480;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = (Brush)FindResource("Hunsu.Surface");
        Foreground = (Brush)FindResource(ThemeKeys.Text);
        FontFamily = (FontFamily)FindResource("Hunsu.Font");
        FontSize = (double)FindResource("Hunsu.FontBody");
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        Content = BuildLayout();
        _shell.ModulesChanged += Rebuild;
        Closed += (_, _) => _shell.ModulesChanged -= Rebuild;
        Rebuild();
    }

    private UIElement BuildLayout()
    {
        var dock = new DockPanel();
        var header = new StackPanel { Margin = new Thickness(22, 18, 22, 8) };
        header.Children.Add(new TextBlock
        {
            Text = "모듈 관리",
            FontFamily = (FontFamily)FindResource("Hunsu.FontTitle"),
            FontSize = (double)FindResource("Hunsu.FontTitleSize") + 5,
            FontWeight = FontWeights.SemiBold,
        });
        _summary.Foreground = (Brush)FindResource(ThemeKeys.Muted);
        _summary.Margin = new Thickness(0, 4, 0, 0);
        header.Children.Add(_summary);
        DockPanel.SetDock(header, Dock.Top);
        dock.Children.Add(header);

        var footer = new StackPanel { Margin = new Thickness(22, 10, 22, 14) };
        var buttons = new WrapPanel();
        buttons.Children.Add(Chip("배치 처음으로", false, ConfirmReset));
        buttons.Children.Add(Chip("설정 파일 열기", false, () => Open(_shell.SettingsPath)));
        buttons.Children.Add(Chip("모듈 폴더 열기", false, () => Open(Path.Combine(AppContext.BaseDirectory, "modules"))));
        footer.Children.Add(buttons);
        footer.Children.Add(new TextBlock
        {
            Text = "새 모듈은 칼퇴 왼쪽 레일에서 받습니다. 받은 모듈은 훈수꾼을 껐다 켜면 이 목록에 나타납니다. 처음 켜짐 여부, 조회 간격 같은 값은 설정 파일에서 바꿉니다.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0),
            FontSize = (double)FindResource("Hunsu.FontCaption"),
            Foreground = (Brush)FindResource(ThemeKeys.Muted),
        });
        var footerBox = new Border { Child = footer, BorderBrush = (Brush)FindResource("Hunsu.Line"), BorderThickness = new Thickness(0, 1, 0, 0) };
        DockPanel.SetDock(footerBox, Dock.Bottom);
        dock.Children.Add(footerBox);

        _cards.Margin = new Thickness(22, 4, 22, 8);
        dock.Children.Add(new ScrollViewer { Content = _cards, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        return dock;
    }

    private void Rebuild()
    {
        _cards.Children.Clear();
        var list = _shell.Modules.OrderBy(m => m.Placement.Hidden).ThenBy(m => m.Placement.Place, StringComparer.Ordinal).ThenBy(m => m.Placement.Order).ToList();
        int on = list.Count(m => !m.Placement.Hidden);
        _summary.Text = $"설치된 모듈 {list.Count}개 · 켜짐 {on}개";
        if (list.Count == 0) _cards.Children.Add(new TextBlock { Text = "설치된 모듈이 없습니다. modules 폴더를 확인하세요.", Foreground = (Brush)FindResource(ThemeKeys.Muted), Margin = new Thickness(0, 12, 0, 0) });
        foreach (var mv in list) _cards.Children.Add(Card(mv));
    }

    private UIElement Card(ModuleView mv)
    {
        var man = mv.Module.Manifest;
        bool enabled = !mv.Placement.Hidden;
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var toggle = Switch(enabled, () => _shell.SetEnabled(mv, !enabled));
        toggle.VerticalAlignment = VerticalAlignment.Top;
        toggle.Margin = new Thickness(0, 2, 14, 0);
        grid.Children.Add(toggle);

        var info = new StackPanel();
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
        titleRow.Children.Add(new TextBlock { Text = man.Name, FontWeight = FontWeights.SemiBold });
        titleRow.Children.Add(new TextBlock { Text = $"  v{man.Version} · {man.Category}", FontSize = (double)FindResource("Hunsu.FontCaption"), Foreground = (Brush)FindResource(ThemeKeys.Muted), VerticalAlignment = VerticalAlignment.Center });
        info.Children.Add(titleRow);
        info.Children.Add(new TextBlock { Text = man.Description, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 3), FontSize = (double)FindResource("Hunsu.FontSmall"), Foreground = (Brush)FindResource(ThemeKeys.Muted) });
        info.Children.Add(new TextBlock { Text = man.Id, FontSize = (double)FindResource("Hunsu.FontCaption"), Foreground = (Brush)FindResource(ThemeKeys.Muted), Opacity = 0.7 });
        Grid.SetColumn(info, 1);
        grid.Children.Add(info);

        var controls = new StackPanel { Margin = new Thickness(12, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
        string place = mv.Placement.Place;
        var where = new StackPanel { Orientation = Orientation.Horizontal };
        where.Children.Add(Chip("왼쪽", place == Places.EdgeLeft, () => _shell.PlaceFromManager(mv, Places.EdgeLeft)));
        where.Children.Add(Chip("오른쪽", place == Places.EdgeRight, () => _shell.PlaceFromManager(mv, Places.EdgeRight)));
        where.Children.Add(Chip("떼기", place == Places.Pin, () => _shell.PlaceFromManager(mv, Places.Pin)));
        controls.Children.Add(where);
        var order = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
        order.Children.Add(Chip("▲ 위", false, () => _shell.MoveOrder(mv, -1)));
        order.Children.Add(Chip("▼ 아래", false, () => _shell.MoveOrder(mv, 1)));
        controls.Children.Add(order);
        Grid.SetColumn(controls, 2);
        grid.Children.Add(controls);

        return new Border
        {
            Child = grid,
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 10),
            CornerRadius = (CornerRadius)FindResource("Hunsu.PanelRadius"),
            Background = (Brush)FindResource("Hunsu.GlassAlt"),
            BorderBrush = (Brush)FindResource("Hunsu.Line"),
            BorderThickness = new Thickness(1),
            Opacity = enabled ? 1.0 : 0.6,
        };
    }

    /// <summary>켜짐/꺼짐 스위치(누르면 act).</summary>
    private FrameworkElement Switch(bool on, Action act)
    {
        var knob = new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(8),
            Background = (Brush)FindResource(on ? "Hunsu.AccentInk" : "Hunsu.Text"),
            HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Margin = new Thickness(3),
        };
        var track = new Border
        {
            Width = 42,
            Height = 22,
            CornerRadius = new CornerRadius(11),
            Background = on ? (Brush)FindResource("Hunsu.Accent") : (Brush)FindResource("Hunsu.Line"),
            Child = knob,
            Cursor = Cursors.Hand,
            ToolTip = on ? "끄기" : "켜기",
        };
        track.MouseLeftButtonUp += (_, e) => { e.Handled = true; act(); };
        return track;
    }

    /// <summary>작은 단추. selected이면 강조색.</summary>
    private Border Chip(string text, bool selected, Action act)
    {
        Brush rest = selected ? (Brush)FindResource("Hunsu.Accent") : (Brush)FindResource("Hunsu.GlassAlt");
        var label = new TextBlock
        {
            Text = text,
            FontSize = (double)FindResource("Hunsu.FontSmall"),
            Foreground = (Brush)FindResource(selected ? "Hunsu.AccentInk" : ThemeKeys.Text),
            FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal,
        };
        var chip = new Border
        {
            Child = label,
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 0, 6, 0),
            CornerRadius = (CornerRadius)FindResource("Hunsu.RowRadius"),
            Background = rest,
            BorderBrush = (Brush)FindResource("Hunsu.Line"),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
        };
        Brush hover = (Brush)FindResource("Hunsu.GlassHover");
        if (!selected)
        {
            chip.MouseEnter += (_, _) => chip.Background = hover;
            chip.MouseLeave += (_, _) => chip.Background = rest;
        }
        chip.MouseLeftButtonUp += (_, e) => { e.Handled = true; act(); };
        return chip;
    }

    private void ConfirmReset()
    {
        var answer = MessageBox.Show(this, "모든 모듈의 자리·순서·켜짐 상태를 처음 값으로 되돌립니다. 계속할까요?", "배치 처음으로", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer == MessageBoxResult.OK) _shell.ResetLayout();
    }

    private static void Open(string path)
    {
        try { Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or FileNotFoundException) { /* 열 프로그램이 없으면 아무 일도 하지 않는다 */ }
    }
}
