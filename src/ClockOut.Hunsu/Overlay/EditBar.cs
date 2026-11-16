// 기능: 편집 모드(Ctrl+Alt+H로 부름)일 때 엑셀 창 위쪽 가운데에 뜨는 작은 띠. 안내 글과 [저장하고 나가기] [버리고 나가기] 단추를 보여 준다. 엑셀 포커스를 뺏지 않는다.
// 사용법: var bar = new EditBar(owner); bar.SaveClicked += ...; bar.DiscardClicked += ...; bar.ShowOver(엑셀 사각형); bar.Hide();
// 의존: Shell/ManagerParts(Chip), Native/User32, Core/PixelRect, Ui Theme. 값은 Theme 이름으로만 쓴다.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Native;
using ClockOut.Hunsu.Shell;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Overlay;

internal sealed class EditBar : Window
{
    private IntPtr _owner;

    public EditBar(IntPtr owner)
    {
        _owner = owner;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        Title = "훈수꾼 편집 띠";
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(new TextBlock
        {
            Text = "편집 모드  패널을 끌어 옮기고, 자물쇠로 잠그세요",
            Foreground = (Brush)FindResource(ThemeKeys.Text),
            FontSize = (double)FindResource("Hunsu.FontSmall"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 14, 0),
        });
        row.Children.Add(ManagerParts.Chip(this, "저장하고 나가기", true, () => SaveClicked?.Invoke()));
        row.Children.Add(ManagerParts.Chip(this, "버리고 나가기", false, () => DiscardClicked?.Invoke()));
        Content = new Border
        {
            Child = row,
            Margin = (Thickness)FindResource("Hunsu.ShadowMargin"),
            Padding = new Thickness(16, 8, 10, 8),
            CornerRadius = (CornerRadius)FindResource("Hunsu.PanelRadius"),
            Background = (Brush)FindResource("Hunsu.Glass"),
            BorderBrush = (Brush)FindResource(ThemeKeys.Accent),
            BorderThickness = new Thickness(1),
            Effect = (System.Windows.Media.Effects.Effect)FindResource("Hunsu.Shadow1"),
        };
        SourceInitialized += (_, _) =>
        {
            Handle = new WindowInteropHelper(this).Handle;
            long ex = User32.GetWindowLongPtr(Handle, User32.GWL_EXSTYLE).ToInt64();
            User32.SetWindowLongPtr(Handle, User32.GWL_EXSTYLE, new IntPtr(ex | User32.WS_EX_NOACTIVATE | User32.WS_EX_TOOLWINDOW));
            if (_owner != IntPtr.Zero) User32.SetWindowLongPtr(Handle, User32.GWLP_HWNDPARENT, _owner);
        };
    }

    public IntPtr Handle { get; private set; }

    public event Action? SaveClicked;
    public event Action? DiscardClicked;

    public void SetOwner(IntPtr owner)
    {
        _owner = owner;
        if (Handle != IntPtr.Zero) User32.SetWindowLongPtr(Handle, User32.GWLP_HWNDPARENT, owner);
    }

    /// <summary>엑셀 창 위쪽 가운데에 놓고 보인다.</summary>
    public void ShowOver(PixelRect excel)
    {
        if (!IsVisible) Show();
        UpdateLayout();
        var src = PresentationSource.FromVisual(this);
        double scale = src?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        int w = (int)Math.Round(ActualWidth * scale), h = (int)Math.Round(ActualHeight * scale);
        int x = excel.X + Math.Max(0, (excel.Width - w) / 2), y = excel.Y + (int)Math.Round(8 * scale);
        var after = _owner != IntPtr.Zero ? User32.InsertAfterJustAbove(Handle, _owner) : IntPtr.Zero;
        User32.SetWindowPos(Handle, after, x, y, w, h, User32.SWP_NOACTIVATE | User32.SWP_SHOWWINDOW);
    }
}
