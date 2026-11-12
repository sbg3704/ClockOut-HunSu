// 기능: 오버레이 창의 동작. 선택 요약 넣기, 바쁨 표시(흐리게), 패널 열고 닫기(base 200ms), 다음 프레임 시각 알리기, PNG 저장.
// 사용법: var w = new OverlayWindow(); w.Show(); w.ShowSummary(s); w.OpenPanel(t => ...); w.NextFrame(t => ...)
// 의존: Theme/Theme.xaml, Core/SelectionSummary, Native/User32. 창은 활성화되지 않는다(WS_EX_NOACTIVATE) — 엑셀의 포커스를 뺏지 않는다.
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Native;

namespace ClockOut.Hunsu.Overlay;

public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            Handle = new WindowInteropHelper(this).Handle;
            long ex = User32.GetWindowLongPtr(Handle, User32.GWL_EXSTYLE).ToInt64();
            User32.SetWindowLongPtr(Handle, User32.GWL_EXSTYLE, new IntPtr(ex | User32.WS_EX_NOACTIVATE | User32.WS_EX_TOOLWINDOW));
        };
    }

    public IntPtr Handle { get; private set; }

    public bool PanelOpen { get; private set; }

    /// <summary>사용자가 알약을 눌렀을 때(측정 도구는 이 대신 등록 메시지로 연다).</summary>
    public event Action? PillClicked;

    public void ShowSummary(SelectionSummary s, string status)
    {
        SummaryText.Opacity = 1;
        SummaryText.Text = s.ShortText();
        PanelAddr.Text = s.Addr + (s.Truncated ? "  (일부만 셈)" : "");
        PanelDetail.Text = $"칸 {s.Count:#,##0} · 숫자 {s.Numbers:#,##0} · 글자 {s.Texts:#,##0} · 수식 {s.Formulas:#,##0} · 오류 {s.Errors:#,##0}";
        PanelStatus.Text = status;
    }

    /// <summary>엑셀이 바쁘거나 조회가 실패하면 마지막 내용을 흐리게 두고 상태만 바꾼다(docs/04 6장).</summary>
    public void ShowStale(string status)
    {
        SummaryText.Opacity = 0.5;
        PanelStatus.Text = status;
    }

    public void OpenPanel(Action<long>? done = null)
    {
        PanelOpen = true;
        Panel.Visibility = Visibility.Visible;
        var dur = (Duration)FindResource("Hunsu.Base");
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var fade = new DoubleAnimation(1, dur) { EasingFunction = ease };
        fade.Completed += (_, _) => NextFrame(t => done?.Invoke(t));
        Panel.BeginAnimation(OpacityProperty, fade);
        PanelShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, dur) { EasingFunction = ease });
    }

    public void ClosePanel()
    {
        PanelOpen = false;
        Panel.BeginAnimation(OpacityProperty, null);
        PanelShift.BeginAnimation(TranslateTransform.YProperty, null);
        Panel.Opacity = 0;
        PanelShift.Y = 6;
        Panel.Visibility = Visibility.Collapsed;
    }

    /// <summary>다음 프레임을 그리기 직전(CompositionTarget.Rendering) 시각을 한 번 알린다.</summary>
    public void NextFrame(Action<long> callback)
    {
        void Handler(object? sender, EventArgs e)
        {
            CompositionTarget.Rendering -= Handler;
            callback(Stopwatch.GetTimestamp());
        }
        CompositionTarget.Rendering += Handler;
    }

    /// <summary>클릭을 받는 구역(알약)의 사각형. 창 왼쪽 위 기준, 실제 픽셀.</summary>
    public PixelRect ZoneRect()
    {
        var src = PresentationSource.FromVisual(this);
        if (src?.CompositionTarget == null) return default;
        var toDevice = src.CompositionTarget.TransformToDevice;
        var r = Pill.TransformToAncestor(this).TransformBounds(new Rect(0, 0, Pill.ActualWidth, Pill.ActualHeight));
        var p1 = toDevice.Transform(r.TopLeft);
        var p2 = toDevice.Transform(r.BottomRight);
        return PixelRect.FromLtrb((int)Math.Ceiling(p1.X), (int)Math.Ceiling(p1.Y), (int)Math.Floor(p2.X), (int)Math.Floor(p2.Y));
    }

    /// <summary>창이 스스로 그린 그림을 PNG로 저장한다(사용자 화면을 찍지 않음, docs/05 6장).</summary>
    public void SaveSnapshot(string path)
    {
        var src = PresentationSource.FromVisual(this);
        double scale = src?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        var bmp = new RenderTargetBitmap((int)(ActualWidth * scale), (int)(ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bmp.Render(Root);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var fs = File.Create(path);
        enc.Save(fs);
    }

    private void Pill_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => PillClicked?.Invoke();
}
