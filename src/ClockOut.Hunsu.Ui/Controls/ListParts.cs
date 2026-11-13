// 기능: 목록(list)과 단추(button) 부품. 모듈 선언만으로 "오류 셀 목록", "시트 이동 단추"처럼 눌러서 엑셀에 명령을 보내는 화면을 만든다. Parts의 일부.
// 사용법: view.json에서
//   {"type":"list","items":"errors.items","text":"{item.addr}","sub":"{item.label}","tone":"bad","command":"goto","arg.addr":"{item.addr}","max":20,"emptyText":"오류 없음","activeWhen":"item.active == true"}
//   {"type":"button","text":"맨 앞 시트로","command":"sheet.activate","arg.name":"Sheet1"}
//   items는 데이터 연결 경로(배열). 줄 안에서는 {item.필드}만 쓴다. 줄을 눌렀을 때 command와 arg.* 가 앱으로 전해진다(앱이 모듈 선언의 commands 목록을 확인).
// 의존: Core/View(Template, Condition, CommandArgs, DataScope), Theme. 줄 수는 min(선언 max, Parts.ListMaxRows). 글자는 말줄임으로 자른다.
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClockOut.Hunsu.Core.View;
using ClockOut.Hunsu.Ui.Theme;
using Condition = ClockOut.Hunsu.Core.View.Condition;

namespace ClockOut.Hunsu.Ui.Controls;

public static partial class Parts
{
    /// <summary>list가 한 번에 그리는 줄 수 상한(앱이 settings.json의 listMaxRows로 정한다).</summary>
    public static int ListMaxRows { get; set; } = 50;

    private static (FrameworkElement, Action<DataScope>) ListPart(ViewItem item, Action<string, string>? onCommand)
    {
        var host = new StackPanel { Margin = new Thickness(0, 2, 0, 2) };
        string itemsPath = item.Get("items");
        int declaredMax = int.TryParse(item.Get("max"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var mx) && mx > 0 ? mx : int.MaxValue;
        return (host, scope =>
        {
            host.Children.Clear();
            if (!scope.TryGet(itemsPath, out var arr) || arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0)
            {
                host.Children.Add(Placeholder(Template.Render(item.Get("emptyText", "표시할 항목이 없습니다."), scope)));
                return;
            }
            int limit = Math.Min(declaredMax, ListMaxRows), shown = 0, total = arr.GetArrayLength();
            foreach (var el in arr.EnumerateArray())
            {
                if (shown >= limit) break;
                host.Children.Add(Row(host, item, el, onCommand));
                shown++;
            }
            if (total > shown) host.Children.Add(Placeholder($"… 외 {total - shown:N0}개"));
        });
    }

    private static FrameworkElement Row(FrameworkElement host, ViewItem item, JsonElement el, Action<string, string>? onCommand)
    {
        var row = CommandArgs.RowScope(el);
        string command = item.Get("command");
        bool active = Condition.Evaluate(item.Get("activeWhen"), row);
        string tone = Template.Render(item.Get("tone"), row);
        var main = new TextBlock
        {
            Text = Template.Render(item.Get("text"), row),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = (double)host.FindResource("Hunsu.FontSmall"),
            FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = tone.Length == 0 ? (Brush)host.FindResource(ThemeKeys.Text) : ThemeKeys.Tone(host, tone),
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(main);
        string sub = Template.Render(item.Get("sub"), row);
        if (sub.Length > 0)
        {
            var subText = new TextBlock
            {
                Text = sub,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = (double)host.FindResource("Hunsu.FontCaption"),
                Foreground = (Brush)host.FindResource(ThemeKeys.Muted),
            };
            Grid.SetColumn(subText, 1);
            grid.Children.Add(subText);
        }
        Brush rest = active ? (Brush)host.FindResource("Hunsu.GlassHover") : Brushes.Transparent;
        var border = new Border
        {
            Child = grid,
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 1, 0, 1),
            MinHeight = 24,
            CornerRadius = (CornerRadius)host.FindResource("Hunsu.RowRadius"),
            Background = rest,
        };
        if (command.Length > 0 && onCommand != null) MakeClickable(border, rest, (Brush)host.FindResource("Hunsu.GlassHover"), () => onCommand(command, CommandArgs.Build(item.Props, row)));
        return border;
    }

    private static (FrameworkElement, Action<DataScope>) ButtonPart(ViewItem item, Action<string, string>? onCommand)
    {
        var text = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold };
        var border = new Border { Child = text, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 4, 0, 4), MinHeight = 28 };
        string command = item.Get("command");
        DataScope current = new();
        bool wired = false; // 도크 ↔ 핀 창을 옮기면 Loaded가 또 불리므로 한 번만 연결한다
        border.Loaded += (_, _) =>
        {
            if (wired) return;
            wired = true;
            border.CornerRadius = (CornerRadius)border.FindResource("Hunsu.RowRadius");
            text.FontSize = (double)border.FindResource("Hunsu.FontSmall");
            Brush rest = (Brush)border.FindResource("Hunsu.GlassAlt");
            border.Background = rest;
            text.Foreground = (Brush)border.FindResource(ThemeKeys.Text);
            if (command.Length > 0 && onCommand != null) MakeClickable(border, rest, (Brush)border.FindResource("Hunsu.GlassHover"), () => onCommand(command, CommandArgs.Build(item.Props, current)));
        };
        return (border, scope =>
        {
            current = scope;
            text.Text = Template.Render(item.Get("text"), scope);
        });
    }

    /// <summary>누를 수 있는 모양: 손 모양 커서, 올리면 밝아짐, 왼쪽 단추를 뗄 때 동작(드래그로 벗어나면 취소).</summary>
    private static void MakeClickable(Border border, Brush rest, Brush hover, Action act)
    {
        border.Cursor = Cursors.Hand;
        border.MouseEnter += (_, _) => border.Background = hover;
        border.MouseLeave += (_, _) => border.Background = rest;
        border.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            act();
        };
    }
}
