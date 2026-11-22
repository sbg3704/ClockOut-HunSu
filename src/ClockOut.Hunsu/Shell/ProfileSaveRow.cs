// 기능: "지금 켜진 모듈로 내 프로필 만들기" 한 줄: 이름 칸, [모양도 함께] 체크 상자, [저장] 단추. 설정 창 [프로필]과 설정 마법사 [하는 일]이 같이 쓴다.
//       저장하면 지금 켜진 모듈(순서대로)과, 스위치가 켜져 있으면 지금 모양(룩·자리·도크·아이콘·수식 흐름 그림)을 이름 붙여 settings의 customProfiles에 둔다. 같은 이름은 덮어쓴다.
// 사용법: panel.Children.Add(ProfileSaveRow.Build(this, shell));
// 의존: Shell/ShellController(SaveMyProfile, Settings), Shell/ManagerParts, Core/Settings(HunsuSettings.MaxProfileName·MaxCustomProfiles), Ui Theme.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClockOut.Hunsu.Core.Settings;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Shell;

internal static class ProfileSaveRow
{
    private static bool _withLook = true; // 창을 다시 지어도 고른 값을 기억한다

    public static FrameworkElement Build(FrameworkElement r, ShellController shell)
    {
        var box = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        int count = shell.Settings.CustomProfiles.Count;
        var name = new TextBox
        {
            Width = 200,
            MaxLength = HunsuSettings.MaxProfileName,
            Padding = new Thickness(8, 4, 8, 4),
            VerticalContentAlignment = VerticalAlignment.Center,
            Text = $"내 프로필 {count + 1}",
            Background = (Brush)r.FindResource("Hunsu.GlassAlt"),
            Foreground = (Brush)r.FindResource(ThemeKeys.Text),
            CaretBrush = (Brush)r.FindResource(ThemeKeys.Text),
            BorderBrush = (Brush)r.FindResource("Hunsu.Line"),
            ToolTip = "프로필 이름 (같은 이름이면 덮어씁니다)",
        };
        name.GotKeyboardFocus += (_, _) => name.SelectAll();
        var row = new WrapPanel();
        row.Children.Add(name);
        // 체크 상자는 그 자리에서만 바뀐다(창을 다시 지으면 적던 이름이 지워지므로).
        var look = new CheckBox
        {
            Content = "모양도 함께",
            IsChecked = _withLook,
            Margin = new Thickness(12, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = (double)r.FindResource("Hunsu.FontSmall"),
            Foreground = (Brush)r.FindResource(ThemeKeys.Text),
        };
        look.Click += (_, _) => _withLook = look.IsChecked == true;
        row.Children.Add(look);
        bool full = count >= HunsuSettings.MaxCustomProfiles;
        void Save() { if (!full) shell.SaveMyProfile(name.Text, _withLook); }
        name.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; Save(); } };
        var save = ManagerParts.Chip(r, full ? $"최대 {HunsuSettings.MaxCustomProfiles}개" : "지금 상태로 저장", !full, Save);
        save.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(save);
        box.Children.Add(row);
        box.Children.Add(new TextBlock
        {
            Text = "지금 켜진 모듈을 순서대로 담습니다. '모양도 함께'를 켜면 패널 룩, 자리, 도크 모양, 아이콘, 수식 흐름 그림도 같이 담아 고를 때 함께 바뀝니다.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
            FontSize = (double)r.FindResource("Hunsu.FontCaption"),
            Foreground = (Brush)r.FindResource(ThemeKeys.Muted),
        });
        return box;
    }
}
