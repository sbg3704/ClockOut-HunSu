// 기능: 설정 창의 [모양] 쪽. 도크 배치 모양(5종), 기능 묶음, 크기, 아이콘 세트(6종), 쉴 때 진하기를 단추로 고르고, 위에서 "다가갔을 때 / 평소" 두 미리보기를 보여 준다(docs/09 2.1장).
//       고르면 바로 두 도크에 반영되고 settings.json에 저장된다(껐다 켤 필요 없음). 모듈의 묶음·순서·켜짐은 건드리지 않는다.
// 사용법: new LookPage(shell) 를 ManagerWindow가 [모양] 단추에서 보인다. shell.LookChanged가 오면 Rebuild로 다시 그린다.
// 의존: Shell/ShellController(Settings, ApplyLook, Modules, LookChanged), Shell/ManagerParts, Ui/Frame/LauncherView, Ui/Icons/IconFactory, Core/Layout(LauncherLayout), Core/Settings(DockLook).
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Core.Settings;
using ClockOut.Hunsu.Ui.Frame;
using ClockOut.Hunsu.Ui.Icons;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Shell;

internal sealed class LookPage : ScrollViewer
{
    private static readonly (string Key, string Name, string Note)[] Shapes =
    [
        (DockShapes.Crescent, "초승달", "기본. 가장자리에 얕은 곡선으로 얇게 붙는다"),
        ("fan", "부채", "훈 원에서 반원으로 펼친다. 평소엔 훈 원만"),
        ("cluster", "꽃송이", "큰 훈 원 둘레에 동그랗게 모인다"),
        ("rail", "자석 줄", "세로 한 줄. 커서 가까운 아이콘만 커진다"),
        ("pills", "알약 목록", "아이콘 옆에 이름과 숫자까지 보인다"),
    ];

    private static readonly (string Key, string Name, string License)[] Sets =
    [
        ("fluent", "Fluent", "MIT · Microsoft"),
        ("lucide", "Lucide", "ISC"),
        ("phosphor", "Phosphor", "MIT"),
        ("tabler", "Tabler", "MIT"),
        ("material", "Material", "Apache-2.0 · Google"),
        ("iconoir", "Iconoir", "MIT"),
    ];

    private static readonly string[] SampleIcons = ["sum", "warning", "sheets", "flow", "table"];
    private static readonly double[] IdleSteps = [0.2, 0.35, 0.45, 0.6, 0.8, 1.0];

    private readonly ShellController _shell;
    private readonly StackPanel _root = new() { Margin = new Thickness(22, 4, 22, 12) };

    public LookPage(ShellController shell)
    {
        _shell = shell;
        Content = _root;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        Loaded += (_, _) => _shell.LookChanged += Rebuild;
        Unloaded += (_, _) => _shell.LookChanged -= Rebuild;
    }

    private DockLook Look => _shell.Settings.Look;

    private void Set(DockLook look) => _shell.ApplyLook(look);

