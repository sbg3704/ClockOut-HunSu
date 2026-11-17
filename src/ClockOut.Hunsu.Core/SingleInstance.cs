// 기능: 훈수꾼 단일 실행 잠금. 보통 모드(인자 없음)로 켠 훈수꾼이 이미 있으면 새로 켠 쪽이 조용히 끝나게 한다.
//   칼퇴 레일의 켜기는 apps 폴더 안의 exe만 "켜져 있다"고 세므로, 개발 빌드·시험 실행기로 띄운 복사본이 있으면 둘이 같이 켜지던 것을 막는다(2026-11-17).
// 사용법: using var guard = SingleInstance.TryAcquire(options); if (guard is null) { /* 이미 켜져 있음: 끝낸다 */ }
//   측정 도구처럼 --follow·--layout·--log·--snapshot을 주는 실행은 여러 개가 돌아야 하므로 잠그지 않는다(빈 잠금을 돌려준다).
// 의존: AppOptions. 사용자 세션 안에서만 겹침을 본다(Local\ 이름).
namespace ClockOut.Hunsu.Core;

public sealed class SingleInstance : IDisposable
{
    /// <summary>사용자 세션 안에서 훈수꾼 하나만 허용하는 잠금 이름.</summary>
    public const string DefaultName = @"Local\ClockOut.Hunsu.SingleInstance";

    private readonly Mutex? _mutex;
    private SingleInstance(Mutex? mutex) => _mutex = mutex;

    /// <summary>보통 모드 실행인지(측정·시험용 인자가 하나도 없는지).</summary>
    public static bool IsNormalLaunch(AppOptions o) =>
        o.FollowHwnd == 0 && o.LogPath is null && o.SnapshotDir is null && o.LayoutPath is null;

    /// <summary>잠금을 잡는다. 이미 다른 훈수꾼이 잡고 있으면 null. 보통 모드가 아니면 잠그지 않고 빈 잠금을 돌려준다.</summary>
    public static SingleInstance? TryAcquire(AppOptions o, string name = DefaultName)
    {
        if (!IsNormalLaunch(o)) return new SingleInstance(null);
        var mutex = new Mutex(initiallyOwned: false, name, out _);
        bool owned;
        try { owned = mutex.WaitOne(0); }
        catch (AbandonedMutexException) { owned = true; } // 앞서 강제 종료된 훈수꾼이 남긴 잠금은 우리가 이어받는다
        if (owned) return new SingleInstance(mutex);
        mutex.Dispose();
        return null;
    }

    public void Dispose()
    {
        if (_mutex is null) return;
        try { _mutex.ReleaseMutex(); } catch (Exception ex) when (ex is ApplicationException or ObjectDisposedException) { } // 다른 스레드에서 풀면 던진다. 프로세스가 끝나면 어차피 풀린다
        _mutex.Dispose();
    }
}
