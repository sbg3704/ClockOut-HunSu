// 기능: 설정 창 [프로필] 쪽. 작업 형태별로 미리 짜 둔 프로필(정산·회계 마감, 데이터 정리, 검수·감사, 수식 따라가기 …)과 내 프로필을 카드로 보인다.
//       여러 개를 함께 고를 수 있고(2026-11-22 사용자 요청), 고른 프로필의 모듈을 합쳐(겹치면 한 번) 켜고 순서를 맞춘다. 아래에서 지금 상태를 내 프로필로 저장한다(Shell/ProfileSaveRow).
// 사용법: new ProfilesPage(shell) 를 ManagerWindow가 [프로필] 단추에서 보인다. 고르기·빼기는 shell.ToggleProfile(id), 모두 빼기는 shell.ApplyProfiles([]), 지우기는 shell.DeleteMyProfile(id).
// 의존: Shell/ShellController(Modules, Settings, ToggleProfile, ApplyProfiles, MyProfiles, DeleteMyProfile, SettingsChanged), Core/Layout/Profiles, Shell/ManagerParts, Shell/ProfileSaveRow.
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
        _root.Children.Add(ManagerParts.Caption(this, "작업 프로필 (여러 개 함께 고를 수 있어요)"));
        _root.Children.Add(Note("고르면 그 작업에 맞는 모듈만 켜고 순서를 맞춥니다. 여러 개를 고르면 모듈을 합쳐 켭니다. 나머지는 꺼질 뿐 지워지지 않고, [모듈] 쪽에서 언제든 다시 켤 수 있습니다."));
        var mine = _shell.MyProfiles;
        var chosen = Profiles.ParseIds(_shell.Settings.Profile, mine);
        var names = _shell.Modules.ToDictionary(m => m.Id, m => m.Module.Manifest.Name);
        foreach (var p in Profiles.All) _root.Children.Add(Card(p, chosen.Contains(p.Id), names, deletable: false));
        if (chosen.Count > 0)
        {
            var clear = ManagerParts.Chip(this, "모두 빼기 (모듈은 그대로)", false, () => _shell.ApplyProfiles([]));
            clear.Margin = new Thickness(0, 8, 0, 0);
            clear.HorizontalAlignment = HorizontalAlignment.Left;
            _root.Children.Add(clear);
        }

        _root.Children.Add(ManagerParts.Caption(this, "내 프로필"));
        if (mine.Count == 0) _root.Children.Add(Note("아직 없어요. 모듈과 모양을 맞춘 뒤 아래에서 이름을 붙여 저장하세요."));
        foreach (var p in mine) _root.Children.Add(Card(p, chosen.Contains(p.Id), names, deletable: true));
        _root.Children.Add(ProfileSaveRow.Build(this, _shell));
    }

    private FrameworkElement Card(Profile p, bool selected, Dictionary<string, string> names, bool deletable)
    {
        var body = new StackPanel();
        var head = new DockPanel();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        if (deletable) buttons.Children.Add(ManagerParts.Chip(this, "지우기", false, () => _shell.DeleteMyProfile(p.Id)));
        buttons.Children.Add(ManagerParts.Chip(this, selected ? "✓ 고름" : "고르기", selected, () => _shell.ToggleProfile(p.Id)));
        DockPanel.SetDock(buttons, Dock.Right);
        head.Children.Add(buttons);
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
