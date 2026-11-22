// 기능: 시작할 때 칼퇴가 켜 둔 하위 모듈 목록(환경변수 CLOCKOUT_MODULES_FILE이 가리키는 modules.enabled.json)과
//   묶음 선언(실행 파일 한 칸 위의 hunsu.app.json)을 읽어 Core/Manifest/ModuleGate를 만든다(2026-11-22, 칼퇴 docs/10 1장).
// 사용법: var gate = ModuleGateFile.Load(log);  ModuleCatalog.Load(roots, log, gate.Allows)
//   반영 시점: 시작할 때 한 번만 읽는다. 켜져 있는 동안 칼퇴에서 바꾼 선택은 훈수꾼을 껐다 켜면 반영된다(파일 감시는 하지 않음, 결정 docs/22).
// 의존: Core/Manifest/ModuleGate, Diagnostics/RunLog. 읽기 실패·너무 큰 파일·깨진 파일은 기록만 하고 선언 기본값으로 간다.
using System.IO;
using ClockOut.Hunsu.Core.Manifest;
using ClockOut.Hunsu.Diagnostics;

namespace ClockOut.Hunsu.Shell;

internal static class ModuleGateFile
{
    private const long MaxBytes = 64 * 1024;

    public static ModuleGate Load(RunLog log)
    {
        string? path = Environment.GetEnvironmentVariable(ModuleGate.EnvVar);
        string? enabled = string.IsNullOrWhiteSpace(path) ? null : ReadSmall(path, log, "enabled");
        string decl = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "hunsu.app.json"));
        string? app = ReadSmall(decl, log, "app");
        var gate = ModuleGate.Decide(enabled, app);
        log.Write("module_gate", ("source", gate.Source), ("env", !string.IsNullOrWhiteSpace(path)), ("file", enabled != null), ("decl", app != null), ("problem", gate.Problem ?? ""));
        return gate;
    }

    private static string? ReadSmall(string path, RunLog log, string what)
    {
        try
        {
            if (!File.Exists(path)) return null;
            if (new FileInfo(path).Length > MaxBytes) { log.Write("module_gate_bad", ("what", what), ("why", "64KB_초과")); return null; }
            return File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            log.Write("module_gate_bad", ("what", what), ("why", ex.GetType().Name));
            return null;
        }
    }
}
