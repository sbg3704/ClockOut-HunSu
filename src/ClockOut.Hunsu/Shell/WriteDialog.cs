// 기능: 쓰기 명령(실험 기능)의 공지·확인·결과 창. 같은 모양의 작은 모달 창 하나로 제목, 글, 단추(확인·취소·되돌리기)를 보인다.
// 사용법: var c = WriteDialog.Ask("실험 기능입니다", 글, "이해했습니다", "취소");  c == WriteDialog.Choice.Ok  /  결과 창: WriteDialog.Ask(제목, 글, "닫기", null, "되돌리기")
// 의존: Shell/ManagerParts(Chip), Ui Theme 이름(Hunsu.Surface 등). 값은 Theme 이름으로만 쓴다. 화면 스레드에서 부른다. 정책 글은 Core/Write/WritePolicy.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Shell;

internal sealed class WriteDialog : Window
{
    internal enum Choice { Cancel, Ok, Undo }

    private Choice _choice = Choice.Cancel;

    private WriteDialog(string title, string body, string okText, string? cancelText, string? undoText, string tone)
    {
        Title = "훈수꾼";
        Width = 420;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        ShowInTaskbar = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = (Brush)FindResource("Hunsu.Surface");
        Foreground = (Brush)FindResource(ThemeKeys.Text);
        FontFamily = (FontFamily)FindResource("Hunsu.Font");
        FontSize = (double)FindResource("Hunsu.FontBody");
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        var root = new StackPanel { Margin = new Thickness(22, 20, 22, 18) };
        root.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = (double)FindResource("Hunsu.FontTitleSize"),
            FontWeight = FontWeights.SemiBold,
            Foreground = ThemeKeys.Tone(this, tone),
            Margin = new Thickness(0, 0, 0, 10),
        });
        root.Children.Add(new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap, LineHeight = 20, Margin = new Thickness(0, 0, 0, 16) });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        if (undoText != null) buttons.Children.Add(ManagerParts.Chip(this, undoText, false, () => Finish(Choice.Undo)));
        if (cancelText != null) buttons.Children.Add(ManagerParts.Chip(this, cancelText, false, () => Finish(Choice.Cancel)));
        buttons.Children.Add(ManagerParts.Chip(this, okText, true, () => Finish(Choice.Ok)));
        root.Children.Add(buttons);
        Content = root;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Finish(Choice.Cancel); };
    }

    private void Finish(Choice c)
    {
        _choice = c;
        Close();
    }

    /// <summary>창을 모달로 띄우고 누른 단추를 돌려준다(닫기·Esc는 Cancel). tone은 제목 색(warn, info, bad).</summary>
    internal static Choice Ask(string title, string body, string okText, string? cancelText = null, string? undoText = null, string tone = "warn")
    {
        var w = new WriteDialog(title, body, okText, cancelText, undoText, tone);
        w.ShowDialog();
        return w._choice;
    }

    /// <summary>입력 칸 여러 개를 묻는 창. 확인하면 키→글 사전, 취소하면 null.</summary>
    internal static Dictionary<string, string>? AskInputs(string title, IReadOnlyList<ClockOut.Hunsu.Core.Write.WriteNeed> needs)
    {
        var win = new Window
        {
            Title = "훈수꾼", Width = 420, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            Topmost = true, ShowInTaskbar = true, WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };
        win.Background = (Brush)win.FindResource("Hunsu.Surface");
        win.Foreground = (Brush)win.FindResource(ThemeKeys.Text);
        win.FontFamily = (FontFamily)win.FindResource("Hunsu.Font");
        win.FontSize = (double)win.FindResource("Hunsu.FontBody");
        var root = new StackPanel { Margin = new Thickness(22, 20, 22, 18) };
        root.Children.Add(new TextBlock { Text = title, FontSize = (double)win.FindResource("Hunsu.FontTitleSize"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) });
        var boxes = new List<(string Key, TextBox Box)>();
        foreach (var n in needs)
        {
            root.Children.Add(new TextBlock { Text = n.Label, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 3) });
            var box = new TextBox { Text = n.Default, Padding = new Thickness(6, 4, 6, 4) };
            boxes.Add((n.Key, box));
            root.Children.Add(box);
        }
        bool ok = false;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        buttons.Children.Add(ManagerParts.Chip(win, "취소", false, () => win.Close()));
        buttons.Children.Add(ManagerParts.Chip(win, "다음", true, () => { ok = true; win.Close(); }));
        root.Children.Add(buttons);
        win.Content = root;
        win.KeyDown += (_, e) => { if (e.Key == Key.Escape) win.Close(); };
        win.Loaded += (_, _) => { if (boxes.Count > 0) boxes[0].Box.Focus(); };
        win.ShowDialog();
        return ok ? boxes.ToDictionary(b => b.Key, b => b.Box.Text) : null;
    }
}