    internal void Rebuild()
    {
        var look = Look;
        _root.Children.Clear();
        _root.Children.Add(Previews(look));

        _root.Children.Add(ManagerParts.Caption(this, "배치 모양"));
        var shapes = new WrapPanel();
        foreach (var (key, name, _) in Shapes)
            shapes.Children.Add(Spaced(ManagerParts.Chip(this, name, look.Shape == key, () => Set(look with { Shape = key }))));
        _root.Children.Add(shapes);
        _root.Children.Add(Note(Shapes.FirstOrDefault(s => s.Key == look.Shape).Note ?? ""));

        _root.Children.Add(ManagerParts.Caption(this, "기능 묶음"));
        _root.Children.Add(Row(ManagerParts.Switch(this, look.Groups, () => Set(look with { Groups = !look.Groups })),
            look.Groups ? "상태 · 점검 · 이동 · 수식 · 참고로 묶고, 올리면 펼친다" : "모듈마다 아이콘 하나씩 바로 보인다"));

        _root.Children.Add(ManagerParts.Caption(this, "아이콘 크기"));
        var sizes = new WrapPanel();
        foreach (var (key, name) in new[] { ("s", "작게"), ("m", "보통"), ("l", "크게") })
            sizes.Children.Add(Spaced(ManagerParts.Chip(this, name, look.Size == key, () => Set(look with { Size = key }))));
        _root.Children.Add(sizes);

        _root.Children.Add(ManagerParts.Caption(this, "아이콘 세트"));
        var sets = new WrapPanel();
        foreach (var (key, name, license) in Sets) sets.Children.Add(SetCard(key, name, license, look.IconProfile == key, () => Set(look with { IconProfile = key })));
        _root.Children.Add(sets);
        _root.Children.Add(Note("세트를 바꾸면 모든 모듈 아이콘이 한꺼번에 바뀝니다. 출처와 라이선스는 vendor/SOURCES.md에 있습니다."));

        _root.Children.Add(ManagerParts.Caption(this, "평소 진하기 (커서가 멀 때)"));
        var idle = new WrapPanel();
        foreach (double v in IdleSteps)
            idle.Children.Add(Spaced(ManagerParts.Chip(this, $"{v * 100:0}%" + (v == DockLook.Default.IdleOpacity ? " 기본" : ""), Math.Abs(look.IdleOpacity - v) < 0.005, () => Set(look with { IdleOpacity = v }))));
        _root.Children.Add(idle);

        var reset = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 18, 0, 0) };
        reset.Children.Add(ManagerParts.Chip(this, "모양 처음으로", false, () => Set(DockLook.Default)));
        _root.Children.Add(reset);
    }

    /// <summary>다가갔을 때(깨어 있음)와 평소(잠듦) 두 미리보기. 실제 도크와 같은 부품을 켜진 모듈로 그린다.</summary>
    private UIElement Previews(DockLook look)
    {
        var mods = _shell.Modules.Where(m => !m.Placement.Hidden).ToList();
        if (mods.Count < 4) mods = _shell.Modules.ToList();
        var items = mods.Select(m => new LauncherModule(m.Id, m.Module.Manifest.Name, m.Placement.Order, m.Module.Manifest.Icon, m.Badge, m.Tone, false)).ToList();
        var grid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(Preview(look, items, awake: true, "다가갔을 때 (묶음에 올려 보세요)"));
        var rest = Preview(look, items, awake: false, "평소");
        Grid.SetColumn(rest, 2);
        grid.Children.Add(rest);
        return grid;
    }

    private FrameworkElement Preview(DockLook look, IReadOnlyList<LauncherModule> items, bool awake, string caption)
    {
        var view = new LauncherView { Width = 230, Height = 300, HorizontalAlignment = HorizontalAlignment.Right };
        view.Configure(look.Shape, look.Groups, LauncherShapes.Scale(look.Size), look.IdleOpacity, look.IconProfile, mirror: false);
        view.SetModules(items);
        view.Loaded += (_, _) => view.Hold = awake;
        var stage = new Border
        {
            Child = view,
            Height = 300,
            ClipToBounds = true,
            CornerRadius = (CornerRadius)FindResource("Hunsu.PanelRadius"),
            Background = (Brush)FindResource("Hunsu.GlassAlt"),
            BorderBrush = (Brush)FindResource("Hunsu.Line"),
            BorderThickness = new Thickness(1),
        };
        var box = new StackPanel();
        box.Children.Add(stage);
        box.Children.Add(Note(caption));
        return box;
    }

    /// <summary>아이콘 세트 고르기 칸: 견본 아이콘 다섯 개 + 이름 + 라이선스.</summary>
    private Border SetCard(string key, string name, string license, bool selected, Action act)
    {
        var ink = (Brush)FindResource(ThemeKeys.Text);
        var icons = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (string icon in SampleIcons)
        {
            var el = IconFactory.Create(key, icon, 20, ink, "·");
            el.Margin = new Thickness(0, 0, 8, 0);
            icons.Children.Add(el);
        }
        var body = new StackPanel();
        body.Children.Add(icons);
        body.Children.Add(new TextBlock { Text = name, FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal, Margin = new Thickness(0, 8, 0, 0) });
        body.Children.Add(new TextBlock { Text = license, FontSize = (double)FindResource("Hunsu.FontCaption"), Foreground = (Brush)FindResource(ThemeKeys.Muted) });
        var card = new Border
        {
            Child = body,
            Width = 160,
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 8, 8),
            CornerRadius = (CornerRadius)FindResource("Hunsu.PanelRadius"),
            Background = (Brush)FindResource(selected ? "Hunsu.GlassHover" : "Hunsu.GlassAlt"),
            BorderBrush = (Brush)FindResource(selected ? "Hunsu.Accent" : "Hunsu.Line"),
            BorderThickness = new Thickness(selected ? 2 : 1),
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        card.MouseLeftButtonUp += (_, e) => { e.Handled = true; act(); };
        return card;
    }

    private FrameworkElement Row(FrameworkElement control, string text)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        control.Margin = new Thickness(0, 0, 12, 0);
        row.Children.Add(control);
        row.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource(ThemeKeys.Muted), FontSize = (double)FindResource("Hunsu.FontSmall") });
        return row;
    }

    private TextBlock Note(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(2, 6, 0, 0),
        FontSize = (double)FindResource("Hunsu.FontCaption"),
        Foreground = (Brush)FindResource(ThemeKeys.Muted),
    };

    private static Border Spaced(Border chip)
    {
        chip.Margin = new Thickness(0, 0, 6, 6);
        return chip;
    }
}
