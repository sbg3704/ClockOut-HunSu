// 기능: 한 수(hunsu-hansu, docs/22) 창. 하고 싶은 일로 함수를 찾고(위), 고른 함수의 빈칸을 채워(HansuWindow.Slots.cs) 수식을 완성해 넣을 칸에 넣게 한다.
//       독·사이드바 패널은 키보드를 받지 않는 창이라, 글을 치는 이 창만 따로 띄운다. 엑셀에서 범위를 끄는 동안에도 맨 위에 떠 있다(엑셀의 함수 인수 창처럼).
// 사용법: var w = new HansuWindow(catalog, recent); w.SetTarget(book, sheet, addr); w.Show();
//         선택이 바뀔 때마다 w.OnSelection(book, sheet, addr)  /  [넣기] → InsertRequested(sheet, addr, formula, 함수 이름)
// 의존: Core/Formula(FunctionCatalog, FunctionSearch, FormulaDraft, FormulaRef), Shell/ManagerParts(Chip), Ui Theme 이름. 엑셀을 직접 부르지 않는다(넣기는 ShellController의 쓰기 흐름).
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClockOut.Hunsu.Core.Formula;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Shell;

internal sealed partial class HansuWindow : Window
{
    private const int MaxResults = 6;
    private readonly FunctionCatalog _catalog;
    private readonly List<string> _recent;
    private readonly TextBox _search = new();
    private readonly StackPanel _results = new();
    private readonly StackPanel _builder = new() { Visibility = Visibility.Collapsed };
    private readonly TextBlock _targetText = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private (string Book, string Sheet, string Addr) _target = ("", "", "");
    private (string Book, string Sheet, string Addr) _sel = ("", "", "");

