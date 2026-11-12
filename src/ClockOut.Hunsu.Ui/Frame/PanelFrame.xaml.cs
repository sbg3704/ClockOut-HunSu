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

    public PanelFrame()
    {
        InitializeComponent();
        MouseEnter += (_, _) => FadeTo(1.0);
        MouseLeave += (_, _) => FadeTo(OpacityLevels.RestOpacity(_opacityLevel));
    }

    public event Action? PinClicked;
    public event Action? CollapseClicked;
    public event Action? OpacityClicked;
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
    public void SetSummoned(bool on) => Card.BorderBrush = (System.Windows.Media.Brush)FindResource(on ? ThemeKeys.Accent : "Hunsu.Line");

    private void FadeTo(double target)
    {
        var dur = ThemeKeys.Get<Duration>(this, ThemeKeys.Fast);
        BeginAnimation(OpacityProperty, new DoubleAnimation(target, dur) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    private void PinButton_Click(object sender, RoutedEventArgs e) => PinClicked?.Invoke();

    private void CollapseButton_Click(object sender, RoutedEventArgs e) => CollapseClicked?.Invoke();

    private void OpacityButton_Click(object sender, RoutedEventArgs e) => OpacityClicked?.Invoke();

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => TitleDragStarted?.Invoke(e);
}
