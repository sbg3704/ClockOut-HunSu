// 기능: 엑셀 추가기능 폴더에 이미 깔린 다리(clockout-hunsu.xlam)가 훈수꾼 묶음에 들어 있는 다리보다 낮은 판(또는 다른 내용)이면 스스로 바꿔 놓는다. 지우고 다시 설치하지 않아도 되게 한다.
//       엑셀이 다리 파일을 잡고 있으면(켜져 있는 동안) 바꾸지 못하므로 30초마다 다시 시도하고, 엑셀이 그 파일을 놓으면 바로 바꾼다. 바꾸기 전 옛 파일은 백업 폴더로 옮긴다.
// 사용법: new BridgeUpdater(log, notify).Start();  notify(글)는 알림 영역 풍선 안내에 쓴다(바꿈 기다리는 중, 바꿈 끝, 실패). Dispose로 멈춘다.
// 의존: Core/Bridge(BridgeUpdatePlan), Diagnostics/RunLog. 묶음의 다리는 <exe 폴더>\..\addins\clockout-hunsu.zip(칼퇴 설치) 또는 <exe 폴더>\..\clockout-hunsu.xlam(co build 시험 폴더).
//       다리가 아직 설치되지 않았으면 아무것도 하지 않는다(설치는 칼퇴가 한다). 엑셀 안에 들어가거나 통합 문서를 만지지 않는다.
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using ClockOut.Hunsu.Core.Bridge;
using ClockOut.Hunsu.Diagnostics;

namespace ClockOut.Hunsu.Shell;

internal sealed class BridgeUpdater : IDisposable
{
    private const string Xlam = "clockout-hunsu.xlam";
    private const string Side = "clockout-hunsu.clockout.json";
    private readonly RunLog _log;
    private readonly Action<string> _notify;
    private readonly CancellationTokenSource _stop = new();
    private bool _toldWaiting;

    public BridgeUpdater(RunLog log, Action<string> notify)
    {
        _log = log;
        _notify = notify;
    }

    public void Start() => Task.Run(Loop);

    public void Dispose() => _stop.Cancel();

    private async Task Loop()
    {
        try
        {
            for (int tries = 0; tries < 240 && !_stop.IsCancellationRequested; tries++) // 30초 × 240 = 2시간까지 기다린다
            {
                var r = TryOnce();
                if (r != Outcome.Locked) return;
                await Task.Delay(TimeSpan.FromSeconds(30), _stop.Token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _log.Write("bridge_update_failed", ("reason", ex.GetType().Name));
        }
    }

    private enum Outcome { Nothing, Updated, Locked }

    private Outcome TryOnce()
    {
        string addIns = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "AddIns");
        string installed = Path.Combine(addIns, Xlam);
        var bundled = FindBundled(out string? tempDir);
        try
        {
            if (bundled == null) return Outcome.Nothing;
            string? instVer = BridgeUpdatePlan.ReadManifestVersion(ReadText(Path.Combine(addIns, Side)));
            string? bunVer = bundled.Value.Side == null ? null : BridgeUpdatePlan.ReadManifestVersion(ReadText(bundled.Value.Side));
            var plan = BridgeUpdatePlan.Decide(File.Exists(installed), instVer, HashOf(installed), bunVer, HashOf(bundled.Value.Xlam));
            if (plan != BridgeAction.Update)
            {
                _log.Write("bridge_check", ("plan", plan.ToString()), ("installed", instVer ?? "?"), ("bundled", bunVer ?? "?"));
                return Outcome.Nothing;
            }
            try
            {
                Replace(bundled.Value.Xlam, bundled.Value.Side, addIns);
            }
            catch (IOException)
            {
                // 엑셀이 다리를 열어 두고 있다: 건드리지 않고 나중에 다시 한다.
                _log.Write("bridge_update_waiting", ("bundled", bunVer ?? "?"));
                if (!_toldWaiting)
                {
                    _toldWaiting = true;
                    _notify($"다리 새 판({bunVer ?? "새 내용"})은 엑셀을 모두 닫으면 자동으로 바뀝니다.");
                }
                return Outcome.Locked;
            }
            _log.Write("bridge_updated", ("from", instVer ?? "?"), ("to", bunVer ?? "?"));
            _notify($"다리를 새 판({bunVer ?? "새 내용"})으로 바꿨습니다. 엑셀을 다시 열면 적용됩니다.");
            return Outcome.Updated;
        }
        finally
        {
            if (tempDir != null) { try { Directory.Delete(tempDir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }
    }

    private static (string Xlam, string? Side)? FindBundled(out string? tempDir)
    {
        tempDir = null;
        string baseDir = AppContext.BaseDirectory;
        string zip = Path.GetFullPath(Path.Combine(baseDir, "..", "addins", "clockout-hunsu.zip"));
        if (File.Exists(zip))
        {
            tempDir = Path.Combine(Path.GetTempPath(), "hunsu-bridge-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tempDir);
            using var za = ZipFile.OpenRead(zip);
            foreach (var e in za.Entries)
            {
                string name = e.Name;
                if (name != Xlam && name != Side) continue; // 폴더 경로는 쓰지 않는다(경로 탈출 방지)
                e.ExtractToFile(Path.Combine(tempDir, name), true);
            }
            string x = Path.Combine(tempDir, Xlam), s = Path.Combine(tempDir, Side);
            return File.Exists(x) ? (x, File.Exists(s) ? s : null) : null;
        }
        string loose = Path.GetFullPath(Path.Combine(baseDir, "..", Xlam));
        return File.Exists(loose) ? (loose, null) : null;
    }

    /// <summary>같은 폴더에 임시 이름으로 복사해 내용을 확인한 뒤, 옛 파일을 백업으로 옮기고 새 파일을 제 이름으로 놓는다. 어느 단계든 실패하면 옛 파일을 되돌린다(칼퇴 AddinInstaller와 같은 순서).</summary>
    private static void Replace(string xlam, string? side, string addIns)
    {
        Directory.CreateDirectory(addIns);
        string dest = Path.Combine(addIns, Xlam), destSide = Path.Combine(addIns, Side);
        string tmp = dest + ".hunsu-" + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        string backup = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClockOut", "Hunsu", "backup", "bridge", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        string? movedAddin = null;
        try
        {
            File.Copy(xlam, tmp, true);
            if (HashOf(tmp) != HashOf(xlam)) throw new IOException("복사한 다리의 내용이 원본과 다릅니다.");
            if (File.Exists(dest))
            {
                Directory.CreateDirectory(backup);
                movedAddin = Path.Combine(backup, Xlam);
                File.Move(dest, movedAddin, true); // 엑셀이 잡고 있으면 여기서 IOException
            }
            File.Move(tmp, dest, true);
            if (side != null) File.Copy(side, destSide, true);
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            if (movedAddin != null && !File.Exists(dest)) { try { File.Move(movedAddin, dest, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
            throw;
        }
    }

    private static string? ReadText(string path)
    {
        try { return File.Exists(path) ? File.ReadAllText(path) : null; }
        catch (IOException) { return null; }
    }

    private static string? HashOf(string path)
    {
        try { using var s = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); return Convert.ToHexString(SHA256.HashData(s)); }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }
}
