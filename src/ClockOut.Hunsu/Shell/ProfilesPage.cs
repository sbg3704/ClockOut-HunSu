// 기능: 설정 창 [프로필] 쪽. 작업 형태별로 미리 짜 둔 프로필(정산·회계 마감, 데이터 정리, 검수·감사, 수식 따라가기 …)을 카드로 보이고, 누르면 그 모듈들만 켜고 순서를 맞춘다.
// 사용법: new ProfilesPage(shell) 를 ManagerWindow가 [프로필] 단추에서 보인다. 적용은 shell.ApplyProfile(id).
// 의존: Shell/ShellController(Modules, Settings, ApplyProfile, SettingsChanged, ModulesChanged), Core/Layout/Profiles, Shell/ManagerParts.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Shell;

internal sealed class ProfilesPage : ScrollViewer
{
    private readonly ShellController _shell;
    private readonly StackPanel _root = new() { Margin = new Thickness(22, 4, 22, 12) };

    public ProfilesPage(ShellController shell)
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
        _root.Children.Add(ManagerParts.Caption(this, "작업 프로필"));
        _root.Children.Add(Note("고르면 그 작업에 맞는 모듈만 켜고 순서를 맞춥니다. 나머지는 꺼질 뿐 지워지지 않고, [모듈] 쪽에서 언제든 다시 켤 수 있습니다."));
        string current = _shell.Settings.Profile;
        var names = _shell.Modules.ToDictionary(m => m.Id, m => m.Module.Manifest.Name);
        foreach (var p in Profiles.All) _root.Children.Add(Card(p, p.Id == current, names));
    }

    private FrameworkElement Card(Profile p, bool selected, Dictionary<string, string> names)
    {
        var body = new StackPanel();
        var head = new DockPanel();
        var apply = ManagerParts.Chip(this, selected ? "적용됨" : "적용", selected, () => _shell.ApplyProfile(p.Id));
        DockPanel.SetDock(apply, Dock.Right);
        head.Children.Add(apply);
        head.Children.Add(new TextBlock
        {
            Text = p.Name,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)FindResource(ThemeKeys.Text),
        });
        body.Children.Add(head);
        body.Children.Add(Note(p.Description));
        string list = string.Join(" · ", p.Modules.Select(id => names.TryGetValue(id, out var n) ? n : null).Where(n => n != null));
        body.Children.Add(new TextBlock
        {
            Text = list,
            TextWrapping = TextWrapping.Wrap,
            FontSize = (double)FindResource("Hunsu.FontCaption"),
            Foreground = (Brush)FindResource(ThemeKeys.Muted),
            Margin = new Thickness(0, 2, 0, 0),
        });
        return new Border
        {
            Child = body,
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 6, 0, 0),
            CornerRadius = (CornerRadius)FindResource("Hunsu.RowRadius"),
            Background = (Brush)FindResource(selected ? "Hunsu.GlassHover" : "Hunsu.Glass"),
            BorderBrush = (Brush)FindResource(selected ? ThemeKeys.Accent : "Hunsu.Line"),
            BorderThickness = new Thickness(1),
        };
    }

    private TextBlock Note(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        FontSize = (double)FindResource("Hunsu.FontSmall"),
        Foreground = (Brush)FindResource(ThemeKeys.Muted),
        Margin = new Thickness(0, 2, 0, 0),
    };
}
