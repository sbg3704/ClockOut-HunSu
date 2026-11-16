// 기능: 패널 틀의 동작. 제목, 내용, 핀 상태(도크/핀 창), 투명도 단계(평소 불투명도, 마우스를 올리면 선명), 강조 테두리(부르기 중).
// 사용법: var f = new PanelFrame { Title = "선택 요약", Body = renderer.Root }; f.PinClicked += ...; f.SetOpacityLevel(1);
//   이벤트: PinClicked, CollapseClicked, OpacityClicked, TitleDragStarted(핀 창에서 끌어 옮기기).
// 의존: Theme(Hunsu.Fast 등), Core/Layout/OpacityLevels. 데이터나 엑셀을 모른다.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Ui.Frame;

public partial class PanelFrame : UserControl
{
    private int _opacityLevel;
    private bool _summoned;
    private bool _locked;

    public PanelFrame()
    {
        InitializeComponent();
        MouseEnter += (_, _) => FadeTo(1.0);
        MouseLeave += (_, _) => FadeTo(OpacityLevels.RestOpacity(_opacityLevel));
    }

    public event Action? PinClicked;
    public event Action? CollapseClicked;
    public event Action? OpacityClicked;
    public event Action? LockClicked;
    public event Action<MouseButtonEventArgs>? TitleDragStarted;

    public string Title
    {
        get => TitleText.Text;
        set => TitleText.Text = value;
    }

    public object? Body
    {
        get => BodyHost.Content;
        set => BodyHost.Content = value;
    }

    /// <summary>핀 창에 있을 때 true: 단추 모양이 "도크로 되돌리기"로 바뀌고 접기 단추를 숨긴다(창은 접지 않음).</summary>
    public bool IsPinned
    {
        get => PinButton.Content as string == "";
        set
        {
            PinButton.Content = value ? "" : "";
            PinButton.ToolTip = value ? "도크로 되돌리기" : "떼어 내기(핀 창)";
            CollapseButton.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    public int OpacityLevel => _opacityLevel;

    public void SetOpacityLevel(int level)
    {
        _opacityLevel = Math.Clamp(level, 0, OpacityLevels.Rest.Length - 1);
        OpacityButton.ToolTip = "투명도: " + OpacityLevels.Names[_opacityLevel];
        if (!IsMouseOver) FadeTo(OpacityLevels.RestOpacity(_opacityLevel));
    }

    /// <summary>부르기(Ctrl+Alt+H) 중: 강조색 테두리로 "지금 누를 수 있음"을 알린다.</summary>
    public void SetSummoned(bool on)
    {
        _summoned = on;
        Card.BorderBrush = (System.Windows.Media.Brush)FindResource(on ? ThemeKeys.Accent : "Hunsu.Line");
        UpdateLockButton();
    }

    /// <summary>자리 잠금 표시. 편집 모드(부르기)에서는 늘 보이고, 잠겨 있으면 평소에도 자물쇠가 보인다.</summary>
    public void SetLocked(bool locked)
    {
        _locked = locked;
        UpdateLockButton();
    }

    private void UpdateLockButton()
    {
        LockButton.Content = _locked ? "\uE72E" : "\uE785"; // 잠김 / 풀림 (Segoe 아이콘)
        LockButton.ToolTip = _locked ? "잠김: 누르면 풀기" : "이 패널 자리 잠그기";
        LockButton.Visibility = _summoned || _locked ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FadeTo(double target)
    {
        var dur = ThemeKeys.Get<Duration>(this, ThemeKeys.Fast);
        BeginAnimation(OpacityProperty, new DoubleAnimation(target, dur) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    private void PinButton_Click(object sender, RoutedEventArgs e) => PinClicked?.Invoke();

    private void CollapseButton_Click(object sender, RoutedEventArgs e) => CollapseClicked?.Invoke();

    private void LockButton_Click(object sender, RoutedEventArgs e) => LockClicked?.Invoke();

    private void OpacityButton_Click(object sender, RoutedEventArgs e) => OpacityClicked?.Invoke();

    /// <summary>제목줄에 설명(툴팁)을 붙인다(null이면 없앰). 핀 창에서 "Shift를 누른 채 끌어 옮기기" 안내에 쓴다.</summary>
    public void SetTitleTip(string? tip) => TitleBar.ToolTip = tip;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => TitleDragStarted?.Invoke(e);
}
