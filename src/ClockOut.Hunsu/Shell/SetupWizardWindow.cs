// 기능: 처음 쓰는 사람용 설정 마법사 창. 위에 단계 점(시작 · 하는 일 · 자리 · 모양 · 쓰는 법 · 끝), 가운데 큰 제목·한 줄 안내·고르는 칸, 아래 [이전] [다음] 단추.
//       고르는 대로 바로 반영·저장되고(설정 창과 같은 길), 닫거나 끝내면 settings의 setupSeen을 적어 다음부터 저절로 뜨지 않는다.
//       단계를 넘길 때는 짧게 스며들 듯 바뀐다(Hunsu.Fast, 튕김 없음, docs/09 "조용한 훈수").
// 사용법: shell.OpenWizard() 가 연다(처음 실행, 설정 창 머리글 [설정 마법사], 알림 영역·훈 원 우클릭 "설정 마법사…", 사이드바 아래 ✦). new SetupWizardWindow(shell).Show()
// 의존: Core/Setup/SetupWizard(단계·요약), Shell/SetupWizardPages(쪽마다 칸), Shell/ShellController(Settings, ApplySetting, SettingsChanged, LookChanged, OpenManager), Shell/ManagerParts, Ui Theme.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ClockOut.Hunsu.Core.Setup;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Shell;

internal sealed class SetupWizardWindow : Window
{
    private readonly ShellController _shell;
    private readonly SetupWizardPages _pages;
    private int _step;
    private bool _rebuildQueued;

    public SetupWizardWindow(ShellController shell)
    {
        _shell = shell;
        _pages = new SetupWizardPages(shell, this);
        Title = "훈수꾼 · 설정 마법사";
        Width = 780;
        Height = 600;
        MinWidth = 520;
        MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetResourceReference(BackgroundProperty, "Hunsu.Surface");
        SetResourceReference(ForegroundProperty, ThemeKeys.Text);
        SetResourceReference(FontFamilyProperty, "Hunsu.Font");
        FontSize = (double)FindResource("Hunsu.FontBody");
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        KeyDown += OnKey;
        _shell.SettingsChanged += QueueRebuild;
        _shell.LookChanged += QueueRebuild;
        ThemePicker.SkinChanged += QueueRebuild;
        Closed += (_, _) =>
        {
            _shell.SettingsChanged -= QueueRebuild;
            _shell.LookChanged -= QueueRebuild;
            ThemePicker.SkinChanged -= QueueRebuild;
            MarkSeen();
        };
        Build(animate: false);
    }

    /// <summary>고른 값이 바뀌면(룩을 바꾸면 색까지) 지금 쪽을 새로 짓는다. 누른 처리 중일 수 있어 그 뒤에 한 번만.</summary>
    private void QueueRebuild()
    {
        if (_rebuildQueued) return;
        _rebuildQueued = true;
        Dispatcher.BeginInvoke(() => { _rebuildQueued = false; if (IsLoaded || IsVisible) Build(animate: false); });
    }

    private void Go(int delta)
    {
        int next = SetupWizard.Move(_step, delta);
        if (next == _step) return;
        _step = next;
        Build(animate: true, from: delta);
    }

