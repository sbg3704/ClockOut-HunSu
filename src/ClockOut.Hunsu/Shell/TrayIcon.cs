// 기능: 알림 영역(작업 표시줄 오른쪽) 아이콘과 메뉴: 부르기·닫기, 전부 숨기기·다시 보이기, 모듈 관리, 설정 마법사, 끄기. 끄는 단추가 없다는 0단계 지적을 메운다.
// 사용법: using var tray = new TrayIcon(); tray.SummonClicked += ...; tray.HideAllClicked += ...; tray.QuitClicked += ...
// 의존: WinForms NotifyIcon(WPF에 없음), System.Drawing. 아이콘은 실행 파일에 넣은 hunsu.ico(고양이, assets/icon)를 읽고, 못 읽으면 "훈" 원을 그려 대신한다. 이름은 다 적어 쓴다(WPF 이름과 겹치지 않게).
namespace ClockOut.Hunsu.Shell;

internal sealed class TrayIcon : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _icon;
    private readonly System.Drawing.Icon _image;
    private readonly System.Windows.Forms.ToolStripMenuItem _hideItem;

    /// <summary>알림 영역 툴팁은 63자까지라 넘으면 자른다.</summary>
    private static string Trim(string s) => s.Length <= 63 ? s : s[..63];

    public TrayIcon()
    {
        _image = LoadIcon() ?? DrawIcon();
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("부르기 · 닫기\tCtrl+Alt+H", null, (_, _) => SummonClicked?.Invoke());
        _hideItem = new System.Windows.Forms.ToolStripMenuItem("전부 숨기기\tCtrl+Alt+Shift+H", null, (_, _) => HideAllClicked?.Invoke());
        menu.Items.Add(_hideItem);
        menu.Items.Add("모듈 관리…", null, (_, _) => ManagerClicked?.Invoke());
        menu.Items.Add("모양 바꾸기…", null, (_, _) => LookClicked?.Invoke());
        menu.Items.Add("설정 마법사…", null, (_, _) => WizardClicked?.Invoke());
        menu.Items.Add("엑셀 연결…", null, (_, _) => ExcelLinkClicked?.Invoke());
        menu.Items.Add("남은 엑셀 정리…", null, (_, _) => ExcelCleanupClicked?.Invoke());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("훈수꾼 끄기", null, (_, _) => QuitClicked?.Invoke());
        _icon = new System.Windows.Forms.NotifyIcon { Icon = _image, Text = Trim("훈수꾼 " + BuildInfo.Text), ContextMenuStrip = menu, Visible = true };
        _icon.MouseClick += (_, e) => { if (e.Button == System.Windows.Forms.MouseButtons.Left) SummonClicked?.Invoke(); };
    }

    public event Action? SummonClicked;
    public event Action? HideAllClicked;
    public event Action? QuitClicked;
    public event Action? ManagerClicked;
    public event Action? LookClicked;
    public event Action? WizardClicked;
    public event Action? ExcelCleanupClicked;
    public event Action? ExcelLinkClicked;

    /// <summary>알림 영역 풍선 안내(다리 갱신처럼 사용자가 알아야 하는 한 줄).</summary>
    /// <summary>아이콘을 보일지(설정만 모드에서는 숨겼다가 훈수꾼을 켜면 보인다).</summary>
    public bool Visible { get => _icon.Visible; set => _icon.Visible = value; }

    public void Balloon(string text) => _icon.ShowBalloonTip(8000, "훈수꾼", text, System.Windows.Forms.ToolTipIcon.Info);

    public void SetHidden(bool hidden) => _hideItem.Text = hidden ? "다시 보이기\tCtrl+Alt+Shift+H" : "전부 숨기기\tCtrl+Alt+Shift+H";

    /// <summary>실행 파일에 넣은 hunsu.ico에서 알림 영역 크기(보통 16·20·24px)에 맞는 그림을 읽는다. 없거나 깨지면 null.</summary>
    private static System.Drawing.Icon? LoadIcon()
    {
        try
        {
            using var s = typeof(TrayIcon).Assembly.GetManifestResourceStream("hunsu.ico");
            return s is null ? null : new System.Drawing.Icon(s, System.Windows.Forms.SystemInformation.SmallIconSize);
        }
        catch (Exception ex) when (ex is ArgumentException or System.IO.IOException)
        {
            return null; // 아이콘 읽기 실패는 앱을 멈출 일이 아니다: 그려 만든 아이콘으로 대신한다.
        }
    }

    /// <summary>(대체용) 청록 원 안에 "훈"(docs/03 accent). 그림 파일을 두지 않으려고 실행 때 그린다.</summary>
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
