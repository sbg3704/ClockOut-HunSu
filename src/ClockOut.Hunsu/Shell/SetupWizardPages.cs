// 기능: 설정 마법사의 쪽마다 고르는 칸. 시작(무엇을 맞추는지), 하는 일(작업 프로필 카드), 자리(사이드바 오른쪽·왼쪽·엑셀 안 도크 + 도크 모양),
//       모양(패널 룩 4종, 아이콘 세트 6종, 크기), 쓰는 법(단축키·우클릭·켜고 끄는 도움), 끝(고른 것 요약).
//       고르면 설정 창과 같은 길(ApplyProfile, ApplySetting, ApplyLook)로 바로 반영·저장된다. 창(SetupWizardWindow)이 바뀐 값을 받아 다시 짓는다.
// 사용법: new SetupWizardPages(shell, window).Build("work") → 그 쪽 내용 부품
// 의존: Shell/ShellController(SkippedModules: 칼퇴에서 끈 모듈), Core/Setup/SetupWizard, Core/Layout/Profiles, Core/Settings(SkinChoice, DockLook), Shell/ManagerParts, Ui/Icons/IconFactory, Ui Theme.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Core.Settings;
using ClockOut.Hunsu.Core.Setup;
using ClockOut.Hunsu.Ui.Icons;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Shell;

internal sealed class SetupWizardPages
{
    private static readonly string[] Shapes = [DockShapes.Crescent, "fan", "cluster", "rail", "pills"];
    private static readonly string[] SampleIcons = ["sum", "warning", "sheets", "flow", "table"];

    private readonly ShellController _shell;
    private readonly FrameworkElement _r;

    public SetupWizardPages(ShellController shell, FrameworkElement resources)
    {
        _shell = shell;
        _r = resources;
    }

    private HunsuSettings S => _shell.Settings;

    public FrameworkElement Build(string id) => id switch
    {
        "hello" => Hello(),
        "work" => Work(),
        "place" => Place(),
        "look" => Look(),
        "tour" => Tour(),
        _ => Done(),
    };

