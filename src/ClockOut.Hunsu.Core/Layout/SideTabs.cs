// 기능: 사이드바의 묶음 탭 계산(docs/19). 모듈을 묶음(탭)으로 나누고, 사용자가 끌어 놓은 결과(모듈 옮기기, 탭 순서, 탭 합치기, 새 묶음)를 만든다. 화면 없이 시험된다.
// 사용법: var tabs = SideTabs.Build(modules, layout.SideTabs);              // 저장된 것이 없으면 기능 묶음(order 범위)대로
//         tabs = SideTabs.MoveModule(tabs, "hunsu-colorsum", "check", before: null);  // 끌어 놓기 한 번
//         layout.SideTabs = SideTabs.Persist(layout.SideTabs, tabs);         // 지금 안 보이는(꺼진) 모듈의 자리는 지킨 채 저장
// 의존: LauncherGroups.Ranges(기본 묶음). 화면이나 WPF를 모른다.
namespace ClockOut.Hunsu.Core.Layout;

/// <summary>사이드바에 넣을 모듈 하나: id, 이름, 순서(기본 묶음을 정함), 아이콘 뜻 이름.</summary>
public sealed record SideTabModule(string Id, string Name, int Order, string Icon);

/// <summary>탭(묶음) 하나. Members는 위에서 아래 순서의 모듈 id.</summary>
public sealed record SideTab(string Key, string Name, string Icon, IReadOnlyList<string> Members);

/// <summary>저장되는 탭 한 칸(layout.json의 sideTabs).</summary>
public sealed record SideTabState
{
    public string Key { get; init; } = "";
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";
    public List<string> Members { get; init; } = [];
}

public static class SideTabs
{
    /// <summary>사용자가 만든 묶음 열쇠의 머리.</summary>
    public const string CustomPrefix = "my-";

    /// <summary>모듈의 기본 묶음(order 범위).</summary>
    public static (string Key, string Name, string Icon) DefaultGroup(int order)
    {
        foreach (var r in LauncherGroups.Ranges)
            if (order >= r.Lo && order <= r.Hi) return (r.Key, r.Name, r.Icon);
        var last = LauncherGroups.Ranges[^1];
        return (last.Key, last.Name, last.Icon);
    }

