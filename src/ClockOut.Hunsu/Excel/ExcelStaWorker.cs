// 기능: 엑셀 인스턴스 하나를 맡는 전용 STA 스레드. 그 엑셀로 가는 모든 COM 호출을 차례로 처리한다(docs/01 3.7장 1번).
//       화면 스레드는 엑셀을 기다리지 않는다: 호출은 Task로 돌려받는다.
// 사용법: var w = new ExcelStaWorker(xlMainHwnd); BridgeCall r = await w.CallAsync("CoHunsu_Query", "selection.summary", "{}");
// 의존: Excel/ExcelLocator, Excel/OleMessageFilter, Core/RetryPolicy, Core/HResultNames.
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ClockOut.Hunsu.Core;

namespace ClockOut.Hunsu.Excel;

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
            _queue.Add(() => tcs.SetResult(CallOnThread(proc, args)));
        }
        catch (InvalidOperationException)
        {
            tcs.SetResult(new BridgeCall(false, null, 0, 0, 0, 0, "closed"));
        }
        return tcs.Task;
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
            try { Marshal.ReleaseComObject(_app); }
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
