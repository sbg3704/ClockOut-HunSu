// 기능: 패널 틀의 동작. 제목, 내용, 핀 상태(도크/핀 창), 투명도(◐ 단추를 누르면 막대가 나와 아날로그로 조절, 마우스를 올리면 선명), 강조 테두리(부르기 중).
// 사용법: var f = new PanelFrame { Title = "선택 요약", Body = renderer.Root }; f.PinClicked += ...; f.SetOpacityLevel(1);
//   이벤트: PinClicked, CollapseClicked, AlphaCommitted(◐ 막대로 정한 투명도), TitleDragStarted(핀 창에서 끌어 옮기기).
// 의존: Theme(Hunsu.Fast 등), Core/Layout/OpacityLevels. 데이터나 엑셀을 모른다.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Ui.Frame;

/// <summary>합쳐 보기(setting panelJoin)에서 패널이 묶음의 어디인지.</summary>
public enum JoinPos { None, First, Middle, Last, Only }

public partial class PanelFrame : UserControl
{
    /// <summary>마우스를 올리면 선명해지는지(설정 hoverSharpen). 모든 패널에 같이 적용된다.</summary>
    public static bool SharpenOnHover { get; set; } = true;

    /// <summary>도크에 있을 때 패널 내용의 최대 높이(DIP, 설정 panelMaxHeight). 넘치면 휠로 내린다. 핀 창은 창 크기를 따른다.</summary>
    public static double MaxBodyHeight { get; set; } = 120;

    private int _opacityLevel;
    private double _alpha; // 슬라이더 값(0이면 단계 값)
    private bool _sliding;
    private bool _summoned;
    private bool _locked;

    public PanelFrame()
    {
        InitializeComponent();
        ApplyBodyLimit(false);
        // 룩을 바꾸면 제목 글꼴·크기가 바뀌므로 형광펜 칠 길이를 다시 잰다(자원 갱신이 끝난 뒤).
        Loaded += (_, _) => ThemePicker.SkinChanged += OnSkinChanged;
        Unloaded += (_, _) => ThemePicker.SkinChanged -= OnSkinChanged;
        MouseEnter += (_, _) => FadeTo(SharpenOnHover && !_sliding ? 1.0 : RestAlpha);
        MouseLeave += (_, _) => { CloseAlphaBar(); FadeTo(RestAlpha); };
    }

    public event Action? PinClicked;
    public event Action? CollapseClicked;
    public event Action? LockClicked;
    /// <summary>제목줄의 켜고 끄기 단추(모듈의 대표 켜기 설정, 예: 그리기)를 눌렀다.</summary>
    public event Action? PowerClicked;
    /// <summary>제목줄 ▾(본문 접기·펴기)를 눌렀다.</summary>
    public event Action? FoldClicked;

    private bool _folded;
    public bool Folded => _folded;

    /// <summary>본문을 접으면 제목줄만 남는다(사이드 패널의 섹션 머리글처럼).</summary>
    public void SetFolded(bool folded)
    {
        _folded = folded;
        BodyScroll.Visibility = folded ? Visibility.Collapsed : Visibility.Visible;
        FoldButton.Content = folded ? "\uE70E" : "\uE70D"; // ▴ / ▾
        FoldButton.ToolTip = folded ? "본문 펴기" : "본문 접기";
    }

    private void FoldButton_Click(object sender, RoutedEventArgs e) => FoldClicked?.Invoke();

    /// <summary>켜고 끄기 단추를 보이거나 감춘다. on이면 강조색, 꺼지면 흐린 색. 모듈이 대표 켜기 설정(quick toggle)을 선언했을 때만 보인다.</summary>
    public void SetPower(bool available, bool on, string label)
    {
        PowerButton.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
        PowerButton.SetResourceReference(Control.ForegroundProperty, on ? ThemeKeys.Accent : ThemeKeys.Muted);
        PowerButton.ToolTip = (on ? "끄기: " : "켜기: ") + label;
    }

