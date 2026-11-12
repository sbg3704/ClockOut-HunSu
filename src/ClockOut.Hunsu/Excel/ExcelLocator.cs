// 기능: 엑셀 주 창(XLMAIN) 핸들로 그 인스턴스의 Application COM 객체를 얻는다. 사용자가 연 다른 엑셀은 건드리지 않는다.
// 사용법: (전용 STA 스레드에서) object? app = ExcelLocator.Find(xlMainHwnd, out string how);
//   1) 창 XLMAIN > XLDESK > EXCEL7에서 AccessibleObjectFromWindow(OBJID_NATIVEOM) → Window.Application
//   2) 안 되면 실행 중 개체 표(ROT)에서 "clockout-hunsu.xlam"이 열린 것만 골라 Application.Hwnd가 같은지 본다
// 의존: oleacc, ole32. 다리가 없는 엑셀(사용자 엑셀)은 2)에서 고르지 않으므로 손대지 않는다.
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using ClockOut.Hunsu.Native;

namespace ClockOut.Hunsu.Excel;

internal static class ExcelLocator
{
    public const string BridgeFile = "clockout-hunsu.xlam";
    private const uint OBJID_NATIVEOM = 0xFFFFFFF0;
    private static readonly Guid IID_IDispatch = new("00020400-0000-0000-C000-000000000046");

    [DllImport("oleacc.dll")]
    private static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint id, ref Guid iid, [MarshalAs(UnmanagedType.IDispatch)] out object? obj);

    [DllImport("ole32.dll")]
    private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable rot);

    [DllImport("ole32.dll")]
    private static extern int CreateBindCtx(int reserved, out IBindCtx ctx);

    public static object? Find(IntPtr xlMain, out string how)
    {
        how = "none";
        var app = FromWindow(xlMain);
        if (app != null) { how = "window"; return app; }
        app = FromRot(xlMain);
        if (app != null) { how = "rot"; return app; }
        return null;
    }

    private static object? FromWindow(IntPtr xlMain)
    {
        IntPtr desk = User32.FindWindowEx(xlMain, IntPtr.Zero, "XLDESK", null);
        IntPtr sheet = desk == IntPtr.Zero ? IntPtr.Zero : User32.FindWindowEx(desk, IntPtr.Zero, "EXCEL7", null);
        if (sheet == IntPtr.Zero) return null;
        var iid = IID_IDispatch;
        if (AccessibleObjectFromWindow(sheet, OBJID_NATIVEOM, ref iid, out var window) != 0 || window == null) return null;
        try { return Com.Get(window, "Application"); }
        catch (COMException) { return null; }
        finally { Marshal.ReleaseComObject(window); }
    }

    private static object? FromRot(IntPtr xlMain)
    {
        if (GetRunningObjectTable(0, out var rot) != 0) return null;
        rot.EnumRunning(out var monikers);
        var one = new IMoniker[1];
        while (monikers.Next(1, one, IntPtr.Zero) == 0)
        {
            var mk = one[0];
            try
            {
                CreateBindCtx(0, out var ctx);
                mk.GetDisplayName(ctx, null, out var name);
                if (name == null || !name.EndsWith(BridgeFile, StringComparison.OrdinalIgnoreCase)) continue;
                rot.GetObject(mk, out var book);
                var app = Com.Get(book, "Application");
                Marshal.ReleaseComObject(book);
                if (app != null && Convert.ToInt64(Com.Get(app, "Hwnd"), System.Globalization.CultureInfo.InvariantCulture) == xlMain.ToInt64()) return app;
                if (app != null) Marshal.ReleaseComObject(app);
            }
            catch (COMException) { /* 다른 프로세스의 항목은 읽지 못할 수 있다: 건너뛴다 */ }
            finally { Marshal.ReleaseComObject(mk); }
        }
        return null;
    }
}

/// <summary>늦은 바인딩 COM 호출 도우미(dynamic 없이 IDispatch로).</summary>
internal static class Com
{
    // 엑셀 COM은 LCID가 맞지 않으면 TYPE_E_INVDATAREAD(0x80028018)로 실패한다(0단계에서 실제로 봄). 늘 지원되는 en-US로 고정.
    private static readonly System.Globalization.CultureInfo ExcelCulture = System.Globalization.CultureInfo.GetCultureInfo(1033);

    public static object? Get(object target, string name) =>
        Unwrap(() => target.GetType().InvokeMember(name, BindingFlags.GetProperty, null, target, null, ExcelCulture));

    public static object? Call(object target, string name, params object?[] args) =>
        Unwrap(() => target.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, target, args, ExcelCulture));

    private static object? Unwrap(Func<object?> f)
    {
        try { return f(); }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }
}
