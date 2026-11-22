// 기능: 한 수 창의 아래쪽(HansuWindow의 일부). 고른 함수의 빈칸 줄, 듣는 빈칸(누른 뒤 엑셀에서 범위를 끌면 주소가 들어감), $ 고정 돌리기, 반복 묶음 ＋/－, 완성 수식 미리 보기, [넣기]·[복사].
// 사용법: Choose(def)로 시작. 빈칸 글 상자를 누르면 그 빈칸이 "듣는 중"이 되고, 그 뒤로 바뀐 엑셀 선택만 받는다(누르기 전 선택은 무시).
// 의존: Core/Formula(FormulaDraft, FormulaRef), ManagerParts. 값(미리 계산)은 넣기 확인 창에서 다리가 보여 준다(write.formula.set 계획).
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClockOut.Hunsu.Core.Formula;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Shell;

internal sealed partial class HansuWindow
{
    private FormulaDraft? _draft;
    private int _listen = -1;
    private (string, string, string) _listenFrom;
    private int _spanStart = -1, _spanLen; // 이번 듣기에서 넣은 주소 자리(선택이 또 바뀌면 그 자리만 바꾼다, 엑셀 수식 입력과 같게)
    private readonly List<TextBox> _boxes = [];
    private readonly TextBox _preview = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, BorderThickness = new Thickness(0), Background = Brushes.Transparent };
    private readonly StackPanel _issues = new();
    private Border? _insert;

    /// <summary>들을 수 있는(엑셀 선택을 받는) 빈칸 종류. 글자·참거짓은 직접 친다.</summary>
    private static bool Listens(string kind) => kind is "range" or "value" or "any" or "criteria";

    private void Choose(FunctionDef def)
    {
        _draft = new FormulaDraft(def);
        _search.Visibility = Visibility.Collapsed;
        _results.Visibility = Visibility.Collapsed;
        _builder.Visibility = Visibility.Visible;
        BuildSlots(focusFirst: true);
    }

    private void BuildSlots(bool focusFirst)
    {
        if (_draft is not { } d) return;
        _builder.Children.Clear();
        _boxes.Clear();
        var head = new DockPanel { Margin = new Thickness(0, 8, 0, 2) };
        var back = ManagerParts.Chip(this, "← 다른 함수", false, BackToSearch);
        DockPanel.SetDock(back, Dock.Right);
        head.Children.Add(back);
        head.Children.Add(new TextBlock { Text = d.Def.Name, FontWeight = FontWeights.Bold, FontSize = (double)FindResource("Hunsu.FontTitleSize"), FontFamily = (FontFamily)FindResource("Hunsu.FontNumber"), VerticalAlignment = VerticalAlignment.Center });
        _builder.Children.Add(head);
        _builder.Children.Add(Hint(d.Def.Say + (d.Def.Example.Length > 0 ? "   예) " + d.Def.Example : "")));
        if (d.Def.Args.Any(a => Listens(a.Kind))) _builder.Children.Add(Hint("빈칸을 누른 뒤 엑셀에서 범위를 끌면 주소가 들어가요. 직접 쳐도 돼요."));
        for (int i = 0; i < d.Slots.Count; i++)
        {
            _builder.Children.Add(SlotRow(i));
            var s = d.Slots[i];
            bool lastOfGroup = s.Arg.Repeat.Length > 0 && (i + 1 == d.Slots.Count || d.Slots[i + 1].Arg.Repeat != s.Arg.Repeat);
            if (lastOfGroup) _builder.Children.Add(RepeatRow(s.Arg.Repeat));
        }
        _builder.Children.Add(PreviewBlock());
        RefreshPreview();
        if (focusFirst && _boxes.Count > 0) Dispatcher.BeginInvoke(() => _boxes[0].Focus());
    }

    private FrameworkElement SlotRow(int index)
    {
        var s = _draft!.Slots[index];
        var label = new TextBlock { Text = s.Label + (s.Arg.Optional ? " (생략 가능)" : ""), FontSize = (double)FindResource("Hunsu.FontSmall"), Margin = new Thickness(2, 8, 0, 3) };
        var box = new TextBox { Text = s.Text, Padding = new Thickness(6, 4, 6, 4) };
        box.ToolTip = KindHelp(s.Arg.Kind);
        box.TextChanged += (_, _) => { s.Text = box.Text; RefreshPreview(); };
        box.GotKeyboardFocus += (_, _) => StartListening(index);
        _boxes.Add(box);
        var row = new DockPanel();
        if (s.Arg.Kind is "range" or "value" or "any")
        {
            var abs = ManagerParts.Chip(this, "$", false, () => { box.Text = FormulaRef.CycleAbs(box.Text); });
            abs.ToolTip = "고정($) 돌리기 (F4와 같음)";
            abs.Margin = new Thickness(6, 0, 0, 0);
            DockPanel.SetDock(abs, Dock.Right);
            row.Children.Add(abs);
        }
        row.Children.Add(box);
        var wrap = new StackPanel();
        wrap.Children.Add(label);
        wrap.Children.Add(row);
        return wrap;
    }

    private FrameworkElement RepeatRow(string group)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        string what = _draft!.Def.Args.First(a => a.Repeat == group).Label;
        row.Children.Add(ManagerParts.Chip(this, "＋ " + what + " 더하기", false, () => { if (_draft.AddRepeat(group)) BuildSlots(false); }));
        if (_draft.RepeatCount(group) > 1) row.Children.Add(ManagerParts.Chip(this, "－ 마지막 빼기", false, () => { if (_draft.RemoveRepeat(group)) BuildSlots(false); }));
        return row;
    }

    private FrameworkElement PreviewBlock()
    {
        var box = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
        box.Children.Add(Hint("완성 수식"));
        _preview.FontFamily = (FontFamily)FindResource("Hunsu.FontNumber");
        _preview.SetResourceReference(ForegroundProperty, ThemeKeys.Text);
        box.Children.Add(new Border { Child = _preview, Padding = new Thickness(8, 6, 8, 6), CornerRadius = (CornerRadius)FindResource("Hunsu.RowRadius"), Background = (Brush)FindResource("Hunsu.GlassAlt") });
        box.Children.Add(_issues);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(ManagerParts.Chip(this, "복사", false, () => { try { Clipboard.SetText(_preview.Text); } catch (System.Runtime.InteropServices.ExternalException) { } }));
        _insert = ManagerParts.Chip(this, "넣기", true, Insert);
        _insert.ToolTip = "넣을 칸에 수식을 넣습니다. 넣기 전에 계산 값을 보여 주고, 넣은 뒤 되돌릴 수 있어요.";
        buttons.Children.Add(_insert);
        box.Children.Add(buttons);
        return box;
    }

    private void RefreshPreview()
    {
        if (_draft is not { } d) return;
        _preview.Text = d.Build();
        _issues.Children.Clear();
        var issues = d.Problems();
        foreach (var i in issues)
        {
            var t = Hint((i.Blocking ? "· " : "· (참고) ") + i.Text);
            t.Foreground = ThemeKeys.Tone(this, i.Blocking ? "warn" : "muted");
            _issues.Children.Add(t);
        }
        if (_target.Addr.Length == 0) _issues.Children.Add(Hint("· 엑셀에서 넣을 칸을 고른 뒤 [지금 칸으로]를 누르세요"));
        if (_insert != null) _insert.Opacity = CanInsert(issues) ? 1 : 0.45;
    }

    private bool CanInsert(IReadOnlyList<DraftIssue> issues) => _target.Addr.Length > 0 && !issues.Any(i => i.Blocking);

    private void Insert()
    {
        if (_draft is not { } d || !CanInsert(d.Problems())) return;
        _recent.Remove(d.Def.Name);
        _recent.Insert(0, d.Def.Name);
        if (_recent.Count > 8) _recent.RemoveRange(8, _recent.Count - 8);
        InsertRequested?.Invoke(_target.Sheet, _target.Addr, d.Build(), d.Def.Name);
    }

    private void StartListening(int index)
    {
        _listen = _draft != null && Listens(_draft.Slots[index].Arg.Kind) ? index : -1;
        _listenFrom = _sel;
        _spanStart = -1;
        for (int i = 0; i < _boxes.Count; i++)
            _boxes[i].BorderBrush = i == _listen ? (Brush)FindResource("Hunsu.Accent") : (Brush)FindResource("Hunsu.Line");
    }

    /// <summary>듣는 빈칸에, 듣기 시작한 뒤 바뀐 선택만 넣는다(넣을 칸 기준으로 다른 시트·책이면 앞을 붙임).</summary>
    private void FillListeningSlot()
    {
        if (_draft == null || _listen < 0 || _listen >= _boxes.Count || _sel == _listenFrom) return;
        string text = FormulaRef.Reference(_sel.Addr, _sel.Sheet, _sel.Book, _target.Sheet, _target.Book);
        if (text.Length == 0) return;
        var box = _boxes[_listen];
        string cur = box.Text;
        if (_spanStart < 0 || _spanStart + _spanLen > cur.Length)
        {
            // 범위 빈칸이나 빈 칸은 통째로, 글이 있는 식 빈칸은 커서 자리에 끼워 넣는다(D2 고르고 >=100 치기).
            bool whole = cur.Trim().Length == 0 || _draft.Slots[_listen].Arg.Kind == "range" || box.SelectionLength == cur.Length;
            _spanStart = whole ? 0 : Math.Clamp(box.CaretIndex, 0, cur.Length);
            _spanLen = whole ? cur.Length : box.SelectionLength;
        }
        box.Text = cur[.._spanStart] + text + cur[(_spanStart + _spanLen)..];
        _spanLen = text.Length;
        box.CaretIndex = _spanStart + _spanLen;
    }

    private static string KindHelp(string kind) => kind switch
    {
        "range" => "범위: 누른 뒤 엑셀에서 끌어 고르세요",
        "criteria" => "조건: 서울, >=100, <>0 처럼 치거나 조건이 적힌 칸을 고르세요(따옴표는 알아서 붙어요)",
        "text" => "글자: 그대로 치세요(따옴표는 알아서 붙어요)",
        "bool" => "참 또는 거짓",
        _ => "값·칸·식: 치거나 엑셀에서 칸을 고르세요. =로 시작하면 식을 그대로 넣어요",
    };
}
