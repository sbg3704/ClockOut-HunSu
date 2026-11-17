// 기능: 엑셀 인스턴스 하나를 맡는 전용 STA 스레드. 그 엑셀로 가는 모든 COM 호출을 차례로 처리한다(docs/01 3.7장 1번).
//       화면 스레드는 엑셀을 기다리지 않는다: 호출은 Task로 돌려받는다.
// 사용법: var w = new ExcelStaWorker(xlMainHwnd); BridgeCall r = await w.CallAsync("CoHunsu_Query", "selection.summary", "{}");  string? key = await w.ViewKeyAsync() (매크로 없이 화면 자리만)
// 의존: Excel/ExcelLocator, Excel/OleMessageFilter, Core/RetryPolicy, Core/HResultNames.
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ClockOut.Hunsu.Core;

namespace ClockOut.Hunsu.Excel;

/// <summary>엑셀 인스턴스의 겉모습 점검 결과(읽기만 한 것). Attached=COM으로 붙었나, BridgeLoaded=다리 통합 문서가 열려 있나, ProtectedViews=보호된 보기 창 수(0이 아니면 다리가 그 파일을 못 봄), ActiveBook·ActiveReadOnly=지금 앞의 통합 문서, Ready=엑셀이 입력을 받을 수 있나, Error=읽다가 막힌 이유.</summary>
internal sealed record ExcelInspect(bool Attached, string Attach, bool BridgeLoaded, int Workbooks, int ProtectedViews, string ActiveBook, bool ActiveReadOnly, bool Ready, string Error);

/// <summary>다리 호출 한 번의 결과. Json은 다리가 돌려준 글(호출 자체가 실패하면 null).</summary>
internal sealed record BridgeCall(bool Ok, string? Json, int HResult, int Retries, int RejectType, double Ms, string Attach)
{
    public string ErrorCode => Ok ? "" : HResult == 0 ? "excel_missing" : HResultNames.ToErrorCode(HResult);
}

