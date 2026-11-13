// 기능: 엑셀(또는 지정한 창)의 이동, 크기 바뀜, 최소화, 숨김, 닫힘을 SetWinEventHook으로 받아 오버레이 창을 그 자리에 붙인다.
// 사용법: var f = new WindowFollower(overlayHwnd, targetHwnd, log, OverlayPlacement.TopRightInside, t => 높이); f.Start(); ... f.Dispose();
//         place = (대상 사각형, 내 폭, 내 높이) → 놓을 사각형. height가 있으면 대상 크기에 맞춰 내 높이도 바꾼다(도크 창). Suspended면 숨긴다(전부 숨김).
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
    private readonly Func<PixelRect, int, int, PixelRect> _place;
    private readonly Func<PixelRect, int>? _height;
    private bool _hidden;

    public WindowFollower(IntPtr overlay, IntPtr target, RunLog log, Func<PixelRect, int, int, PixelRect>? place = null, Func<PixelRect, int>? height = null)
    {
        _overlay = overlay;
        _target = target;
        _log = log;
        _proc = OnWinEvent;
        _place = place ?? ((t, w, h) => OverlayPlacement.TopRightInside(t, w, h));
        _height = height;
    }

    public IntPtr Target => _target;

    /// <summary>전부 숨김(Ctrl+Alt+Shift+H) 중이면 true. 바꾼 뒤 Sync("show")를 부른다.</summary>
    public bool Suspended { get; set; }

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
        if (Suspended)
        {
            Hide("suspended");
            return;
        }
        var own = User32.WindowBounds(_overlay);
        int height = _height?.Invoke(target) ?? own.Height;
        var place = _place(target, own.Width, height);
        uint flags = User32.SWP_NOACTIVATE | User32.SWP_NOOWNERZORDER;
        if (place.Height == own.Height) flags |= User32.SWP_NOSIZE; // 크기가 같으면 옮기기만(레이어드 창 다시 그리기를 피함)
        if (_hidden) flags |= User32.SWP_SHOWWINDOW;
        // 이동 중에는 z순서를 건드리지 않는다(빠르게). 처음, 복원, 다시 보일 때만 "대상 창 바로 위"로 올린다.
        IntPtr after = why == "move" ? IntPtr.Zero : JustAboveTarget();
        if (after == IntPtr.Zero && why == "move") flags |= User32.SWP_NOZORDER;
        User32.SetWindowPos(_overlay, after, place.X, place.Y, place.Width, place.Height, flags);
        _hidden = false;
        // 이동은 자주 오므로 측정 기록을 켰을 때만 남긴다(t0 = 이벤트를 받은 시각, us = 자리 계산 + SetWindowPos 시간).
        if (why != "move" || _log.Enabled) _log.Write("follow", ("why", why), ("rect", place), ("us", Elapsed(t0)), ("t0", t0));
    }

    private IntPtr JustAboveTarget() => User32.InsertAfterJustAbove(_overlay, _target);

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
