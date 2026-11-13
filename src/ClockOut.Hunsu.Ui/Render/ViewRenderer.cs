// 기능: 화면 선언(ViewSpec)의 panel 부품 목록을 WPF 부품으로 만들고, 데이터가 오면 글 틀과 조건(hideWhen/showWhen)을 다시 채운다.
// 사용법: var r = new ViewRenderer(spec); panelFrame.Body = r.Root; ... r.Update(scope);   tab 글은 r.TabText(scope)
// 의존: Core/View(ViewSpec, DataScope, Template, Condition), Controls/Parts. 모듈 코드를 실행하지 않는다(선언만 읽음).
using System.Windows;
using System.Windows.Controls;
using ClockOut.Hunsu.Core.View;
using Condition = ClockOut.Hunsu.Core.View.Condition;
using ClockOut.Hunsu.Ui.Controls;

namespace ClockOut.Hunsu.Ui.Render;

public sealed class ViewRenderer
{
    private readonly List<(ViewItem Item, FrameworkElement Element, Action<DataScope> Fill)> _parts = [];

    public ViewRenderer(ViewSpec spec)
    {
        Spec = spec;
        Root = new StackPanel();
        foreach (var item in spec.Panel)
        {
            var (element, fill) = Parts.Create(item, (command, args) => CommandRequested?.Invoke(command, args));
            _parts.Add((item, element, fill));
            Root.Children.Add(element);
        }
        if (_parts.Count == 0) Root.Children.Add(Parts.Placeholder("표시할 부품이 없습니다."));
    }

    public ViewSpec Spec { get; }

    /// <summary>단추나 목록 줄을 눌렀다: 명령 이름, 인자 JSON. 명령이 이 모듈 선언에 있는지는 받는 쪽이 확인한다.</summary>
    public event Action<string, string>? CommandRequested;

    public StackPanel Root { get; }

    /// <summary>데이터가 바뀌었을 때 부른다. 아직 데이터가 없으면 부품은 "-"를 보인다.</summary>
    public void Update(DataScope scope)
    {
        foreach (var (item, element, fill) in _parts)
        {
            bool hidden = Condition.Evaluate(item.Get("hideWhen"), scope);
            string show = item.Get("showWhen");
            if (show.Length > 0 && !Condition.Evaluate(show, scope)) hidden = true;
            element.Visibility = hidden ? Visibility.Collapsed : Visibility.Visible;
            if (!hidden) fill(scope);
        }
    }

    /// <summary>손잡이(tab)의 제목, 배지, 톤.</summary>
    public (string Title, string Badge, string Tone) TabText(DataScope scope, string fallbackTitle)
    {
        var t = Spec.Tab;
        if (t == null) return (fallbackTitle, "", "info");
        return (Template.Render(t.Get("title", fallbackTitle), scope), Template.Render(t.Get("badge"), scope), Template.Render(t.Get("tone", "info"), scope));
    }
}
