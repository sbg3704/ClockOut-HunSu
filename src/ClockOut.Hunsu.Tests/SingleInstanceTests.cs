// 기능: 단일 실행 잠금 시험: 같은 이름은 하나만, 측정 인자가 있으면 잠그지 않음, 풀면 다시 잡힘.
// 사용법: dotnet test
// 의존: ClockOut.Hunsu.Core(SingleInstance, AppOptions)
using ClockOut.Hunsu.Core;

namespace ClockOut.Hunsu.Tests;

public class SingleInstanceTests
{
    private static string UniqueName() => @"Local\ClockOut.Hunsu.Test." + Guid.NewGuid().ToString("N");

    // 뮤텍스는 잡은 스레드가 주인이라, 시험 스레드는 그대로 두고 따로 만든 스레드에서 확인한다(Task.Result는 xUnit1031로 막힘).
    private static bool OnOtherThread(Func<bool> check)
    {
        bool result = false;
        var t = new Thread(() => result = check());
        t.Start();
        t.Join();
        return result;
    }

    [Fact]
    public void OtherThread_CannotAcquire_WhileHeld_AndCanAfterRelease()
    {
        var name = UniqueName();
        var normal = AppOptions.Parse(Array.Empty<string>());
        var first = SingleInstance.TryAcquire(normal, name);
        Assert.NotNull(first);
        bool blocked = OnOtherThread(() => SingleInstance.TryAcquire(normal, name) is null);
        Assert.True(blocked);
        first!.Dispose();
        bool free = OnOtherThread(() => { using var g = SingleInstance.TryAcquire(normal, name); return g is not null; });
        Assert.True(free);
    }

    [Fact]
    public void MeasurementArguments_DoNotLock()
    {
        var name = UniqueName();
        var layout = AppOptions.Parse(new[] { "--layout", "x.json" });
        Assert.False(SingleInstance.IsNormalLaunch(layout));
        using var a = SingleInstance.TryAcquire(layout, name);
        using var b = SingleInstance.TryAcquire(layout, name);
        Assert.NotNull(a);
        Assert.NotNull(b);
    }
}
