// 기능: 훈수꾼 설정 창. 위의 [모듈] [모양] 두 쪽. [모듈]: 설치된 모듈을 분류 묶음별 한 줄씩 보여 주고 켜기·끄기, 자리(왼쪽 도크·오른쪽 도크·떼어 냄), 순서(위·아래)를 바꾸고, 배치를 처음으로 되돌리거나 설정 파일·모듈 폴더를 연다.
//       새 모듈을 받는 일은 칼퇴 왼쪽 레일(카탈로그 modules 구역)이 맡고, 여기서는 받은 모듈을 다룬다(docs/07).
//       [모양]: 도크 배치 모양·묶음·크기·아이콘 세트·쉴 때 진하기를 미리보기와 함께 고른다(Shell/LookPage).
// 사용법: 알림 영역 아이콘 → "모듈 관리…", 훈 원 우클릭 → "모듈 관리…" / "모양 바꾸기…", 또는 settings.json의 managerOnStart. new ManagerWindow(shell, "look").Show()
// 의존: Shell/ShellController(SetEnabled, MoveOrder, PlaceFromManager, ResetLayout, Modules, ModulesChanged), Shell/LookPage, Shell/ManagerParts, Ui Theme. 값은 Theme 이름으로만 쓴다.
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
    private readonly Border _body = new();
    private readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private ScrollViewer? _modulesView;
    private LookPage? _look;
    private GeneralPage? _general;
    private ProfilesPage? _profiles;
    private OptionsPage? _options;
    private string _page = "modules";

    public ManagerWindow(ShellController shell, string page = "modules")
    {
        _shell = shell;
        Title = "훈수꾼 · 설정";
        Width = 680;
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
        ShowPage(page);
    }

    /// <summary>[모듈] 또는 [모양] 쪽을 보인다("modules" | "look").</summary>
    internal void ShowPage(string page)
    {
        _page = page is "look" or "general" or "profiles" ? page : "modules";
        if (_page == "look")
        {
            _look ??= new LookPage(_shell);
            _body.Child = _look;
            _look.Rebuild();
        }
        else if (_page == "profiles")
        {
            _profiles ??= new ProfilesPage(_shell);
            _body.Child = _profiles;
            _profiles.Rebuild();
        }
        else if (_page == "options")
        {
            _options ??= new OptionsPage(_shell);
            _body.Child = _options;
            _options.Rebuild();
        }
        else if (_page == "general")
        {
            _general ??= new GeneralPage(_shell);
            _body.Child = _general;
            _general.Rebuild();
        }
        else _body.Child = _modulesView;
        _tabs.Children.Clear();
        _tabs.Children.Add(ManagerParts.Chip(this, "모듈", _page == "modules", () => ShowPage("modules")));
        _tabs.Children.Add(ManagerParts.Chip(this, "프로필", _page == "profiles", () => ShowPage("profiles")));
        _tabs.Children.Add(ManagerParts.Chip(this, "옵션", _page == "options", () => ShowPage("options")));
        _tabs.Children.Add(ManagerParts.Chip(this, "모양", _page == "look", () => ShowPage("look")));
        _tabs.Children.Add(ManagerParts.Chip(this, "일반", _page == "general", () => ShowPage("general")));
    }

    private UIElement BuildLayout()
    {
        var dock = new DockPanel();
        var header = new Grid { Margin = new Thickness(22, 18, 22, 8) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var logo = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(20),
            Background = (Brush)FindResource("Hunsu.Accent"),
            Margin = new Thickness(0, 0, 14, 0),
            Child = new TextBlock
            {
                Text = "훈",
                FontWeight = FontWeights.Bold,
                FontSize = (double)FindResource("Hunsu.FontTitleSize") + 3,
                Foreground = (Brush)FindResource("Hunsu.AccentInk"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        header.Children.Add(logo);
        var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(new TextBlock
        {
            Text = "설정  " + BuildInfo.Text,
            FontFamily = (FontFamily)FindResource("Hunsu.FontTitle"),
            FontSize = (double)FindResource("Hunsu.FontTitleSize") + 5,
            FontWeight = FontWeights.SemiBold,
            ToolTip = "훈수꾼 판 " + BuildInfo.Text,
        });
        _summary.Foreground = (Brush)FindResource(ThemeKeys.Muted);
        _summary.Margin = new Thickness(0, 2, 0, 0);
        titles.Children.Add(_summary);
        Grid.SetColumn(titles, 1);
        header.Children.Add(titles);
        Grid.SetColumn(_tabs, 2);
        header.Children.Add(_tabs);
        DockPanel.SetDock(header, Dock.Top);
        dock.Children.Add(header);

        var footer = new StackPanel { Margin = new Thickness(22, 10, 22, 14) };
        var buttons = new WrapPanel();
        buttons.Children.Add(ManagerParts.Chip(this, "배치 처음으로", false, ConfirmReset));
        buttons.Children.Add(ManagerParts.Chip(this, "모듈 폴더 열기", false, () => Open(Path.Combine(AppContext.BaseDirectory, "modules"))));
        footer.Children.Add(buttons);
        footer.Children.Add(new TextBlock
        {
            Text = "새 모듈은 칼퇴 왼쪽 레일에서 받습니다. 받은 모듈은 훈수꾼을 껐다 켜면 이 목록에 나타납니다. 처음 켜짐 여부, 조회 간격 같은 값은 [일반]에서 바꿉니다.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0),
            FontSize = (double)FindResource("Hunsu.FontCaption"),
            Foreground = (Brush)FindResource(ThemeKeys.Muted),
        });
        var footerBox = new Border { Child = footer, BorderBrush = (Brush)FindResource("Hunsu.Line"), BorderThickness = new Thickness(0, 1, 0, 0) };
        DockPanel.SetDock(footerBox, Dock.Bottom);
        dock.Children.Add(footerBox);

        _cards.Margin = new Thickness(22, 4, 22, 8);
        _modulesView = new ScrollViewer { Content = _cards, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        dock.Children.Add(_body);
        return dock;
    }

    private readonly HashSet<string> _folded = new();

    private void Rebuild()
    {
        _cards.Children.Clear();
        var list = _shell.Modules.OrderBy(m => m.Placement.Order).ToList();
        int on = list.Count(m => !m.Placement.Hidden);
        _summary.Text = $"설치된 모듈 {list.Count}개 · 켜짐 {on}개 · 묶음 제목을 누르면 접힘";
        var bulk = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 4, 0, 4) };
        bulk.Children.Add(ManagerParts.Chip(this, "모두 켜기", false, () => _shell.SetAllEnabled(true)));
        bulk.Children.Add(ManagerParts.Chip(this, "모두 끄기", false, () => _shell.SetAllEnabled(false)));
        _cards.Children.Add(bulk);
        if (list.Count == 0) _cards.Children.Add(new TextBlock { Text = "설치된 모듈이 없습니다. modules 폴더를 확인하세요.", Foreground = (Brush)FindResource(ThemeKeys.Muted), Margin = new Thickness(0, 12, 0, 0) });
        foreach (var g in list.GroupBy(m => string.IsNullOrWhiteSpace(m.Module.Manifest.Category) ? "기타" : m.Module.Manifest.Category).OrderBy(g => g.Min(m => m.Placement.Order)))
        {
            var members = g.ToList();
            bool folded = _folded.Contains(g.Key);
            _cards.Children.Add(GroupHeader(g.Key, members, folded));
            if (!folded) foreach (var mv in members) _cards.Children.Add(Row(mv));
        }
    }

    /// <summary>묶음 머리줄: 접기 화살표, 이름, 켜진 수, 묶음 전체 켜기·끄기.</summary>
    private UIElement GroupHeader(string name, List<ModuleView> members, bool folded)
    {
        int on = members.Count(m => !m.Placement.Hidden);
        var row = new DockPanel { Margin = new Thickness(0, 8, 0, 3), Background = Brushes.Transparent, Cursor = Cursors.Hand };
        var right = new StackPanel { Orientation = Orientation.Horizontal };
        right.Children.Add(ManagerParts.Chip(this, "묶음 켜기", false, () => { foreach (var m in members) _shell.SetEnabled(m, true); }));
        right.Children.Add(ManagerParts.Chip(this, "끄기", false, () => { foreach (var m in members) _shell.SetEnabled(m, false); }));
        DockPanel.SetDock(right, Dock.Right);
        row.Children.Add(right);
        row.Children.Add(new TextBlock
        {
            Text = $"{(folded ? "▸" : "▾")} {name}  {on}/{members.Count}",
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        });
        row.MouseLeftButtonUp += (_, _) => { if (!_folded.Remove(name)) _folded.Add(name); Rebuild(); };
        return row;
    }

    /// <summary>모듈 한 줄: 스위치, 이름, 짧은 설명(말줄임), 자리·순서 작은 단추.</summary>
    private UIElement Row(ModuleView mv)
    {
        var man = mv.Module.Manifest;
        bool enabled = !mv.Placement.Hidden;
        string place = mv.Placement.Place;
        var dock = new DockPanel { LastChildFill = true };
        var controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        controls.Children.Add(ManagerParts.Chip(this, "왼", place == Places.EdgeLeft, () => _shell.PlaceFromManager(mv, Places.EdgeLeft)));
        controls.Children.Add(ManagerParts.Chip(this, "오른", place == Places.EdgeRight, () => _shell.PlaceFromManager(mv, Places.EdgeRight)));
        controls.Children.Add(ManagerParts.Chip(this, "떼기", place == Places.Pin, () => _shell.PlaceFromManager(mv, Places.Pin)));
        controls.Children.Add(ManagerParts.Chip(this, "◐" + OpacityLevels.Short[Math.Clamp(mv.Placement.Opacity, 0, OpacityLevels.Short.Length - 1)], mv.Placement.Opacity != 0, () => _shell.CycleOpacity(mv)));
        controls.Children.Add(ManagerParts.Chip(this, "▲", false, () => _shell.MoveOrder(mv, -1)));
        controls.Children.Add(ManagerParts.Chip(this, "▼", false, () => _shell.MoveOrder(mv, 1)));
        DockPanel.SetDock(controls, Dock.Right);
        dock.Children.Add(controls);
        var toggle = ManagerParts.Switch(this, enabled, () => _shell.SetEnabled(mv, !enabled));
        toggle.VerticalAlignment = VerticalAlignment.Center;
        toggle.Margin = new Thickness(0, 0, 10, 0);
        DockPanel.SetDock(toggle, Dock.Left);
        dock.Children.Add(toggle);
        var text = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = man.Name, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 8, 0) });
        text.Children.Add(new TextBlock
        {
            Text = man.Description,
            FontSize = (double)FindResource("Hunsu.FontSmall"),
            Foreground = (Brush)FindResource(ThemeKeys.Muted),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 230,
        });
        dock.Children.Add(text);
        // 줄 어디에 올려도 전체 설명이 팝업으로 뜬다(설명이 잘려 있어도 읽을 수 있게).
        var tip = new TextBlock { Text = $"{man.Name}\n{man.Description}\n\n{man.Id} · v{man.Version} · {PlaceName(place)}", TextWrapping = TextWrapping.Wrap, MaxWidth = 340 };
        var rowBox = new Border
        {
            ToolTip = tip,
            Child = dock,
            Padding = new Thickness(10, 5, 8, 5),
            Margin = new Thickness(0, 0, 0, 3),
            CornerRadius = (CornerRadius)FindResource("Hunsu.PanelRadius"),
            Background = (Brush)FindResource("Hunsu.GlassAlt"),
            BorderBrush = (Brush)FindResource("Hunsu.Line"),
            BorderThickness = new Thickness(1),
            Opacity = enabled ? 1.0 : 0.55,
        };
        ToolTipService.SetInitialShowDelay(rowBox, 250);
        ToolTipService.SetShowDuration(rowBox, 20000);
        return rowBox;
    }

    private static string PlaceName(string place) => place switch
    {
        Places.EdgeLeft => "왼쪽 도크",
        Places.EdgeRight => "오른쪽 도크",
        Places.Pin => "떼어 낸 창",
        _ => place,
    };

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
