// 기능: 엑셀 직접 연결(ShellController의 일부). 자동으로 엑셀을 못 찾거나 엉뚱한 엑셀 창을 따라갈 때, 사용자가 실행 중인 엑셀 창 목록에서 고르거나 화면의 엑셀 창을 눌러 골라 그 엑셀만 따라가게 한다.
//       고른 엑셀은 프로세스 번호와 시작 시각으로 설정(linkedExcel)에 기억한다(창 번호는 엑셀을 다시 켜면 바뀌므로). 그 엑셀이 없어지면 자동 찾기로 돌아가고, 같은 엑셀이 다시 나타나면 다시 따라간다. 풀기는 [엑셀 연결] 창의 "자동으로 되돌리기".
//       엑셀 안의 값은 창 핸들 하나로 읽는다: 다리가 XLMAIN 창 → XLDESK → EXCEL7에서 AccessibleObjectFromWindow(OBJID_NATIVEOM)로 Application을 얻는다(Excel/ExcelLocator). 여기서는 어느 창을 따라갈지만 정한다.
// 사용법: 훈 원 우클릭·알림 아이콘·사이드바 하단 🔗가 OpenExcelLink()를 부른다. 시작과 엑셀 찾기(OnReady, 기다리는 중 찾기, 앞 창 바뀜, 사이드바)는 FindTargetExcel()·LinkedMain()을 쓴다.
// 의존: Excel/ExcelWindows, Core/Procs/ExcelLink, Overlay/HintWindow, Shell/ExcelLinkWindow. 엑셀에 COM으로 말을 걸지 않는다(창 목록과 마우스 위치만 본다).
using System.Windows;
using System.Windows.Threading;
using ClockOut.Hunsu.Core.Procs;
using ClockOut.Hunsu.Excel;
using ClockOut.Hunsu.Native;
using ClockOut.Hunsu.Overlay;

namespace ClockOut.Hunsu.Shell;

internal sealed partial class ShellController
{
    private ExcelLinkWindow? _linkWindow;
    private DispatcherTimer? _pickTimer;
    private HintWindow? _pickHint;
    private int _noExcelTicks;
    private bool _toldLink;

    /// <summary>직접 연결한 엑셀의 지금 주 창. 연결하지 않았거나 그 엑셀이 없으면 IntPtr.Zero.</summary>
    internal IntPtr LinkedMain() =>
        ExcelLink.TryParse(_settings.LinkedExcel, out int pid, out long ticks) ? ExcelWindows.MainOfProcess(pid, ticks) : IntPtr.Zero;

    /// <summary>따라갈 엑셀 창: 직접 연결한 엑셀이 있으면 그것, 아니면 화면에 보이는 첫 엑셀 창.</summary>
    private IntPtr FindTargetExcel()
    {
        var linked = LinkedMain();
        return linked != IntPtr.Zero ? linked : ExcelWindows.FindVisibleMain();
    }

    /// <summary>직접 연결 사용 중인가(연결한 엑셀이 지금 있는지와 무관, 설정에 있으면 true).</summary>
    internal bool IsLinked => ExcelLink.TryParse(_settings.LinkedExcel, out _, out _);

    internal string LinkedLabel
    {
        get
        {
            if (!ExcelLink.TryParse(_settings.LinkedExcel, out int pid, out long ticks)) return "";
            var w = ExcelWindows.List().FirstOrDefault(x => x.Pid == pid && x.StartTicks == ticks);
            return w == null ? $"엑셀 {pid}번 (지금은 없음)" : $"엑셀 {pid}번 · {w.Title}";
        }
    }

    internal void LinkExcel(ExcelWin w)
    {
        ApplySetting("linkedExcel", ExcelLink.Format(w.Pid, w.StartTicks));
        _log.Write("excel_link", ("pid", w.Pid), ("hwnd", w.Hwnd.ToInt64()), ("title", w.Title));
        _finder?.Stop();
        _noExcelTicks = 0;
        if (w.Hwnd != _target) Follow(w.Hwnd);
        if (!_hiddenAll) foreach (var p in ActivePins()) p.Reappear();
        _tray.Balloon($"엑셀 {w.Pid}번에 연결했습니다. 다른 엑셀 창이 앞에 나와도 이 엑셀만 따라갑니다. 풀려면 「엑셀 연결」에서 자동으로 되돌리세요.");
    }

    internal void UnlinkExcel()
    {
        ApplySetting("linkedExcel", "");
        _log.Write("excel_unlink");
        var auto = ExcelWindows.FindVisibleMain();
        if (auto != IntPtr.Zero && auto != _target) Follow(auto);
    }

    /// <summary>[엑셀 연결] 창을 연다(이미 열려 있으면 앞으로).</summary>
    internal void OpenExcelLink()
    {
        if (_linkWindow != null) { _linkWindow.Activate(); return; }
        _linkWindow = new ExcelLinkWindow(this);
        _linkWindow.Closed += (_, _) => _linkWindow = null;
        _linkWindow.Show();
    }

    /// <summary>"창 눌러 고르기": 안내 말풍선을 마우스 옆에 띄우고, 다음 왼쪽 클릭이 닿은 창이 엑셀이면 연결한다. Esc 또는 엑셀이 아닌 창을 누르면 취소. 클릭은 막지 않는다(그 창에 그대로 전해진다).</summary>
    internal void BeginPickExcelWindow(Action<string> status)
    {
        if (_pickTimer != null) return;
        _pickHint = new HintWindow();
        bool wasDown = User32.LeftButtonDown(); // 이 단추를 누른 클릭은 건너뛴다
        void Finish()
        {
            _pickTimer?.Stop();
            _pickTimer = null;
            _pickHint?.Close();
            _pickHint = null;
        }
        _pickTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(40), DispatcherPriority.Input, (_, _) =>
        {
            User32.GetCursorPos(out var pt);
            _pickHint?.ShowAt("연결할 엑셀 창을 누르세요 · Esc 취소", pt.X + 24, pt.Y + 24);
            if ((User32.GetAsyncKeyState(0x1B) & 0x8000) != 0) { Finish(); status("고르기를 취소했습니다."); return; }
            bool down = User32.LeftButtonDown();
            bool pressed = down && !wasDown;
            wasDown = down;
            if (!pressed) return;
            var hwnd = ExcelWindows.MainAtPoint(pt.X, pt.Y);
            Finish();
            var win = hwnd == IntPtr.Zero ? null : ExcelWindows.List().FirstOrDefault(w => w.Hwnd == hwnd);
            if (win == null) { status("엑셀 창이 아니었습니다. 다시 눌러 보세요."); return; }
            LinkExcel(win);
            status($"엑셀 {win.Pid}번에 연결했습니다.");
        }, Application.Current.Dispatcher);
        _pickTimer.Start();
    }

    /// <summary>엑셀 프로세스는 있는데 따라갈 창을 못 찾은 채 20초가 지나면, 직접 연결하는 길을 풍선으로 한 번 알린다(훈 원도 숨어 있으니 알림 아이콘 메뉴를 알려 준다).</summary>
    private void NoteNoExcelWindow()
    {
        if (_toldLink || ++_noExcelTicks < 10 || ExcelProcesses.Snapshot().Count == 0) return;
        _toldLink = true;
        _log.Write("excel_link_hint");
        _tray.Balloon("엑셀이 켜져 있는데 훈수꾼이 창을 못 찾았어요. 알림 영역 훈수꾼 아이콘 우클릭 → 「엑셀 연결…」에서 직접 골라 연결할 수 있어요.");
    }
}
