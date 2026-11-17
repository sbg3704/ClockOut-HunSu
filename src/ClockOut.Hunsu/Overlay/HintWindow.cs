// 기능: 패널을 끌 때 잠깐 뜨는 한 줄 안내(말풍선). 마우스와 키를 가로채지 않고 엑셀의 초점도 빼앗지 않는다.
// 사용법: var h = new HintWindow(); h.ShowAt("안내 글", x, y);(실제 픽셀) ... h.HideHint();  끝나면 Close().
// 의존: Native/User32, Hunsu.* 색 이름. 화면(WPF)이라 시험은 못 한다.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using ClockOut.Hunsu.Native;

namespace ClockOut.Hunsu.Overlay;

internal sealed class HintWindow : Window
{
    private readonly TextBlock _text = new() { TextWrapping = TextWrapping.Wrap, MaxWidth = 260 };
    private bool _shown;

    public HintWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        IsHitTestVisible = false;
        _text.SetResourceReference(TextBlock.ForegroundProperty, "Hunsu.AccentInk");
        _text.FontFamily = (FontFamily)Application.Current.FindResource("Hunsu.Font");
        _text.FontSize = (double)Application.Current.FindResource("Hunsu.FontBody");
        var box = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6, 10, 6), Child = _text, Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 8, ShadowDepth = 1, Opacity = 0.4 } };
        box.SetResourceReference(Border.BackgroundProperty, "Hunsu.Accent");
        Content = box;
        SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            long ex = User32.GetWindowLongPtr(h, User32.GWL_EXSTYLE).ToInt64();
            User32.SetWindowLongPtr(h, User32.GWL_EXSTYLE, new IntPtr(ex | User32.WS_EX_NOACTIVATE | User32.WS_EX_TOOLWINDOW | User32.WS_EX_TRANSPARENT));
        };
    }

    /// <summary>안내를 (x, y) 화면 픽셀 자리에 띄운다(이미 떠 있으면 글과 자리만 바꾼다).</summary>
    public void ShowAt(string text, int x, int y)
    {
        if (_text.Text != text) _text.Text = text;
        if (!_shown) { Show(); _shown = true; }
        var h = new WindowInteropHelper(this).Handle;
        User32.SetWindowPos(h, User32.HWND_TOPMOST, x, y, 0, 0, User32.SWP_NOACTIVATE | User32.SWP_NOSIZE | User32.SWP_SHOWWINDOW);
    }

    public void HideHint()
    {
        if (!_shown) return;
        Hide();
        _shown = false;
    }
}