    public HansuWindow(FunctionCatalog catalog, List<string> recent)
    {
        _catalog = catalog;
        _recent = recent;
        Title = "한 수 · 훈수꾼";
        Width = 380;
        SizeToContent = SizeToContent.Height;
        MinWidth = 320;
        ResizeMode = ResizeMode.CanResize;
        Topmost = true;
        ShowInTaskbar = false;
        WindowStyle = WindowStyle.ToolWindow;
        WindowStartupLocation = WindowStartupLocation.Manual;
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 48;
        Top = area.Top + 120;
        SetResourceReference(BackgroundProperty, "Hunsu.Surface");
        SetResourceReference(ForegroundProperty, ThemeKeys.Text);
        SetResourceReference(FontFamilyProperty, "Hunsu.Font");
        FontSize = (double)FindResource("Hunsu.FontBody");
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

        var root = new StackPanel { Margin = new Thickness(18, 14, 18, 16) };
        root.Children.Add(TargetRow());
        _search.Padding = new Thickness(8, 6, 8, 6);
        _search.Margin = new Thickness(0, 10, 0, 6);
        _search.ToolTip = "하고 싶은 일을 쳐 보세요. 예: 조건 합, 찾아서 가져오기, ㅈㄱㅎ, vlookup";
        _search.TextChanged += (_, _) => ShowResults();
        _search.PreviewKeyDown += OnSearchKey;
        root.Children.Add(Hint("하고 싶은 일 (예: 조건 합, ㅈㄱㅎ, vlookup)"));
        root.Children.Add(_search);
        root.Children.Add(_results);
        root.Children.Add(_builder);
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = area.Height - 160 };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { if (_draft != null) BackToSearch(); else Close(); } };
        Loaded += (_, _) => { _search.Focus(); ShowResults(); };
    }

    /// <summary>[넣기]를 눌렀다: 넣을 칸의 시트, 주소, 수식, 함수 이름.</summary>
    public event Action<string, string, string, string>? InsertRequested;

    public void SetTarget(string book, string sheet, string addr)
    {
        _target = (book, sheet, FirstCell(addr));
        _sel = (book, sheet, addr);
        _targetText.Text = _target.Addr.Length == 0 ? "넣을 칸: (엑셀에서 칸을 고르세요)" : $"넣을 칸: {FormulaRef.QuoteSheet(_target.Sheet)}!{_target.Addr}";
        RefreshPreview();
    }

    /// <summary>엑셀 선택이 바뀌었다. 듣는 빈칸이 있으면 그 빈칸에 주소를 넣는다.</summary>
    public void OnSelection(string book, string sheet, string addr)
    {
        if (addr.Length == 0) return;
        _sel = (book, sheet, addr);
        if (_target.Addr.Length == 0) SetTarget(book, sheet, addr);
        FillListeningSlot();
    }

    private FrameworkElement TargetRow()
    {
        var row = new DockPanel();
        var pick = ManagerParts.Chip(this, "지금 칸으로", false, () => { if (_sel.Addr.Length > 0) SetTarget(_sel.Book, _sel.Sheet, _sel.Addr); });
        pick.ToolTip = "엑셀에서 지금 고른 칸을 수식 넣을 칸으로 바꿉니다";
        DockPanel.SetDock(pick, Dock.Right);
        row.Children.Add(pick);
        _targetText.FontWeight = FontWeights.SemiBold;
        row.Children.Add(_targetText);
        return row;
    }

    private void ShowResults()
    {
        _results.Children.Clear();
        var found = FunctionSearch.Search(_catalog, _search.Text, _recent, MaxResults);
        if (found.Count == 0)
        {
            _results.Children.Add(Hint(_catalog.Functions.Count == 0 ? "함수 사전을 읽지 못했습니다." : "맞는 함수가 없어요. 다른 말로 쳐 보세요(예: 합계, 개수, 찾기)."));
            return;
        }
        if (_search.Text.Trim().Length == 0) _results.Children.Add(Hint(_recent.Count > 0 ? "최근·자주 쓰는 함수" : "자주 쓰는 함수"));
        foreach (var f in found) _results.Children.Add(ResultRow(f));
    }

    private Border ResultRow(FunctionDef f)
    {
        var name = new TextBlock { Text = f.Name, FontWeight = FontWeights.SemiBold, FontFamily = (FontFamily)FindResource("Hunsu.FontNumber") };
        var head = new StackPanel { Orientation = Orientation.Horizontal };
        head.Children.Add(name);
        if (f.Needs is "365" or "2019") head.Children.Add(new TextBlock { Text = "  " + f.Needs, FontSize = (double)FindResource("Hunsu.FontCaption"), Foreground = (Brush)FindResource(ThemeKeys.Muted), VerticalAlignment = VerticalAlignment.Center });
        var body = new StackPanel();
        body.Children.Add(head);
        body.Children.Add(new TextBlock { Text = f.Say, TextWrapping = TextWrapping.Wrap, FontSize = (double)FindResource("Hunsu.FontSmall") });
        if (f.Example.Length > 0) body.Children.Add(new TextBlock { Text = f.Example, FontSize = (double)FindResource("Hunsu.FontCaption"), Foreground = (Brush)FindResource(ThemeKeys.Muted), TextTrimming = TextTrimming.CharacterEllipsis });
        var row = new Border
        {
            Child = body,
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 0, 4),
            CornerRadius = (CornerRadius)FindResource("Hunsu.RowRadius"),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Tag = f,
        };
        Brush hover = (Brush)FindResource("Hunsu.GlassHover");
        row.MouseEnter += (_, _) => row.Background = hover;
        row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
        row.MouseLeftButtonUp += (_, e) => { e.Handled = true; Choose(f); };
        return row;
    }

    private void OnSearchKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (_results.Children.OfType<Border>().FirstOrDefault(b => b.Tag is FunctionDef) is { Tag: FunctionDef f }) Choose(f);
        e.Handled = true;
    }

    private void BackToSearch()
    {
        _draft = null;
        _listen = -1;
        _builder.Visibility = Visibility.Collapsed;
        _search.Visibility = Visibility.Visible;
        _results.Visibility = Visibility.Visible;
        ShowResults();
        _search.Focus();
        _search.SelectAll();
    }

    private TextBlock Hint(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(2, 4, 0, 4),
        FontSize = (double)FindResource("Hunsu.FontCaption"),
        Foreground = (Brush)FindResource(ThemeKeys.Muted),
    };

    /// <summary>범위의 왼쪽 위 칸(넣을 칸은 한 칸).</summary>
    private static string FirstCell(string addr)
    {
        string a = addr.Split(',')[0].Split(':')[0].Trim();
        return a.Replace("$", "");
    }
}
