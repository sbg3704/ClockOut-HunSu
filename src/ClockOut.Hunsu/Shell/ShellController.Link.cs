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
        else StartProbe();
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

    /// <summary>연결 창의 "이 엑셀 점검": 따라가는 엑셀을 속성으로만 읽어 사람이 읽을 줄들로 돌려준다(다리가 열려 있나, 보호된 보기, 앞 통합 문서, 입력을 받는 상태).</summary>
    internal async Task<(string Text, bool BridgeMissing)> InspectTargetAsync()
    {
        if (_target == IntPtr.Zero) return ("따라가는 엑셀 창이 없습니다.", false);
        var r = await _registry.InspectAsync(_target);
        _log.Write("excel_inspect", ("hwnd", _target.ToInt64()), ("attached", r.Attached), ("attach", r.Attach), ("bridge", r.BridgeLoaded), ("books", r.Workbooks), ("pv", r.ProtectedViews), ("ready", r.Ready), ("err", r.Error));
        if (!r.Attached) return ((ExcelElevationText(_target) is { } ev ? "⚠ " + ev + "\n" : "") + "이 엑셀에 붙지 못했습니다(" + r.Error + "). 다른 권한(관리자)으로 켠 엑셀이면 훈수꾼도 같은 권한으로 켜야 해요.", false);
        User32.GetWindowThreadProcessId(_target, out uint pidU);
        var same = ExcelWindows.List().Where(w => w.Pid == (int)pidU).ToList();
        var lines = new List<string>();
        if (ExcelElevationText(_target) is { } elevated) lines.Add("⚠ " + elevated);
        static string Rights(bool? e) => e == null ? "알 수 없음(못 열었다)" : e == true ? "관리자" : "일반";
        lines.AddRange(new[]
        {
            $"권한: 엑셀 {Rights(Native.ProcessElevation.TryIsElevated((int)pidU))} · 훈수꾼 {Rights(Native.ProcessElevation.TryIsElevated(Environment.ProcessId))} (다르면 못 읽어요)",
            "훈수꾼 판 " + BuildInfo.Text,
            $"따라가는 창 {_target.ToInt64()} · 프로세스 {pidU}번 · 같은 프로세스의 엑셀 주 창 {same.Count}개(보이는 것 {same.Count(w => w.Visible)}개) · 붙는 방법 {r.Attach}",
            "다리 파일: " + (BridgeFilePath() ?? "못 찾음"),
            r.BridgeLoaded ? "다리: 열려 있음" : "다리: 이 엑셀에 안 열려 있음 (그래서 패널이 비어요)",
            $"열린 통합 문서 {r.Workbooks}개 · 앞의 문서: {(r.ActiveBook.Length > 0 ? r.ActiveBook : "없음")}{(r.ActiveReadOnly ? " (읽기 전용)" : "")}",
            r.ProtectedViews > 0 ? $"보호된 보기 창 {r.ProtectedViews}개: 거기서 「편집 사용」을 눌러야 읽을 수 있어요" : "보호된 보기: 없음",
            r.Ready ? "엑셀 상태: 입력 받는 중" : "엑셀 상태: 바쁨 또는 대화 상자가 떠 있음 (링크 업데이트 확인 창 등을 닫아 보세요)",
        });
        if (r.Error.Length > 0) lines.Add("읽다가 막힘: " + r.Error);
        return (string.Join("\n", lines), !r.BridgeLoaded);
    }

    /// <summary>다리 파일 위치: 엑셀 추가기능 폴더에 깔린 것, 없으면 묶음에 든 것(co build 시험 폴더). 없으면 null.</summary>
    private static string? BridgeFilePath()
    {
        string installed = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "AddIns", ExcelLocator.BridgeFile);
        if (System.IO.File.Exists(installed)) return installed;
        string bundled = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", ExcelLocator.BridgeFile));
        return System.IO.File.Exists(bundled) ? bundled : null;
    }

    /// <summary>"브리지 열기": 따라가는 엑셀에 다리가 없을 때, 사용자가 확인하면 그 엑셀에서 다리 파일을 연다. 조용히 하지 않는다. 사용자 엑셀을 건드리는 일이라 확인 대화 상자를 거친다.</summary>
    internal async Task<string> OpenBridgeInTargetAsync()
    {
        if (_target == IntPtr.Zero) return "따라가는 엑셀 창이 없습니다.";
        string? path = BridgeFilePath();
        if (path == null) return "다리 파일을 찾지 못했습니다. 칼퇴 왼쪽 레일에서 훈수꾼을 설치하세요.";
        var ok = WriteDialog.Ask("이 엑셀에서 다리 열기",
            "이 엑셀 창에는 훈수꾼 다리(clockout-hunsu.xlam)가 안 열려 있어 내용을 못 읽어요. 이 엑셀에서 다리 파일을 한 번 열까요?\n\n- 엑셀 설정과 열려 있는 통합 문서는 바뀌지 않아요.\n- 이 엑셀을 닫으면 다시 안 열려 있어요(영구 설치 아님).\n- 파일: " + path,
            "열기", "취소", null, "warn");
        if (ok != WriteDialog.Choice.Ok) return "취소했습니다.";
        string err = await _registry.OpenBridgeAsync(_target, path);
        _log.Write("bridge_open", ("hwnd", _target.ToInt64()), ("err", err));
        return err.Length == 0 ? "다리를 열었습니다. 칸을 눌러 패널이 바뀌는지 보세요." : "다리를 열지 못했습니다: " + err;
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