    private void PowerButton_Click(object sender, RoutedEventArgs e) => PowerClicked?.Invoke();
    public event Action<MouseButtonEventArgs>? TitleDragStarted;

    public string Title
    {
        get => TitleText.Text;
        set
        {
            TitleText.Text = value;
            Hand.Seed = value;
            Hand.TitleTextWidth = MeasureTitle(value);
        }
    }

    private void OnSkinChanged()
    {
        ApplyJoin(); // 룩이 모서리를 바꾸면 합친 모양도 다시 맞춘다
        Dispatcher.BeginInvoke(() => Hand.TitleTextWidth = MeasureTitle(TitleText.Text), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private JoinPos _join = JoinPos.None;

    /// <summary>도크 합쳐 보기에서 이 패널의 자리: 맨 위/가운데/맨 아래만 모서리를 둥글게(이웃과 닿는 쪽은 각지게). None이면 따로 뜬 카드.</summary>
    public void SetJoin(JoinPos pos)
    {
        if (_join == pos) return;
        _join = pos;
        ApplyJoin();
    }

    private void ApplyJoin()
    {
        if (_join == JoinPos.None) { Card.SetResourceReference(System.Windows.Controls.Border.CornerRadiusProperty, "Hunsu.PanelRadius"); return; }
        var r = FindResource("Hunsu.PanelRadius") is CornerRadius c ? c : new CornerRadius(8);
        bool top = _join is JoinPos.First or JoinPos.Only, bottom = _join is JoinPos.Last or JoinPos.Only;
        Card.CornerRadius = new CornerRadius(top ? r.TopLeft : 0, top ? r.TopRight : 0, bottom ? r.BottomRight : 0, bottom ? r.BottomLeft : 0);
    }

    /// <summary>제목 글자 폭(손그림 형광펜 칠이 글자 밑까지만 가도록). 빈 글이면 0.</summary>
    private double MeasureTitle(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var tf = new System.Windows.Media.Typeface(TitleText.FontFamily, TitleText.FontStyle, TitleText.FontWeight, TitleText.FontStretch);
        var ft = new System.Windows.Media.FormattedText(text, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, tf,
            TitleText.FontSize, System.Windows.Media.Brushes.Black, System.Windows.Media.VisualTreeHelper.GetDpi(this).PixelsPerDip);
        return ft.Width;
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
            CollapseButton.ToolTip = "작게(아이콘으로)"; // 떼어 낸 창도 아이콘으로 줄일 수 있다
            ApplyBodyLimit(value);
        }
    }

    /// <summary>도크의 내용 높이 제한을 다시 건다(설정이 바뀌었을 때). 핀 창이면 제한을 두지 않는다.</summary>
    public void ApplyBodyLimit(bool pinned)
    {
        BodyScroll.ClearValue(HeightProperty); // 사이드바에서 정한 높이는 사이드바에 있을 때만
        BodyScroll.MaxHeight = pinned ? double.PositiveInfinity : Math.Max(40, MaxBodyHeight);
    }

    /// <summary>사이드바 스플리터가 정한 본문 높이(DIP). 0 이하면 내용만큼(자동). 넘치면 본문 안에서 스크롤.</summary>
    public void SetBodyHeight(double height)
    {
        BodyScroll.MaxHeight = double.PositiveInfinity;
        if (height > 0) BodyScroll.Height = height; else BodyScroll.ClearValue(HeightProperty);
    }

    /// <summary>지금 그려진 본문 높이(스플리터를 끌기 시작할 때의 값).</summary>
    public double BodyActualHeight => BodyScroll.ActualHeight;

    /// <summary>눌린 곳이 제목줄의 빈 자리(단추·막대가 아닌 곳)인가. 사이드바가 끌어 옮기기를 시작할 때 쓴다.</summary>
    public bool IsTitleGrab(DependencyObject? source)
    {
        if (source == null || _sliding || !IsInside(source, TitleBar)) return false;
        for (var n = source; n != null && !ReferenceEquals(n, TitleBar); n = ParentOf(n))
            if (n is System.Windows.Controls.Primitives.ButtonBase) return false;
        return true;
    }

    public int OpacityLevel => _opacityLevel;

    /// <summary>평소(마우스가 없을 때) 불투명도: 슬라이더 값이 있으면 그것, 없으면 단계 값.</summary>
    public double RestAlpha => OpacityLevels.Resolve(_opacityLevel, _alpha);

    /// <summary>슬라이더를 놓았다(막대를 닫을 때): 저장할 값. 앱이 배치에 저장한다.</summary>
    public event Action<double>? AlphaCommitted;

    /// <summary>저장된 슬라이더 값을 넣는다(0이면 단계 값을 씀).</summary>
    public void SetAlpha(double alpha)
    {
        _alpha = OpacityLevels.NormalizeAlpha(alpha);
        if (!IsMouseOver) FadeTo(RestAlpha);
    }

    private void AlphaSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (AlphaSlider.Visibility != Visibility.Visible) return;
        _alpha = OpacityLevels.NormalizeAlpha(e.NewValue);
        BeginAnimation(OpacityProperty, null); // 끄는 동안 바로 따라오게(애니메이션 없이)
        Opacity = RestAlpha;
        OpacityButton.ToolTip = $"투명도: {Math.Round(RestAlpha * 100)}%";
    }

