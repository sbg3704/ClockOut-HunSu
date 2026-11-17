// 기능: 설정 창 [옵션] 쪽. 모든 모듈이 manifest의 quick 선언표에 적은 옵션을 모듈별로 모아 보이고(순서·묶음·하위 메뉴 그대로), 우클릭 빠른 메뉴와 같은 값을 바꾼다. 모듈 코드는 없다.
// 사용법: new OptionsPage(shell)을 ManagerWindow가 [옵션] 단추에서 보인다. 값 바꾸기는 shell.ApplySetting(key, 값).
// 의존: Core/Manifest/QuickSetting(배치·값), Shell/ManagerParts(스위치·단추), ShellController(Modules, Settings, SettingsChanged).
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClockOut.Hunsu.Core.Manifest;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Shell;

internal sealed class OptionsPage : ScrollViewer
{
    private readonly ShellController _shell;
    private readonly StackPanel _root = new() { Margin = new Thickness(22, 4, 22, 12) };

    public OptionsPage(ShellController shell)
    {
        _shell = shell;
        Content = _root;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        Loaded += (_, _) => _shell.SettingsChanged += Rebuild;
        Unloaded += (_, _) => _shell.SettingsChanged -= Rebuild;
    }

    internal void Rebuild()
    {
        _root.Children.Clear();
        _root.Children.Add(ManagerParts.Caption(this, "모듈 옵션"));
        _root.Children.Add(Note("모듈 아이콘이나 패널을 우클릭해도 같은 옵션이 나옵니다. 여기서는 모듈별로 한 번에 봅니다. 바로 반영됩니다."));
        string json = _shell.Settings.ToJson();
        bool any = false;
        foreach (var mv in _shell.Modules.Where(m => m.Module.Manifest.Quick.Count > 0).OrderBy(m => m.Placement.Order))
        {
            any = true;
            var head = ManagerParts.Caption(this, mv.Module.Manifest.Name);
            head.Margin = new Thickness(0, 14, 0, 2);
            _root.Children.Add(head);
            AddItems(mv.Module.Manifest.Quick, json, 0);
        }
        if (!any) _root.Children.Add(Note("옵션을 가진 모듈이 아직 없습니다."));
    }

    private void AddItems(IReadOnlyList<QuickSetting> items, string json, int depth)
    {
        foreach (var g in QuickSchema.Arrange(items))
        {
            if (g.Name.Length > 0) _root.Children.Add(Indent(Note("▸ " + g.Name), depth));
            foreach (var q in g.Items)
            {
                if (q.Kind == QuickSetting.Menu)
                {
                    _root.Children.Add(Indent(Note(q.Label), depth));
                    AddItems(q.Children, json, depth + 1);
                }
                else if (q.Kind == QuickSetting.Toggle)
                {
                    bool on = QuickSettings.IsOn(QuickSettings.Current(json, q.Key));
                    _root.Children.Add(Indent(Row(ManagerParts.Switch(this, on, () => _shell.ApplySetting(q.Key, !on)), q.Label), depth));
                }
                else
                {
                    string cur = QuickSettings.Current(json, q.Key);
                    var wrap = new WrapPanel();
                    foreach (var o in q.Options)
                    {
                        var val = QuickSettings.Typed(o.Value);
                        wrap.Children.Add(ManagerParts.Chip(this, o.Label, QuickSettings.IsCurrent(cur, o), () => _shell.ApplySetting(q.Key, val)));
                    }
                    _root.Children.Add(Indent(Note(q.Label), depth));
                    _root.Children.Add(Indent(wrap, depth));
                }
            }
        }
    }

    private static FrameworkElement Indent(FrameworkElement e, int depth) { e.Margin = new Thickness(depth * 18 + e.Margin.Left, e.Margin.Top, 0, e.Margin.Bottom); return e; }

    private FrameworkElement Row(FrameworkElement control, string text)
    {
        // 설명 글은 남은 폭에서 줄바꿈한다(좁은 창에서 오른쪽 끝이 잘리지 않게).
        var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2), LastChildFill = true };
        control.Margin = new Thickness(0, 0, 12, 0);
        control.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(control, Dock.Left);
        row.Children.Add(control);
        row.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource(ThemeKeys.Muted), FontSize = (double)FindResource("Hunsu.FontSmall") });
        return row;
    }

    private TextBlock Note(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 6, 0, 2),
        FontSize = (double)FindResource("Hunsu.FontCaption"),
        Foreground = (Brush)FindResource(ThemeKeys.Muted),
    };
}