    /// <summary>탭 목록. 저장된 것(saved)이 있으면 그 순서·구성을 따르고, 거기에 없는 모듈은 기본 묶음 탭에(없으면 새로 만들어 뒤에) 넣는다. 빈 탭은 뺀다.</summary>
    public static IReadOnlyList<SideTab> Build(IEnumerable<SideTabModule> modules, IReadOnlyList<SideTabState>? saved)
    {
        var list = modules.OrderBy(m => m.Order).ThenBy(m => m.Id, StringComparer.Ordinal).ToList();
        var byId = list.ToDictionary(m => m.Id, StringComparer.Ordinal);
        var tabs = new List<(string Key, string Name, string Icon, List<string> Members)>();
        var placed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in saved ?? [])
        {
            if (string.IsNullOrWhiteSpace(s.Key) || tabs.Any(t => t.Key == s.Key)) continue;
            var members = (s.Members ?? []).Where(id => byId.ContainsKey(id) && placed.Add(id)).ToList();
            tabs.Add((s.Key, s.Name ?? "", s.Icon ?? "", members));
        }
        foreach (var m in list)
        {
            if (placed.Contains(m.Id)) continue;
            var g = DefaultGroup(m.Order);
            int i = tabs.FindIndex(t => t.Key == g.Key);
            if (i < 0)
            {
                tabs.Add((g.Key, g.Name, g.Icon, []));
                i = tabs.Count - 1;
            }
            tabs[i].Members.Add(m.Id);
            placed.Add(m.Id);
        }
        return tabs.Where(t => t.Members.Count > 0).Select(t => new SideTab(t.Key, NameOf(t.Key, t.Name), IconOf(t.Key, t.Icon, t.Members, byId), t.Members)).ToList();
    }

    private static string NameOf(string key, string name)
    {
        if (!string.IsNullOrWhiteSpace(name)) return name;
        var r = LauncherGroups.Ranges.FirstOrDefault(x => x.Key == key);
        return r.Key == key && r.Name != null ? r.Name : "내 묶음";
    }

    /// <summary>모듈이 하나뿐인 탭은 그 모듈의 아이콘, 여럿이면 묶음 아이콘(정한 것, 없으면 첫 모듈의 기본 묶음 아이콘).</summary>
    private static string IconOf(string key, string icon, List<string> members, Dictionary<string, SideTabModule> byId)
    {
        if (members.Count == 1) return byId[members[0]].Icon;
        if (!string.IsNullOrWhiteSpace(icon)) return icon;
        var r = LauncherGroups.Ranges.FirstOrDefault(x => x.Key == key);
        return r.Key == key && r.Icon != null ? r.Icon : DefaultGroup(byId[members[0]].Order).Icon;
    }

    /// <summary>모듈을 다른 탭(같은 탭도 됨)으로 옮긴다. before가 있으면 그 모듈 바로 위, 없으면 맨 아래.</summary>
    public static IReadOnlyList<SideTab> MoveModule(IReadOnlyList<SideTab> tabs, string id, string targetKey, string? before)
    {
        if (id == before || tabs.All(t => t.Key != targetKey) || tabs.All(t => !t.Members.Contains(id))) return tabs;
        var work = tabs.Select(t => (Tab: t, Members: t.Members.Where(x => x != id).ToList())).ToList();
        var target = work.First(w => w.Tab.Key == targetKey).Members;
        int at = before == null ? -1 : target.IndexOf(before);
        if (at < 0) target.Add(id); else target.Insert(at, id);
        return work.Where(w => w.Members.Count > 0).Select(w => w.Tab with { Members = w.Members }).ToList();
    }

    /// <summary>탭 순서를 바꾼다. before가 있으면 그 탭 바로 앞, 없으면 맨 뒤.</summary>
    public static IReadOnlyList<SideTab> MoveTab(IReadOnlyList<SideTab> tabs, string key, string? before)
    {
        var tab = tabs.FirstOrDefault(t => t.Key == key);
        if (tab == null || key == before) return tabs;
        var rest = tabs.Where(t => t.Key != key).ToList();
        int at = before == null ? -1 : rest.FindIndex(t => t.Key == before);
        if (at < 0) rest.Add(tab); else rest.Insert(at, tab);
        return rest;
    }

    /// <summary>탭 하나를 다른 탭에 통째로 넣는다(그 탭 맨 아래, before가 있으면 그 모듈 위). 넣은 탭은 사라진다.</summary>
    public static IReadOnlyList<SideTab> MergeTab(IReadOnlyList<SideTab> tabs, string key, string intoKey, string? before = null)
    {
        var from = tabs.FirstOrDefault(t => t.Key == key);
        var into = tabs.FirstOrDefault(t => t.Key == intoKey);
        if (from == null || into == null || key == intoKey) return tabs;
        var members = into.Members.ToList();
        int at = before == null ? -1 : members.IndexOf(before);
        if (at < 0) members.AddRange(from.Members); else members.InsertRange(at, from.Members);
        return tabs.Where(t => t.Key != key).Select(t => t.Key == intoKey ? t with { Members = members } : t).ToList();
    }

    /// <summary>모듈 하나를 빼서 새 묶음(탭)으로. before가 있으면 그 탭 앞, 없으면 맨 뒤. 이미 혼자인 탭이면 순서만 옮긴다.</summary>
    public static IReadOnlyList<SideTab> NewTab(IReadOnlyList<SideTab> tabs, string id, string? before = null)
    {
        var src = tabs.FirstOrDefault(t => t.Members.Contains(id));
        if (src == null) return tabs;
        if (src.Members.Count == 1) return MoveTab(tabs, src.Key, before);
        int n = 1;
        while (tabs.Any(t => t.Key == CustomPrefix + n)) n++;
        var fresh = new SideTab(CustomPrefix + n, "내 묶음 " + n, src.Icon, [id]);
        var rest = tabs.Select(t => t with { Members = t.Members.Where(x => x != id).ToList() }).Where(t => t.Members.Count > 0).ToList();
        int at = before == null ? -1 : rest.FindIndex(t => t.Key == before);
        if (at < 0) rest.Add(fresh); else rest.Insert(at, fresh);
        return rest;
    }

    /// <summary>모듈이 든 탭의 열쇠(없으면 null).</summary>
    public static string? TabOf(IReadOnlyList<SideTab> tabs, string id) => tabs.FirstOrDefault(t => t.Members.Contains(id))?.Key;

    /// <summary>저장할 값. 지금 보이는 탭(tabs)을 그대로 적되, 예전 저장(old)에 있던 안 보이는 모듈(꺼졌거나 떼어 낸 것)은 원래 탭이 남아 있으면 그 탭 끝에 두어 다시 켜도 같은 자리로 온다.</summary>
    public static List<SideTabState> Persist(IReadOnlyList<SideTabState>? old, IReadOnlyList<SideTab> tabs)
    {
        var visible = new HashSet<string>(tabs.SelectMany(t => t.Members), StringComparer.Ordinal);
        var result = tabs.Select(t => new SideTabState { Key = t.Key, Name = t.Name, Icon = t.Key.StartsWith(CustomPrefix, StringComparison.Ordinal) ? t.Icon : "", Members = t.Members.ToList() }).ToList();
        foreach (var o in old ?? [])
        {
            var keep = result.FirstOrDefault(r => r.Key == o.Key);
            if (keep == null) continue;
            foreach (var id in o.Members ?? [])
                if (!visible.Contains(id) && !keep.Members.Contains(id)) keep.Members.Add(id);
        }
        return result;
    }
}
