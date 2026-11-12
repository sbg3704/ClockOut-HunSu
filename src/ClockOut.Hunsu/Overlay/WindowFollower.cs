// 기능: 엑셀(또는 지정한 창)의 이동, 크기 바뀜, 최소화, 숨김, 닫힘을 SetWinEventHook으로 받아 오버레이 창을 그 자리에 붙인다.
// 사용법: var f = new WindowFollower(overlayHwnd, targetHwnd, log); f.Start(); ... f.Dispose();
//         화면 스레드(메시지 루프가 있는 스레드)에서 만들고 부른다. 콜백도 그 스레드로 온다(WINEVENT_OUTOFCONTEXT).
// 의존: Native/User32, Core/OverlayPlacement, Diagnostics/RunLog. 오버레이는 대상 창의 "소유 창"이 되어 늘 그 위에 있다.
using System.Diagnostics;
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Diagnostics;
using ClockOut.Hunsu.Native;

namespace ClockOut.Hunsu.Overlay;

internal sealed class WindowFollower : IDisposable
{
    private readonly IntPtr _overlay;
    private readonly IntPtr _target;
    private readonly RunLog _log;
    private readonly User32.WinEventProc _proc; // 대리자를 필드로 붙잡아 GC가 거두지 않게 한다
    private readonly List<IntPtr> _hooks = [];
    private bool _hidden;

    public WindowFollower(IntPtr overlay, IntPtr target, RunLog log)
    {
        _overlay = overlay;
        _target = target;
        _log = log;
        _proc = OnWinEvent;
    }

    public IntPtr Target => _target;

    /// <summary>대상 창이 닫혔을 때(앱은 다른 엑셀 창을 찾거나 숨는다).</summary>
    public event Action? TargetGone;

    public void Start()
    {
        uint tid = User32.GetWindowThreadProcessId(_target, out uint pid);
        // 오버레이를 대상 창의 소유 창으로: 대상보다 늘 위, 대상이 최소화되면 함께 숨는다.
        User32.SetWindowLongPtr(_overlay, User32.GWLP_HWNDPARENT, _target);
        const uint flags = User32.WINEVENT_OUTOFCONTEXT | User32.WINEVENT_SKIPOWNPROCESS;
        Hook(User32.EVENT_OBJECT_LOCATIONCHANGE, User32.EVENT_OBJECT_LOCATIONCHANGE, pid, tid, flags);
        Hook(User32.EVENT_SYSTEM_MINIMIZESTART, User32.EVENT_SYSTEM_MINIMIZEEND, pid, tid, flags);
        Hook(User32.EVENT_OBJECT_DESTROY, User32.EVENT_OBJECT_HIDE, pid, tid, flags);
        _log.Write("follow_start", ("target", _target.ToInt64()), ("cls", User32.ClassOf(_target)), ("hooks", _hooks.Count));
        Sync("start");
    }

    private void Hook(uint min, uint max, uint pid, uint tid, uint flags)
    {
        var h = User32.SetWinEventHook(min, max, IntPtr.Zero, _proc, pid, tid, flags);
        if (h != IntPtr.Zero) _hooks.Add(h);
        else _log.Write("hook_failed", ("min", min), ("max", max));
    }

    private void OnWinEvent(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (hwnd != _target || idObject != User32.OBJID_WINDOW || idChild != 0) return;
        if (evt == User32.EVENT_OBJECT_DESTROY)
        {
            Hide("destroy");
            TargetGone?.Invoke();
            return;
        }
        Sync(evt switch
        {
            User32.EVENT_OBJECT_LOCATIONCHANGE => "move",
            User32.EVENT_SYSTEM_MINIMIZESTART => "minimize",
            User32.EVENT_SYSTEM_MINIMIZEEND => "restore",
            User32.EVENT_OBJECT_HIDE => "hide",
            User32.EVENT_OBJECT_SHOW => "show",
            _ => "other",
        });
    }

    /// <summary>대상 창의 지금 상태로 오버레이를 옮기거나 숨긴다. 크기가 바뀐 패널을 다시 맞출 때도 부른다.</summary>
    public void Sync(string why)
    {
        long t0 = Stopwatch.GetTimestamp();
        if (!User32.IsWindow(_target))
        {
            Hide("gone");
            return;
        }
        var target = User32.VisibleBounds(_target);
        bool minimized = User32.IsIconic(_target);
        if (OverlayPlacement.ShouldHide(User32.IsWindowVisible(_target), minimized, target))
        {
            Hide(why);
            return;
        }
        var own = User32.WindowBounds(_overlay);
        var place = OverlayPlacement.TopRightInside(target, own.Width, own.Height);
        uint flags = User32.SWP_NOACTIVATE | User32.SWP_NOSIZE | User32.SWP_NOOWNERZORDER;
        if (_hidden) flags |= User32.SWP_SHOWWINDOW;
        // 이동 중에는 z순서를 건드리지 않는다(빠르게). 처음, 복원, 다시 보일 때만 "대상 창 바로 위"로 올린다.
        IntPtr after = why == "move" ? IntPtr.Zero : JustAboveTarget();
        if (after == IntPtr.Zero && why == "move") flags |= User32.SWP_NOZORDER;
        User32.SetWindowPos(_overlay, after, place.X, place.Y, 0, 0, flags);
        _hidden = false;
        // 이동은 자주 오므로 측정 기록을 켰을 때만 남긴다(t0 = 이벤트를 받은 시각, us = 자리 계산 + SetWindowPos 시간).
        if (why != "move" || _log.Enabled) _log.Write("follow", ("why", why), ("rect", place), ("us", Elapsed(t0)), ("t0", t0));
    }

    /// <summary>
    /// 오버레이를 대상 창 바로 위에 끼울 자리(SetWindowPos의 hWndInsertAfter). 소유 창 지정만으로는 z순서가 바뀌지 않아
    /// 대상이 맨 위(TOPMOST)이거나 나중에 앞으로 나온 창이면 오버레이가 그 아래에 깔린다(0단계에서 실제로 봄).
    /// 다른 프로그램 위로 띄우지 않도록 HWND_TOP 대신 "대상 바로 위 창의 뒤"에 넣는다.
    /// </summary>
    private IntPtr JustAboveTarget()
    {
        bool topmost = (User32.GetWindowLongPtr(_target, User32.GWL_EXSTYLE).ToInt64() & User32.WS_EX_TOPMOST) != 0;
        IntPtr prev = User32.GetWindow(_target, User32.GW_HWNDPREV);
        if (prev == _overlay) prev = User32.GetWindow(_overlay, User32.GW_HWNDPREV);
        if (prev != IntPtr.Zero) return prev;
        return topmost ? User32.HWND_TOPMOST : User32.HWND_TOP;
    }

    private void Hide(string why)
    {
        if (_hidden) return;
        User32.ShowWindow(_overlay, User32.SW_HIDE);
        _hidden = true;
        _log.Write("follow_hide", ("why", why));
    }

    private static long Elapsed(long t0) => (Stopwatch.GetTimestamp() - t0) * 1_000_000 / Stopwatch.Frequency;

    public void Dispose()
    {
        foreach (var h in _hooks) User32.UnhookWinEvent(h);
        _hooks.Clear();
    }
}
