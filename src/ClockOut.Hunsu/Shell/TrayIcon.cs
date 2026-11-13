// 기능: 알림 영역(작업 표시줄 오른쪽) 아이콘과 메뉴: 부르기·닫기, 전부 숨기기·다시 보이기, 모듈 관리, 끄기. 끄는 단추가 없다는 0단계 지적을 메운다.
// 사용법: using var tray = new TrayIcon(); tray.SummonClicked += ...; tray.HideAllClicked += ...; tray.QuitClicked += ...
// 의존: WinForms NotifyIcon(WPF에 없음), System.Drawing(아이콘을 그림 파일 없이 그려 만든다). 이름은 다 적어 쓴다(WPF 이름과 겹치지 않게).
namespace ClockOut.Hunsu.Shell;

internal sealed class TrayIcon : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _icon;
    private readonly System.Drawing.Icon _image;
    private readonly System.Windows.Forms.ToolStripMenuItem _hideItem;

    public TrayIcon()
    {
        _image = DrawIcon();
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("부르기 · 닫기\tCtrl+Alt+H", null, (_, _) => SummonClicked?.Invoke());
        _hideItem = new System.Windows.Forms.ToolStripMenuItem("전부 숨기기\tCtrl+Alt+Shift+H", null, (_, _) => HideAllClicked?.Invoke());
        menu.Items.Add(_hideItem);
        menu.Items.Add("모듈 관리…", null, (_, _) => ManagerClicked?.Invoke());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("훈수꾼 끄기", null, (_, _) => QuitClicked?.Invoke());
        _icon = new System.Windows.Forms.NotifyIcon { Icon = _image, Text = "훈수꾼", ContextMenuStrip = menu, Visible = true };
        _icon.MouseClick += (_, e) => { if (e.Button == System.Windows.Forms.MouseButtons.Left) SummonClicked?.Invoke(); };
    }

    public event Action? SummonClicked;
    public event Action? HideAllClicked;
    public event Action? QuitClicked;
    public event Action? ManagerClicked;

    public void SetHidden(bool hidden) => _hideItem.Text = hidden ? "다시 보이기\tCtrl+Alt+Shift+H" : "전부 숨기기\tCtrl+Alt+Shift+H";

    /// <summary>청록 원 안에 "훈"(docs/03 accent). 그림 파일을 두지 않으려고 실행 때 그린다.</summary>
    private static System.Drawing.Icon DrawIcon()
    {
        using var bmp = new System.Drawing.Bitmap(32, 32);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using var fill = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0x5F, 0xD4, 0xE6));
            g.FillEllipse(fill, 1, 1, 30, 30);
            using var font = new System.Drawing.Font("Malgun Gothic", 15, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
            using var ink = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0x0B, 0x15, 0x20));
            var fmt = new System.Drawing.StringFormat { Alignment = System.Drawing.StringAlignment.Center, LineAlignment = System.Drawing.StringAlignment.Center };
            g.DrawString("훈", font, ink, new System.Drawing.RectangleF(0, 1, 32, 32), fmt);
        }
        IntPtr h = bmp.GetHicon();
        using var temp = System.Drawing.Icon.FromHandle(h);
        var copy = (System.Drawing.Icon)temp.Clone();
        Native.User32.DestroyIcon(h);
        return copy;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _image.Dispose();
    }
}
