// 기능: 모듈 폴더에서 선언(<id>.hunsu.json)과 화면 선언(view.json)을 읽는다. 틀린 모듈은 건너뛰고 이유를 기록한다(manifest_bad, view_failed).
//   읽는 곳: 묶음 기본 모듈(실행 파일 옆 modules\<id>\)과 칼퇴가 카탈로그에서 받아 둔 모듈(CLOCKOUT_MODULES_DIR\<id>\<판>\, 2026-11-22 칼퇴 docs/10 3장).
//   같은 id는 선언 판이 높은 쪽이 이긴다(Core/Manifest/ModuleSources).
// 사용법: var sources = ModuleCatalog.Sources(bundledRoots, ModuleCatalog.DownloadedRoot(), log);  ModuleCatalog.Load(sources, log, gate.Allows, skipped)
//   allow가 false인 모듈은 선언도 읽지 않는다(칼퇴에서 끈 모듈).
// 의존: Core/Manifest, Core/View, Diagnostics/RunLog. 모듈은 선언만 있고 코드를 실행하지 않는다(받은 모듈도 같다). 선언 파일 크기는 64KB까지(docs/04 8장).
using System.IO;
using ClockOut.Hunsu.Core.Manifest;
using ClockOut.Hunsu.Core.View;
using ClockOut.Hunsu.Diagnostics;

namespace ClockOut.Hunsu.Shell;

internal sealed record LoadedModule(ModuleManifest Manifest, ViewSpec View, string Dir);

internal static class ModuleCatalog
{
    private const long MaxDeclBytes = 64 * 1024;

    /// <summary>칼퇴가 넘긴 받은 모듈 폴더(환경변수). 없거나 비었으면 null.</summary>
    public static string? DownloadedRoot()
    {
        string? dir = Environment.GetEnvironmentVariable(ModuleSources.EnvVar);
        return string.IsNullOrWhiteSpace(dir) ? null : dir;
    }

    /// <summary>읽을 모듈 폴더 목록: 묶음 기본 모듈 다음에 받은 모듈(id마다 가장 높은 판 하나).</summary>
    public static List<ModuleSource> Sources(IEnumerable<string> bundledRoots, string? downloadedRoot, RunLog log)
    {
        var list = new List<ModuleSource>();
        foreach (var root in bundledRoots.Where(Directory.Exists))
            foreach (var dir in Directory.EnumerateDirectories(root))
                list.Add(new ModuleSource(Path.GetFileName(dir), dir, ModuleSources.Bundled));
        if (downloadedRoot == null) return list;
        try
        {
            if (!Directory.Exists(downloadedRoot)) { log.Write("module_dir", ("found", false)); return list; }
            var installed = Directory.EnumerateDirectories(downloadedRoot)
                .Select(d => (Path.GetFileName(d), Directory.EnumerateDirectories(d).Where(v => File.Exists(Path.Combine(v, ModuleSources.Mark))).Select(Path.GetFileName).OfType<string>()))
                .ToList();
            var picked = ModuleSources.PickDownloaded(installed);
            foreach (var (id, version) in picked) list.Add(new ModuleSource(id, Path.Combine(downloadedRoot, id, version), ModuleSources.Downloaded));
            log.Write("module_dir", ("found", true), ("modules", picked.Count));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            log.Write("module_dir", ("found", false), ("why", ex.GetType().Name));
        }
        return list;
    }

    public static IReadOnlyList<LoadedModule> Load(IEnumerable<ModuleSource> sources, RunLog log, Func<string, bool>? allow = null, ICollection<string>? skipped = null)
    {
        var found = new Dictionary<string, LoadedModule>(StringComparer.Ordinal);
        foreach (var src in sources)
        {
            string id = src.Id;
            if (allow != null && !allow(id))
            {
                if (skipped != null && !skipped.Contains(id)) skipped.Add(id);
                log.Write("module_skip", ("id", id), ("why", "칼퇴에서_꺼짐"));
                continue;
            }
            var m = TryLoad(src.Dir, id, log);
            if (m == null) continue;
            if (found.TryGetValue(id, out var old))
            {
                if (!ModuleSources.Replaces(m.Manifest.Version, old.Manifest.Version)) { log.Write("module_keep", ("id", id), ("kept", old.Manifest.Version), ("other", m.Manifest.Version)); continue; }
                log.Write("module_replace", ("id", id), ("from", src.Origin), ("version", m.Manifest.Version), ("old", old.Manifest.Version));
            }
            else log.Write("module_load", ("id", id), ("version", m.Manifest.Version), ("from", src.Origin));
            found[id] = m;
        }
        return [.. found.Values.OrderBy(m => m.Manifest.Order)];
    }

    private static LoadedModule? TryLoad(string dir, string id, RunLog log)
    {
        string declPath = Path.Combine(dir, id + ".hunsu.json"), viewPath = Path.Combine(dir, "view.json");
        if (!File.Exists(declPath)) return null;
        try
        {
            if (new FileInfo(declPath).Length > MaxDeclBytes || (File.Exists(viewPath) && new FileInfo(viewPath).Length > MaxDeclBytes))
            {
                log.Write("manifest_bad", ("id", id), ("why", "64KB_초과"));
                return null;
            }
            var (manifest, errors) = ModuleManifest.Parse(File.ReadAllText(declPath));
            if (manifest == null || manifest.Id != id)
            {
                log.Write("manifest_bad", ("id", id), ("errors", errors.Count), ("first", errors.FirstOrDefault() ?? "폴더 이름과 id가 다름"));
                return null;
            }
            var (view, viewErrors) = File.Exists(viewPath) ? ViewSpec.Parse(File.ReadAllText(viewPath)) : (new ViewSpec(null, null, []), []);
            if (view == null)
            {
                log.Write("view_failed", ("id", id), ("first", viewErrors.FirstOrDefault()));
                return null;
            }
            if (viewErrors.Count > 0) log.Write("view_warn", ("id", id), ("count", viewErrors.Count), ("first", viewErrors[0]));
            return new LoadedModule(manifest, view, dir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Write("manifest_bad", ("id", id), ("why", "읽기_실패"));
            return null;
        }
    }
}
