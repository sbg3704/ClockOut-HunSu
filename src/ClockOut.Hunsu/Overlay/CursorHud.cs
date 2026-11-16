// 기능: 커서 옆 HUD. 범위를 끄는 동안 커서를 따라다니며 합계·개수를 작게 보여 주고, 놓으면 잠시 뒤 사라진다. 클릭은 그대로 엑셀로 지나간다(투명 창).
// 사용법: var h = new CursorHud(); h.Show("합계 1,234 · 5칸");  (끄는 동안 계속 부르면 따라다님. 마지막 호출 뒤 1.2초 지나면 숨김)
// 의존: Native/User32. 커서 위치와 마우스 단추는 직접 읽는다(투명한 곳은 이벤트가 오지 않음). 값은 Theme 이름으로만 쓴다.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ClockOut.Hunsu.Native;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Overlay;

internal sealed class CursorHud : Window
{
    private readonly TextBlock _text;
    private readonly DispatcherTimer _tick;
    private DateTime _until;

    public CursorHud()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        Topmost = true;
        Title = "훈수꾼 커서 HUD";
        _text = new TextBlock
        {
            Foreground = (Brush)FindResource(ThemeKeys.Text),
            FontSize = (double)FindResource("Hunsu.FontSmall"),
            FontWeight = FontWeights.SemiBold,
        };
        Content = new Border
        {
            Child = _text,
            Padding = new Thickness(8, 3, 8, 3),
            CornerRadius = (CornerRadius)FindResource("Hunsu.RowRadius"),
            Background = (Brush)FindResource("Hunsu.Glass"),
            BorderBrush = (Brush)FindResource(ThemeKeys.Accent),
            BorderThickness = new Thickness(1),
        };
        SourceInitialized += (_, _) =>
        {
            Handle = new WindowInteropHelper(this).Handle;
            long ex = User32.GetWindowLongPtr(Handle, User32.GWL_EXSTYLE).ToInt64();
            User32.SetWindowLongPtr(Handle, User32.GWL_EXSTYLE, new IntPtr(ex | User32.WS_EX_NOACTIVATE | User32.WS_EX_TOOLWINDOW | User32.WS_EX_TRANSPARENT));
        };
        _tick = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(30) };
        _tick.Tick += (_, _) =>
        {
            if (DateTime.UtcNow > _until && !User32.LeftButtonDown()) { _tick.Stop(); Hide(); return; }
            Follow();
        };
    }

    public IntPtr Handle { get; private set; }

    /// <summary>글을 바꾸고 커서 옆에 보인다. 마지막으로 부른 뒤 1.2초가 지나고 마우스 단추를 놓았으면 스스로 숨는다.</summary>
    public void Show(string text)
    {
        _text.Text = text;
        _until = DateTime.UtcNow.AddMilliseconds(1200);
        if (!IsVisible) { Show(); _tick.Start(); }
        Follow();
    }

    /// <summary>글을 바꾸고 화면 픽셀 자리 (x, y)에 고정해 보인다(고른 칸 옆). 따라다니지 않고, HideNow로 숨길 때까지 남는다.</summary>
    public void ShowAt(string text, int x, int y)
    {
        _tick.Stop();
        _text.Text = text;
        if (!IsVisible) Show();
        if (Handle == IntPtr.Zero) return;
        UpdateLayout();
        double scale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        int w = (int)Math.Round(ActualWidth * scale), h = (int)Math.Round(ActualHeight * scale);
        User32.SetWindowPos(Handle, new IntPtr(-1), x, y, w, h, User32.SWP_NOACTIVATE | User32.SWP_SHOWWINDOW);
    }

    public void HideNow()
    {
        _tick.Stop();
        if (IsVisible) Hide();
    }

    private void Follow()
    {
        if (Handle == IntPtr.Zero || !User32.GetCursorPos(out var pt)) return;
        UpdateLayout();
        double scale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        int w = (int)Math.Round(ActualWidth * scale), h = (int)Math.Round(ActualHeight * scale);
        User32.SetWindowPos(Handle, new IntPtr(-1), pt.X + (int)(18 * scale), pt.Y + (int)(22 * scale), w, h, User32.SWP_NOACTIVATE | User32.SWP_SHOWWINDOW);
    }
}