    private void Jump(int index)
    {
        if (index == _step) return;
        int delta = index > _step ? 1 : -1;
        _step = index;
        Build(animate: true, from: delta);
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Close(); e.Handled = true; }
        else if (e.Key is Key.Right or Key.PageDown || (e.Key == Key.Enter && _step < SetupWizard.Steps.Count - 1)) { Go(1); e.Handled = true; }
        else if (e.Key is Key.Left or Key.PageUp) { Go(-1); e.Handled = true; }
    }

    private void MarkSeen()
    {
        if (_shell.Settings.SetupSeen < SetupWizard.Version) _shell.ApplySetting("setupSeen", SetupWizard.Version);
    }

    private void Build(bool animate, int from = 1)
    {
        var step = SetupWizard.Steps[_step];
        var dock = new DockPanel();

        var top = Dots();
        DockPanel.SetDock(top, Dock.Top);
        dock.Children.Add(top);

        var bottom = Footer();
        DockPanel.SetDock(bottom, Dock.Bottom);
        dock.Children.Add(bottom);

        var body = new StackPanel { Margin = new Thickness(40, 8, 40, 16) };
        body.Children.Add(new TextBlock
        {
            Text = step.Title,
            FontFamily = (FontFamily)FindResource("Hunsu.FontTitle"),
            FontSize = (double)FindResource("Hunsu.FontTitleSize") + 11,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        body.Children.Add(new TextBlock
        {
            Text = step.Lead,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 18),
            Foreground = (Brush)FindResource(ThemeKeys.Muted),
            LineHeight = FontSize * 1.5,
        });
        body.Children.Add(_pages.Build(step.Id));
        var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false };
        dock.Children.Add(scroll);
        Content = dock;
        if (animate) Enter(body, from);
    }

    /// <summary>새 쪽이 넘긴 쪽에서 8px 미끄러지며 스민다(감속, 튕김 없음).</summary>
    private void Enter(FrameworkElement body, int from)
    {
        var dur = (Duration)FindResource(ThemeKeys.Fast);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var move = new TranslateTransform(from > 0 ? 18 : -18, 0);
        body.RenderTransform = move;
        move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, dur) { EasingFunction = ease });
        body.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, dur) { EasingFunction = ease });
    }

    /// <summary>위 단계 점 줄: 지난 단계는 강조색 점, 지금 단계는 길쭉한 알약, 남은 단계는 옅은 점. 눌러서 건너뛸 수 있다.</summary>
    private FrameworkElement Dots()
    {
        var row = new DockPanel { Margin = new Thickness(40, 22, 28, 18) };
        var later = ManagerParts.Chip(this, _step == SetupWizard.Steps.Count - 1 ? "닫기" : "나중에 할게요", false, Close);
        later.ToolTip = "닫아도 설정 창 머리글의 [설정 마법사]로 언제든 다시 열 수 있어요";
        DockPanel.SetDock(later, Dock.Right);
        row.Children.Add(later);
        var dots = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var accent = (Brush)FindResource(ThemeKeys.Accent);
        var line = (Brush)FindResource("Hunsu.Line");
        for (int i = 0; i < SetupWizard.Steps.Count; i++)
        {
            bool now = i == _step;
            var pip = new Border
            {
                Width = now ? 26 : 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = i <= _step ? accent : line,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            var label = new TextBlock
            {
                Text = SetupWizard.Steps[i].Tab,
                Margin = new Thickness(0, 6, 0, 0),
                FontSize = (double)FindResource("Hunsu.FontCaption"),
                FontWeight = now ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = (Brush)FindResource(now ? ThemeKeys.Text : ThemeKeys.Muted),
            };
            var cell = new StackPanel { Margin = new Thickness(0, 0, 18, 0), Background = Brushes.Transparent, Cursor = Cursors.Hand };
            cell.Children.Add(pip);
            cell.Children.Add(label);
            int index = i;
            cell.MouseLeftButtonUp += (_, e) => { e.Handled = true; Jump(index); };
            dots.Children.Add(cell);
        }
        row.Children.Add(dots);
        return row;
    }

    /// <summary>아래 줄: [이전] (왼쪽), [다음 →] 또는 마지막 쪽의 [설정 창 열기] [시작하기] (오른쪽).</summary>
    private FrameworkElement Footer()
    {
        var bar = new DockPanel { Margin = new Thickness(40, 12, 40, 20) };
        var right = new StackPanel { Orientation = Orientation.Horizontal };
        bool last = _step == SetupWizard.Steps.Count - 1;
        if (last)
        {
            right.Children.Add(Big(ManagerParts.Chip(this, "설정 창 열기", false, () => { Close(); _shell.OpenManager("modules"); })));
            right.Children.Add(Big(ManagerParts.Chip(this, "시작하기", true, Close)));
        }
        else right.Children.Add(Big(ManagerParts.Chip(this, _step == 0 ? "맞춰 볼게요  →" : "다음  →", true, () => Go(1))));
        DockPanel.SetDock(right, Dock.Right);
        bar.Children.Add(right);
        if (_step > 0) bar.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { Big(ManagerParts.Chip(this, "←  이전", false, () => Go(-1))) } });
        else bar.Children.Add(new TextBlock
        {
            Text = "← → 키로도 넘길 수 있어요",
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = (double)FindResource("Hunsu.FontCaption"),
            Foreground = (Brush)FindResource(ThemeKeys.Muted),
        });
        return new Border { Child = bar, BorderBrush = (Brush)FindResource("Hunsu.Line"), BorderThickness = new Thickness(0, 1, 0, 0) };
    }

    /// <summary>아래 줄 단추는 조금 크게(누르기 쉽게).</summary>
    private static Border Big(Border chip)
    {
        chip.Padding = new Thickness(18, 8, 18, 8);
        chip.Margin = new Thickness(8, 0, 0, 0);
        return chip;
    }
}
