// 기능: 전역 단축키 Ctrl+Alt+H(부르기·닫기), Ctrl+Alt+Shift+H(전부 숨김). 엑셀이나 훈수꾼이 앞에 있을 때만 등록하고 다른 프로그램이 앞이면 해제한다(docs/01 12-1장).
// 사용법: var hk = new HotkeyService(signalHwnd, log); hk.Summon += ...; hk.HideAll += ...; signalWindow.Hotkey += hk.OnHotkey;
// 의존: Native/User32, Core/HotkeyPolicy. 앞 창 바뀜은 SetWinEventHook(EVENT_SYSTEM_FOREGROUND)으로 받는다(되풀이 확인 없음).
using System.Diagnostics;
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Diagnostics;
using ClockOut.Hunsu.Native;

namespace ClockOut.Hunsu.Shell;

internal sealed class HotkeyService : IDisposable
{
    public const int SummonId = 1, HideAllId = 2;
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private readonly IntPtr _hwnd;
    private readonly RunLog _log;
    private readonly User32.WinEventProc _proc;
    private readonly IntPtr _hook;
    private readonly int _ownPid = Environment.ProcessId;
    private bool _registered;

    public HotkeyService(IntPtr messageWindow, RunLog log)
    {
        _hwnd = messageWindow;
        _log = log;
        _proc = (_, _, hwnd, obj, _, _, _) => { if (obj == User32.OBJID_WINDOW) Evaluate(hwnd); };
        _hook = User32.SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _proc, 0, 0, User32.WINEVENT_OUTOFCONTEXT);
        Evaluate(User32.GetForegroundWindow());
    }

    public event Action? Summon;
    public event Action? HideAll;

    /// <summary>앞 창이 바뀌었을 때(엑셀 창이면 그 창 핸들도 알려 준다 — 따라갈 엑셀을 바꿀 때 쓴다).</summary>
    public event Action<IntPtr, bool>? ForegroundChanged;

    public bool Registered => _registered;

    private void Evaluate(IntPtr fg)
    {
        User32.GetWindowThreadProcessId(fg, out uint pid);
        string cls = User32.ClassOf(fg);
        bool want = HotkeyPolicy.ShouldRegister(cls, (int)pid, _ownPid);
        if (want && !_registered) Register();
        else if (!want && _registered) Unregister();
        ForegroundChanged?.Invoke(fg, cls == HotkeyPolicy.ExcelMainClass);
    }

    private void Register()
    {
        bool a = User32.RegisterHotKey(_hwnd, SummonId, HotkeyPolicy.SummonMods, HotkeyPolicy.Key);
        bool b = User32.RegisterHotKey(_hwnd, HideAllId, HotkeyPolicy.HideAllMods, HotkeyPolicy.Key);
        _registered = a || b;
        // 실패는 다른 프로그램이 이미 쓰는 키라는 뜻. 1단계는 기록만 하고, 알림과 키 바꾸기는 설정 화면이 생기면 더한다.
        _log.Write("hotkey_register", ("summon", a), ("hideAll", b));
    }

    private void Unregister()
    {
        User32.UnregisterHotKey(_hwnd, SummonId);
        User32.UnregisterHotKey(_hwnd, HideAllId);
        _registered = false;
        _log.Write("hotkey_unregister");
    }

    /// <summary>신호 창이 WM_HOTKEY를 받으면 부른다(측정 도구도 같은 메시지를 보내 시험한다).</summary>
    public void OnHotkey(int id)
    {
        _log.Write("hotkey", ("id", id), ("at", Stopwatch.GetTimestamp()));
        if (id == SummonId) Summon?.Invoke();
        else if (id == HideAllId) HideAll?.Invoke();
    }

    public void Dispose()
    {
        if (_registered) Unregister();
        if (_hook != IntPtr.Zero) User32.UnhookWinEvent(_hook);
    }
}
