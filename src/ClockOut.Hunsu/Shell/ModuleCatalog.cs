// 기능: 모듈 폴더(실행 파일 옆 modules\<id>\)에서 선언(<id>.hunsu.json)과 화면 선언(view.json)을 읽는다. 틀린 모듈은 건너뛰고 이유를 기록한다(manifest_bad, view_failed).
// 사용법: var mods = ModuleCatalog.Load(dirs, log);
// 의존: Core/Manifest, Core/View, Diagnostics/RunLog. 모듈은 선언만 있고 코드를 실행하지 않는다. 선언 파일 크기는 64KB까지(docs/04 8장).
using System.IO;
using ClockOut.Hunsu.Core.Manifest;
using ClockOut.Hunsu.Core.View;
using ClockOut.Hunsu.Diagnostics;

namespace ClockOut.Hunsu.Shell;

internal sealed record LoadedModule(ModuleManifest Manifest, ViewSpec View, string Dir);

internal static class ModuleCatalog
{
    private const long MaxDeclBytes = 64 * 1024;

    public static IReadOnlyList<LoadedModule> Load(IEnumerable<string> roots, RunLog log)
    {
        var found = new Dictionary<string, LoadedModule>(StringComparer.Ordinal);
        foreach (var root in roots.Where(Directory.Exists))
        {
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                string id = Path.GetFileName(dir);
                var m = TryLoad(dir, id, log);
                if (m != null && found.TryAdd(m.Manifest.Id, m)) log.Write("module_load", ("id", m.Manifest.Id), ("version", m.Manifest.Version));
            }
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