    /// <summary>◐ 단추: 막대를 보이거나 감춘다(감출 때 값을 저장).</summary>
    private void ToggleAlphaBar()
    {
        if (_sliding) { CloseAlphaBar(); return; }
        _sliding = true;
        AlphaSlider.Value = Math.Clamp(RestAlpha, OpacityLevels.MinAlpha, OpacityLevels.MaxAlpha);
        AlphaSlider.Visibility = Visibility.Visible;
        TitleRow.Visibility = Visibility.Hidden;
        BeginAnimation(OpacityProperty, null);
        Opacity = RestAlpha;
    }

    private void CloseAlphaBar()
    {
        if (!_sliding) return;
        _sliding = false;
        AlphaSlider.Visibility = Visibility.Collapsed;
        TitleRow.Visibility = Visibility.Visible;
        AlphaCommitted?.Invoke(_alpha);
    }

    public void SetOpacityLevel(int level)
    {
        _opacityLevel = Math.Clamp(level, 0, OpacityLevels.Rest.Length - 1);
        OpacityButton.ToolTip = "투명도: " + OpacityLevels.Names[_opacityLevel];
        if (!IsMouseOver) FadeTo(RestAlpha);
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

    private void OpacityButton_Click(object sender, RoutedEventArgs e) => ToggleAlphaBar();

    /// <summary>제목줄에 설명(툴팁)을 붙인다(null이면 없앰). 핀 창에서 "Shift를 누른 채 끌어 옮기기" 안내에 쓴다.</summary>
    public void SetTitleTip(string? tip) => TitleBar.ToolTip = tip;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_sliding && e.OriginalSource is DependencyObject src && IsInside(src, AlphaSlider)) return; // 막대를 밀 때는 창을 끌지 않는다
        TitleDragStarted?.Invoke(e);
    }

    /// <summary>화면 나무의 부모(글 조각 같은 ContentElement는 논리 나무로).</summary>
    private static DependencyObject? ParentOf(DependencyObject n) =>
        (n is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D ? System.Windows.Media.VisualTreeHelper.GetParent(n) : null) ?? LogicalTreeHelper.GetParent(n);

    private static bool IsInside(DependencyObject node, DependencyObject ancestor)
    {
        for (var n = node; n != null; n = ParentOf(n))
            if (ReferenceEquals(n, ancestor)) return true;
        return false;
    }
}