internal sealed class ExcelStaWorker : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;
    private readonly RetryPolicy _policy = RetryPolicy.Default;
    private readonly OleMessageFilter _filter;
    private object? _app;
    private string _attach = "none";

    public ExcelStaWorker(IntPtr excelHwnd)
    {
        ExcelHwnd = excelHwnd;
        _filter = new OleMessageFilter(_policy);
        _thread = new Thread(Loop) { IsBackground = true, Name = "hunsu-excel-" + excelHwnd.ToInt64() };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public IntPtr ExcelHwnd { get; }

    /// <summary>작업 스레드에서 한 일을 돌린다. 예상 밖 예외(COM 아닌 것)가 나도 스레드가 죽어 훈수꾼이 꺼지지 않게 막고, 기다리는 쪽에는 실패 값을 돌려준다.</summary>
    private void Run<T>(TaskCompletionSource<T> tcs, Func<T> work, Func<Exception, T> failed)
    {
        try { tcs.SetResult(work()); }
        catch (Exception ex)
        {
            Release(); // 이상한 상태일 수 있으니 COM 참조를 놓고 다음에 다시 붙는다
            tcs.TrySetResult(failed(ex));
        }
    }

    private void Loop()
    {
        OleMessageFilter.Register(_filter);
        try
        {
            foreach (var work in _queue.GetConsumingEnumerable()) work();
        }
        finally
        {
            Release();
            OleMessageFilter.Register(null);
        }
    }

    /// <summary>다리 진입점 하나를 부른다(예: "CoHunsu_Query", name, args). 엑셀이 거절하면 정책대로 다시 시도한다.</summary>
    public Task<BridgeCall> CallAsync(string proc, params object[] args)
    {
        var tcs = new TaskCompletionSource<BridgeCall>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            _queue.Add(() => Run(tcs, () => CallOnThread(proc, args), ex => new BridgeCall(false, null, ex.HResult, 0, 0, 0, "exception:" + ex.GetType().Name)));
        }
        catch (InvalidOperationException)
        {
            tcs.SetResult(new BridgeCall(false, null, 0, 0, 0, 0, "closed"));
        }
        return tcs.Task;
    }

    /// <summary>엑셀 화면 자리 글(스크롤 줄·칸, 확대, 창 나눔, 시트)을 속성으로만 읽는다. 다리 매크로(Application.Run)를 돌리지 않으므로 엑셀 커서가 '바쁨'으로 바뀌지 않는다. 못 읽으면 null.</summary>
    public Task<string?> ViewKeyAsync()
    {
        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            _queue.Add(() => Run(tcs, ViewKeyOnThread, _ => null));
        }
        catch (InvalidOperationException)
        {
            tcs.SetResult(null);
        }
        return tcs.Task;
    }

    private string? ViewKeyOnThread()
    {
        _app ??= ExcelLocator.Find(ExcelHwnd, out _attach);
        if (_app == null) return null;
        object? win = null, sheet = null;
        try
        {
            win = Com.Get(_app, "ActiveWindow");
            if (win == null) return null;
            sheet = Com.Get(win, "ActiveSheet");
            string name = sheet == null ? "" : Com.Get(sheet, "Name") as string ?? "";
            return string.Join("|", Com.Get(win, "ScrollRow"), Com.Get(win, "ScrollColumn"), Com.Get(win, "Zoom"),
                Com.Get(win, "SplitRow"), Com.Get(win, "SplitColumn"), Com.Get(win, "FreezePanes"), name);
        }
        catch (COMException ex)
        {
            if (HResultNames.IsGone(ex.HResult)) Release();
            return null; // 셀 편집 중이거나 바쁨: 이번에는 묻지 않는다
        }
        catch (InvalidComObjectException)
        {
            Release();
            return null;
        }
        finally
        {
            if (sheet != null && Marshal.IsComObject(sheet)) Marshal.ReleaseComObject(sheet);
            if (win != null && Marshal.IsComObject(win)) Marshal.ReleaseComObject(win);
        }
    }

    /// <summary>이 엑셀을 속성으로만 읽어 다리가 열려 있는지 등을 본다(매크로를 돌리지 않고 아무것도 바꾸지 않는다).</summary>
    public Task<ExcelInspect> InspectAsync()
    {
        var tcs = new TaskCompletionSource<ExcelInspect>(TaskCreationOptions.RunContinuationsAsynchronously);
        try { _queue.Add(() => Run(tcs, InspectOnThread, ex => new ExcelInspect(false, _attach, false, 0, 0, "", false, false, ex.GetType().Name + ": " + ex.Message))); }
        catch (InvalidOperationException) { tcs.SetResult(new ExcelInspect(false, "closed", false, 0, 0, "", false, false, "작업 스레드가 닫혔습니다")); }
        return tcs.Task;
    }

    private ExcelInspect InspectOnThread()
    {
        _app ??= ExcelLocator.Find(ExcelHwnd, out _attach);
        if (_app == null) return new ExcelInspect(false, _attach, false, 0, 0, "", false, false, "이 창의 엑셀에 COM으로 붙지 못했습니다");
        bool bridge = false, ro = false, ready = false;
        int books = 0, pv = 0;
        string active = "", error = "";
        try
        {
            var wbs = Com.Get(_app, "Workbooks")!;
            books = Convert.ToInt32(Com.Get(wbs, "Count"), System.Globalization.CultureInfo.InvariantCulture);
            for (int i = 1; i <= books && !bridge; i++)
            {
                var wb = Com.Call(wbs, "Item", i);
                if (wb == null) continue;
                try { bridge = string.Equals(Com.Get(wb, "Name") as string, ExcelLocator.BridgeFile, StringComparison.OrdinalIgnoreCase); }
                finally { Marshal.ReleaseComObject(wb); }
            }
            Marshal.ReleaseComObject(wbs);
            ready = Com.Get(_app, "Ready") is true;
            try
            {
                var pvs = Com.Get(_app, "ProtectedViewWindows");
                if (pvs != null) { pv = Convert.ToInt32(Com.Get(pvs, "Count"), System.Globalization.CultureInfo.InvariantCulture); Marshal.ReleaseComObject(pvs); }
            }
            catch (COMException) { /* 오래된 엑셀엔 보호된 보기 목록이 없다 */ }
            var ab = Com.Get(_app, "ActiveWorkbook");
            if (ab != null)
            {
                active = Com.Get(ab, "Name") as string ?? "";
                ro = Com.Get(ab, "ReadOnly") is true;
                Marshal.ReleaseComObject(ab);
            }
        }
        catch (COMException ex)
        {
            error = HResultNames.Name(ex.HResult);
            if (HResultNames.IsGone(ex.HResult)) Release();
        }
        return new ExcelInspect(true, _attach, bridge, books, pv, active, ro, ready, error);
    }

    /// <summary>다리 파일을 이 엑셀에서 연다(추가기능 파일을 통합 문서로 여는 것뿐, 엑셀 설정과 다른 파일은 안 건드린다). 사용자가 확인한 뒤에만 부른다. 돌려주는 글이 비면 성공.</summary>
    public Task<string> OpenBridgeAsync(string path)
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        try { _queue.Add(() => Run(tcs, () => OpenBridgeOnThread(path), ex => ex.GetType().Name + ": " + ex.Message)); }
        catch (InvalidOperationException) { tcs.SetResult("작업 스레드가 닫혔습니다"); }
        return tcs.Task;
    }

    private string OpenBridgeOnThread(string path)
    {
        _app ??= ExcelLocator.Find(ExcelHwnd, out _attach);
        if (_app == null) return "이 창의 엑셀에 COM으로 붙지 못했습니다";
        try
        {
            var wbs = Com.Get(_app, "Workbooks")!;
            var wb = Com.Call(wbs, "Open", path);
            if (wb != null) Marshal.ReleaseComObject(wb);
            Marshal.ReleaseComObject(wbs);
            return "";
        }
        catch (COMException ex)
        {
            return HResultNames.Name(ex.HResult);
        }
    }

    private BridgeCall CallOnThread(string proc, object[] args)
    {
        long t0 = Stopwatch.GetTimestamp();
        _app ??= ExcelLocator.Find(ExcelHwnd, out _attach);
        if (_app == null) return new BridgeCall(false, null, 0, 0, 0, Ms(t0), _attach);
        var all = new object?[args.Length + 1];
        all[0] = "'" + ExcelLocator.BridgeFile + "'!" + proc;
        Array.Copy(args, 0, all, 1, args.Length);
        int managedRetries = 0;
        while (true)
        {
            _filter.Reset();
            try
            {
                var result = Com.Call(_app, "Run", all) as string;
                return new BridgeCall(true, result, 0, _filter.Retries + managedRetries, _filter.LastRejectType, Ms(t0), _attach);
            }
            catch (COMException ex)
            {
                int hr = ex.HResult;
                // 메시지 필터가 이미 다시 시도했으면 여기서 또 하지 않는다. VBA_E_IGNORE처럼 필터를 거치지 않는 바쁨만 여기서.
                int wait = _filter.Retries == 0 && HResultNames.IsBusy(hr) ? _policy.NextDelayMs(managedRetries) : -1;
                if (wait >= 0)
                {
                    managedRetries++;
                    Thread.Sleep(wait);
                    continue;
                }
                if (HResultNames.IsGone(hr)) Release();
                return new BridgeCall(false, null, hr, _filter.Retries + managedRetries, _filter.LastRejectType, Ms(t0), _attach);
            }
            catch (InvalidComObjectException)
            {
                Release();
                return new BridgeCall(false, null, HResultNames.RpcDisconnected, managedRetries, 0, Ms(t0), _attach);
            }
        }
    }

    private void Release()
    {
        if (_app != null)
        {
            try { Marshal.FinalReleaseComObject(_app); }
            catch (InvalidComObjectException) { /* 이미 끊긴 객체: 놓기만 하면 된다 */ }
        }
        _app = null;
    }

    private static double Ms(long t0) => (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;

    public void Dispose()
    {
        _queue.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(2));
        _queue.Dispose();
    }
}
