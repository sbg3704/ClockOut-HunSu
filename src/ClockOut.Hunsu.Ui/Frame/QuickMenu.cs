// 기능: 옵션 선언표(QuickSetting)를 우클릭 메뉴로 그리는 공용 부품. 순서·묶음(작은 제목과 구분선)·하위 메뉴·고르기(체크 표시)를 모듈 코드 없이 만든다.
// 사용법: QuickMenu.Fill(menu, schema, key => 현재값글, (key, 값) => 저장); 메뉴가 열릴 때마다 부르면 된다(맨 위의 이전 항목을 지우고 새로 넣는다).
// 의존: Core/Manifest/QuickSetting(배치·값 해석). 메뉴 그림 모양은 WPF 기본 메뉴 스타일을 따른다.
using System.Windows.Controls;
using ClockOut.Hunsu.Core.Manifest;

namespace ClockOut.Hunsu.Ui.Frame;

public static class QuickMenu
{
    public const string Tag = "hunsu-quick";

    public static void Fill(ContextMenu menu, IReadOnlyList<QuickSetting> schema, Func<string, string> current, Action<string, object> apply)
    {
        for (int i = menu.Items.Count - 1; i >= 0; i--) if (menu.Items[i] is Control c && Equals(c.Tag, Tag)) menu.Items.RemoveAt(i);
        if (schema.Count == 0) return;
        int at = 0;
        foreach (var item in Build(schema, current, apply, top: true)) menu.Items.Insert(at++, item);
        menu.Items.Insert(at, new Separator { Tag = Tag });
    }

    /// <summary>항목을 메뉴 줄로: 묶음마다 작은 제목(이름이 있을 때)과 구분선.</summary>
    public static IEnumerable<Control> Build(IReadOnlyList<QuickSetting> schema, Func<string, string> current, Action<string, object> apply, bool top)
    {
        bool first = true;
        foreach (var g in QuickSchema.Arrange(schema))
        {
            if (!first) yield return Mark(new Separator(), top);
            first = false;
            if (g.Name.Length > 0) yield return Mark(new MenuItem { Header = g.Name, IsEnabled = false, FontSize = 11 }, top);
            foreach (var q in g.Items) yield return Mark(Item(q, current, apply), top);
        }
    }

    private static MenuItem Item(QuickSetting q, Func<string, string> current, Action<string, object> apply)
    {
        if (q.Kind == QuickSetting.Toggle)
        {
            bool on = QuickSettings.IsOn(current(q.Key));
            var it = new MenuItem { Header = q.Label, IsCheckable = true, IsChecked = on };
            it.Click += (_, _) => apply(q.Key, !on);
            return it;
        }
        var sub = new MenuItem { Header = q.Label };
        if (q.Kind == QuickSetting.Menu)
        {
            foreach (var c in Build(q.Children, current, apply, top: false)) sub.Items.Add(c);
            return sub;
        }
        string cur = current(q.Key);
        foreach (var o in q.Options)
        {
            var oi = new MenuItem { Header = o.Label, IsCheckable = true, IsChecked = QuickSettings.IsCurrent(cur, o) };
            var val = QuickSettings.Typed(o.Value);
            oi.Click += (_, _) => apply(q.Key, val);
            sub.Items.Add(oi);
        }
        return sub;
    }

    private static T Mark<T>(T c, bool top) where T : Control { if (top) c.Tag = Tag; return c; }
}