    // ---- 시작 ----
    private FrameworkElement Hello()
    {
        var box = new StackPanel();
        foreach (var (no, title, note) in new[]
        {
            ("1", "하는 일", "정산, 데이터 정리, 검수처럼 내 일에 맞는 모듈만 켭니다"),
            ("2", "자리와 모양", "사이드바를 어디에 둘지, 어떤 느낌으로 보일지 고릅니다"),
            ("3", "쓰는 법", "꼭 알아야 할 단축키와 메뉴 세 가지만 알려 드립니다"),
        })
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            var badge = new Border
            {
                Width = 30, Height = 30, CornerRadius = new CornerRadius(15), Margin = new Thickness(0, 0, 14, 0),
                Background = Brush("Hunsu.Accent"), VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock { Text = no, FontWeight = FontWeights.Bold, Foreground = Brush("Hunsu.AccentInk"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            };
            DockPanel.SetDock(badge, Dock.Left);
            row.Children.Add(badge);
            var text = new StackPanel();
            text.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold });
            text.Children.Add(Note(note));
            row.Children.Add(text);
            box.Children.Add(row);
        }
        box.Children.Add(Note("고른 것은 바로 엑셀 옆에 반영됩니다. 마음에 안 들면 이전으로 돌아가 다시 고르면 돼요."));
        return box;
    }

    // ---- 하는 일 ----
    private FrameworkElement Work()
    {
        var wrap = new WrapPanel();
        var names = _shell.Modules.ToDictionary(m => m.Id, m => m.Module.Manifest.Name);
        foreach (var p in Profiles.All)
        {
            string list = string.Join(" · ", p.Modules.Select(id => names.TryGetValue(id, out var n) ? n : null).Where(n => n != null));
            wrap.Children.Add(Card(p.Name, p.Description, list, S.Profile == p.Id, () => _shell.ApplyProfile(p.Id)));
        }
        wrap.Children.Add(Card("직접 고를게요", "지금 켜진 모듈을 그대로 둡니다. 설정 창 [모듈]에서 하나씩 켜고 끕니다.", "", S.Profile == "", () => _shell.ApplySetting("profile", "")));
        int skipped = _shell.SkippedModules.Count;
        if (skipped == 0) return wrap;
        // 칼퇴에서 끈 모듈은 불러오지 않아 프로필에 있어도 켜지지 않는다(docs/22). 어디서 켜는지 알려 준다.
        var box = new StackPanel();
        box.Children.Add(wrap);
        box.Children.Add(Note($"칼퇴에서 끈 모듈 {skipped}개는 여기서 켜지 않아요. 쓰려면 칼퇴 [모듈 관리]에서 켠 뒤 훈수꾼을 껐다 켜세요."));
        return box;
    }

    // ---- 자리 ----
    private FrameworkElement Place()
    {
        var box = new StackPanel();
        var wrap = new WrapPanel();
        foreach (var (key, note) in new[]
        {
            ("right", "기본. 화면 오른쪽에 붙고 엑셀이 왼쪽 남은 곳을 씁니다"),
            ("left", "왼손잡이나 오른쪽에 다른 창을 둘 때"),
            ("off", "엑셀 창 가장자리에 아이콘만 살짝. 눌렀을 때만 패널이 뜹니다"),
        })
            wrap.Children.Add(Card(SetupWizard.PlaceName(key), note, "", S.Sidebar == key, () => _shell.ApplySetting("sidebar", key), PlaceSketch(key)));
        box.Children.Add(wrap);
        if (S.Sidebar == "off")
        {
            box.Children.Add(ManagerParts.Caption(_r, "도크 아이콘 배치 모양"));
            var chips = new WrapPanel();
            foreach (string shape in Shapes)
                chips.Children.Add(Spaced(ManagerParts.Chip(_r, SetupWizard.DockShapeName(shape), S.DockShape == shape, () => _shell.ApplyLook(S.Look with { Shape = shape }))));
            box.Children.Add(chips);
        }
        return box;
    }

    /// <summary>자리 카드의 작은 그림: 화면 틀 안에 엑셀(격자)과 사이드바(강조색 띠) 또는 가장자리 아이콘 점.</summary>
    private FrameworkElement PlaceSketch(string key)
    {
        const double W = 150, H = 80, Bar = 34;
        var canvas = new Canvas { Width = W, Height = H, Margin = new Thickness(0, 0, 0, 10) };
        var line = Brush("Hunsu.Line");
        var accent = Brush("Hunsu.Accent");
        canvas.Children.Add(new Rectangle { Width = W, Height = H, RadiusX = 6, RadiusY = 6, Stroke = line, StrokeThickness = 1.5, Fill = Brush("Hunsu.GlassAlt") });
        double ex = key == "left" ? Bar + 4 : 4, ew = key == "off" ? W - 8 : W - Bar - 8;
        var excel = new Rectangle { Width = ew, Height = H - 8, Stroke = line, StrokeThickness = 1, StrokeDashArray = [2, 2] };
        Canvas.SetLeft(excel, ex);
        Canvas.SetTop(excel, 4);
        canvas.Children.Add(excel);
        if (key == "off")
        {
            for (int i = 0; i < 4; i++)
            {
                var dot = new Ellipse { Width = 9, Height = 9, Fill = accent, Opacity = i == 0 ? 1 : 0.55 };
                Canvas.SetLeft(dot, W - 16 - (i % 2) * 5);
                Canvas.SetTop(dot, 18 + i * 13);
                canvas.Children.Add(dot);
            }
        }
        else
        {
            var bar = new Rectangle { Width = Bar - 2, Height = H - 8, RadiusX = 4, RadiusY = 4, Fill = accent, Opacity = 0.85 };
            Canvas.SetLeft(bar, key == "left" ? 3 : W - Bar - 1);
            Canvas.SetTop(bar, 4);
            canvas.Children.Add(bar);
        }
        return canvas;
    }

    // ---- 모양 ----
    private FrameworkElement Look()
    {
        var box = new StackPanel();
        box.Children.Add(ManagerParts.Caption(_r, "패널 룩"));
        var skins = new WrapPanel();
        foreach (var k in SkinChoice.All)
            skins.Children.Add(Card(k.Label, k.Note, "", S.Skin == k.Name, () => _shell.ApplySetting("skin", k.Name), width: 222));
        box.Children.Add(skins);

        box.Children.Add(ManagerParts.Caption(_r, "아이콘 세트"));
        var sets = new WrapPanel();
        var ink = Brush(ThemeKeys.Text);
        foreach (string key in HunsuSettings.IconProfiles)
        {
            var icons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            foreach (string icon in SampleIcons)
            {
                var el = IconFactory.Create(key, icon, 18, ink, "·");
                el.Margin = new Thickness(0, 0, 7, 0);
                icons.Children.Add(el);
            }
            string k = key;
            sets.Children.Add(Card(SetupWizard.IconSetName(key), "", "", S.IconProfile == key, () => _shell.ApplyLook(S.Look with { IconProfile = k }), icons, width: 150));
        }
        box.Children.Add(sets);

        box.Children.Add(ManagerParts.Caption(_r, "아이콘 크기"));
        var sizes = new WrapPanel();
        foreach (var (key, name) in new[] { ("s", "작게"), ("m", "보통"), ("l", "크게") })
            sizes.Children.Add(Spaced(ManagerParts.Chip(_r, name, S.DockSize == key, () => _shell.ApplyLook(S.Look with { Size = key }))));
        box.Children.Add(sizes);
        return box;
    }

    // ---- 쓰는 법 ----
    private FrameworkElement Tour()
    {
        var box = new StackPanel();
        foreach (var tip in SetupWizard.Tips)
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8), LastChildFill = true };
            if (tip.SettingKey is string key)
            {
                bool on = SetupWizard.IsOn(S, key);
                var sw = ManagerParts.Switch(_r, on, () => _shell.ApplySetting(key, !on));
                sw.Margin = new Thickness(14, 0, 0, 0);
                sw.VerticalAlignment = VerticalAlignment.Center;
                DockPanel.SetDock(sw, Dock.Right);
                row.Children.Add(sw);
            }
            var text = new StackPanel();
            text.Children.Add(new TextBlock { Text = tip.Title, FontWeight = FontWeights.SemiBold });
            text.Children.Add(Note(tip.How));
            row.Children.Add(text);
            box.Children.Add(Panel(row));
        }
        return box;
    }

    // ---- 끝 ----
    private FrameworkElement Done()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        int row = 0;
        foreach (var (label, value) in SetupWizard.Summary(S))
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = new TextBlock { Text = label, Foreground = Brush(ThemeKeys.Muted), Margin = new Thickness(0, 0, 0, 10) };
            var v = new TextBlock { Text = value, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(l, row);
            Grid.SetRow(v, row);
            Grid.SetColumn(v, 1);
            grid.Children.Add(l);
            grid.Children.Add(v);
            row++;
        }
        var box = new StackPanel();
        box.Children.Add(Panel(grid));
        box.Children.Add(Note("이 마법사는 설정 창 머리글의 [설정 마법사], 훈 원 우클릭, 알림 영역 메뉴에서 다시 열 수 있습니다."));
        return box;
    }

    // ---- 조각 ----

    /// <summary>고르는 카드: (그림) 제목, 설명, 작은 목록. 고른 것은 강조색 테두리와 ✓.</summary>
    private Border Card(string title, string note, string list, bool selected, Action act, FrameworkElement? art = null, double width = 222)
    {
        var body = new StackPanel();
        if (art != null) body.Children.Add(art);
        var head = new DockPanel();
        if (selected)
        {
            var check = new TextBlock { Text = "✓", FontWeight = FontWeights.Bold, Foreground = Brush(ThemeKeys.Accent) };
            DockPanel.SetDock(check, Dock.Right);
            head.Children.Add(check);
        }
        head.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        body.Children.Add(head);
        if (note.Length > 0) body.Children.Add(Note(note));
        if (list.Length > 0)
            body.Children.Add(new TextBlock { Text = list, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0), FontSize = (double)_r.FindResource("Hunsu.FontCaption"), Foreground = Brush(ThemeKeys.Muted), Opacity = 0.85 });
        Brush rest = Brush(selected ? "Hunsu.GlassHover" : "Hunsu.GlassAlt");
        var card = new Border
        {
            Child = body,
            Width = width,
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 10, 10),
            CornerRadius = (CornerRadius)_r.FindResource("Hunsu.PanelRadius"),
            Background = rest,
            BorderBrush = Brush(selected ? ThemeKeys.Accent : "Hunsu.Line"),
            BorderThickness = new Thickness(selected ? 2 : 1),
            Cursor = Cursors.Hand,
        };
        if (!selected)
        {
            var hover = Brush("Hunsu.GlassHover");
            card.MouseEnter += (_, _) => card.Background = hover;
            card.MouseLeave += (_, _) => card.Background = rest;
        }
        card.MouseLeftButtonUp += (_, e) => { e.Handled = true; act(); };
        return card;
    }

    private Border Panel(UIElement child) => new()
    {
        Child = child,
        Padding = new Thickness(14, 10, 14, 10),
        Margin = new Thickness(0, 0, 0, 8),
        CornerRadius = (CornerRadius)_r.FindResource("Hunsu.PanelRadius"),
        Background = Brush("Hunsu.GlassAlt"),
        BorderBrush = Brush("Hunsu.Line"),
        BorderThickness = new Thickness(1),
    };

    private TextBlock Note(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 3, 0, 0),
        FontSize = (double)_r.FindResource("Hunsu.FontSmall"),
        Foreground = Brush(ThemeKeys.Muted),
    };

    private Brush Brush(string key) => (Brush)_r.FindResource(key);

    private static Border Spaced(Border chip)
    {
        chip.Margin = new Thickness(0, 0, 6, 6);
        return chip;
    }
}
