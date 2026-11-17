// 기능: 사이드바의 묶음 탭(SidebarWindow의 일부, docs/19). 활동 막대에는 묶음(탭)마다 아이콘 하나, 고른 묶음의 모듈들이 위에서 아래로 쌓이고
//       모듈마다 아래에 스플리터(가로 손잡이)가 있어 끌면 그 모듈의 본문 높이가 바뀐다(두 번 누르면 자동). 접힌 모듈은 이름 줄로 남아 누르면 펴진다.
//       쌓아 보기(sidebarTabs=false)에서는 모든 묶음이 머리글과 함께 차례로 쌓이고, 탭 아이콘을 누르면 그 묶음으로 내려간다.
// 사용법: SetModules(views, profile, tabs, id => 저장된 높이). 탭 구성이 바뀌면 TabsChanged, 높이가 바뀌면 HeightChanged, 되돌리기는 TabsReset.
// 의존: Core/Layout/SideTabs(탭 계산), Shell/ModuleView(Slot·Frame), Ui/Icons/IconFactory, Theme 이름.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Shell;
using ClockOut.Hunsu.Ui.Icons;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Overlay;

internal sealed partial class SidebarWindow
{
    private const double MinBody = 40, MaxBody = 1200;
    private List<ModuleView> _panels = [];
    private IReadOnlyList<SideTab> _tabList = [];
    private string _profile = "";
    private string? _activeKey;
    private Func<string, double> _heightOf = _ => 0;
    private readonly Dictionary<string, FrameworkElement> _tabIcons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FrameworkElement> _sections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FrameworkElement> _heads = new(StringComparer.Ordinal);

    /// <summary>끌어 놓아 묶음 구성(탭 순서·모듈 자리)이 바뀌었다. 앱이 저장하고 다시 넣는다.</summary>
    public event Action<IReadOnlyList<SideTab>>? TabsChanged;

    /// <summary>스플리터로 모듈 본문 높이를 정했다(DIP, 0 = 자동).</summary>
    public event Action<string, double>? HeightChanged;

    /// <summary>탭 아이콘 우클릭 "기본 묶음으로 되돌리기".</summary>
    public event Action? TabsReset;

    /// <summary>모듈과 묶음 탭을 지금 상태로 다시 넣는다.</summary>
    internal void SetModules(IEnumerable<ModuleView> panels, string profile, IReadOnlyList<SideTab> tabs, Func<string, double> heightOf)
    {
        foreach (var m in _panels) Detach(m.Slot); // 빠지는 모듈의 칸도 놓아 준다(도크로 돌아갈 때 부모가 남지 않게)
        _panels = panels.ToList();
        _profile = profile;
        _tabList = tabs;
        _heightOf = heightOf;
        Render();
    }

    private ModuleView? ViewOf(string id) => _panels.FirstOrDefault(m => m.Id == id);

    private static void Detach(FrameworkElement e)
    {
        if (e.Parent is Panel p) p.Children.Remove(e);
        else if (e.Parent is Decorator d) d.Child = null;
    }

    private void Render()
    {
        CancelDrag();
        var byId = _panels.ToDictionary(m => m.Id, StringComparer.Ordinal);
        var tabs = _tabList.Select(t => t with { Members = t.Members.Where(byId.ContainsKey).ToList() }).Where(t => t.Members.Count > 0).ToList();
        if (tabs.All(t => t.Key != _activeKey)) _activeKey = (tabs.FirstOrDefault(t => t.Members.Any(id => byId[id].ShowsPanel)) ?? tabs.FirstOrDefault())?.Key;
        foreach (var m in _panels) Detach(m.Slot);
        _items.Children.Clear();
        _bar.Children.Clear();
        _tabIcons.Clear();
        _sections.Clear();
        _heads.Clear();
        BuildFooter();
        foreach (var tab in tabs)
        {
            var icon = TabIcon(tab, byId);
            _tabIcons[tab.Key] = icon;
            _bar.Children.Add(icon);
            if (_tabs && tab.Key != _activeKey) continue;
            if (!_tabs || tab.Members.Count > 1) _items.Children.Add(Head(tab));
            foreach (var id in tab.Members) AddSection(byId[id]);
        }
        _bar.Children.Add(_newZone); // 모듈을 여기 놓으면 새 묶음(끄는 동안만 보임)
    }

    private void AddSection(ModuleView m)
    {
        m.UseTab = false;
        m.Tab.Visibility = Visibility.Collapsed;
        m.Slot.Margin = new Thickness(0);
        m.Slot.Visibility = Visibility.Visible;
        m.Slot.HorizontalAlignment = HorizontalAlignment.Stretch;
        m.Frame.Width = double.NaN; // 사이드바 폭에 맞춘다
        m.Frame.ApplyBodyLimit(true);
        m.Frame.SetBodyHeight(_heightOf(m.Id));
        m.Frame.SetTitleTip("Shift를 누른 채 제목을 끌어 다른 묶음으로(Ctrl+Alt+H로 부르면 그냥 끌기)");
        if (!m.ShowsPanel)
        {
            _items.Children.Add(FoldedRow(m));
            return;
        }
        var wrap = new Border { Child = m.Slot, Tag = m.Id, BorderThickness = new Thickness(0, 2, 0, 2), BorderBrush = Brushes.Transparent };
        wrap.PreviewMouseLeftButtonDown += (_, e) => { if (m.Frame.IsTitleGrab(e.OriginalSource as DependencyObject)) BeginPending("module", m.Id, wrap, e); };
        _sections[m.Id] = wrap;
        _items.Children.Add(wrap);
        _items.Children.Add(m.Frame.Folded ? new Border { Height = 4 } : Splitter(m));
    }

    /// <summary>묶음 머리글(쌓아 보기, 또는 탭에 모듈이 여럿일 때): 이름과 모듈 수. 끌어 놓기의 과녁도 된다.</summary>
    private FrameworkElement Head(SideTab tab)
    {
        var t = new TextBlock { Text = tab.Name + (tab.Members.Count > 1 ? $"  ·  {tab.Members.Count}" : ""), FontSize = 11, Margin = new Thickness(6, 4, 6, 2) };
        t.SetResourceReference(TextBlock.ForegroundProperty, ThemeKeys.Muted);
        var b = new Border { Child = t, Tag = tab.Key, Background = Brushes.Transparent, BorderThickness = new Thickness(0, 0, 0, 2), BorderBrush = Brushes.Transparent };
        _heads[tab.Key] = b;
        return b;
    }

    /// <summary>접힌 모듈: 이름 한 줄(누르면 펴기).</summary>
    private FrameworkElement FoldedRow(ModuleView m)
    {
        var t = new TextBlock { Text = "＋ " + m.Module.Manifest.Name, FontSize = 12, Margin = new Thickness(8, 3, 8, 3) };
        t.SetResourceReference(TextBlock.ForegroundProperty, ThemeKeys.Muted);
        var b = new Border { Child = t, Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.Hand, ToolTip = "누르면 펴기", CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 0, 2) };
        b.MouseEnter += (_, _) => b.SetResourceReference(Border.BackgroundProperty, "Hunsu.GlassAlt");
        b.MouseLeave += (_, _) => b.Background = Brushes.Transparent;
        string id = m.Id;
        b.MouseLeftButtonUp += (_, _) => ModuleClicked?.Invoke(id);
        return b;
    }

    /// <summary>모듈 아래 가로 손잡이: 끌면 위 모듈의 본문 높이, 두 번 누르면 자동으로. 평소엔 거의 안 보이고 마우스를 올리면 가는 선.</summary>
    private FrameworkElement Splitter(ModuleView m)
    {
        var line = new FrameworkElementFactory(typeof(Border));
        line.SetValue(Border.HeightProperty, 2.0);
        line.SetValue(Border.WidthProperty, 28.0);
        line.SetValue(Border.CornerRadiusProperty, new CornerRadius(1));
        line.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Center);
        line.SetResourceReference(Border.BackgroundProperty, "Hunsu.Line");
        var host = new FrameworkElementFactory(typeof(Border));
        host.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        host.AppendChild(line);
        var thumb = new Thumb { Height = 8, Cursor = System.Windows.Input.Cursors.SizeNS, Template = new ControlTemplate(typeof(Thumb)) { VisualTree = host }, ToolTip = "끌어 높이 조절 · 두 번 누르면 자동", Opacity = 0.35 };
        thumb.MouseEnter += (_, _) => thumb.Opacity = 1;
        thumb.MouseLeave += (_, _) => thumb.Opacity = 0.35;
        double startH = 0, startY = 0, h = 0;
        thumb.DragStarted += (_, _) =>
        {
            startH = m.Frame.BodyActualHeight;
            startY = System.Windows.Input.Mouse.GetPosition(_root).Y;
            h = startH;
        };
        thumb.DragDelta += (_, _) =>
        {
            // 손잡이가 패널과 함께 움직이므로 Thumb의 변화량 대신 창 기준 마우스 위치로 잰다.
            h = Math.Clamp(Math.Round(startH + System.Windows.Input.Mouse.GetPosition(_root).Y - startY), MinBody, MaxBody);
            m.Frame.SetBodyHeight(h);
        };
        string id = m.Id;
        thumb.DragCompleted += (_, e) => { if (!e.Canceled && Math.Abs(h - startH) >= 1) HeightChanged?.Invoke(id, h); };
        thumb.MouseDoubleClick += (_, _) => { m.Frame.SetBodyHeight(0); HeightChanged?.Invoke(id, 0); };
        return thumb;
    }

    private FrameworkElement TabIcon(SideTab tab, Dictionary<string, ModuleView> byId)
    {
        bool active = _tabs ? tab.Key == _activeKey : tab.Members.Any(id => byId[id].ShowsPanel);
        var brush = (Brush)Application.Current.FindResource(active ? "Hunsu.AccentInk" : ThemeKeys.Text);
        var names = tab.Members.Select(id => byId[id].Module.Manifest.Name).ToList();
        var grid = new Grid();
        grid.Children.Add(IconFactory.Create(_profile, tab.Icon, 18, brush, tab.Name));
        if (tab.Members.Count > 1)
        {
            // 모듈이 여럿인 묶음: 오른쪽 아래 작은 숫자
            var n = new TextBlock { Text = tab.Members.Count.ToString(), FontSize = 9, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 3, 1) };
            n.SetResourceReference(TextBlock.ForegroundProperty, active ? "Hunsu.AccentInk" : ThemeKeys.Muted);
            grid.Children.Add(n);
        }
        var b = new Border
        {
            Width = 32, Height = 32, Margin = new Thickness(4, 2, 4, 2), CornerRadius = new CornerRadius(6), Cursor = System.Windows.Input.Cursors.Hand, Tag = tab.Key,
            BorderThickness = new Thickness(2), BorderBrush = Brushes.Transparent,
            ToolTip = (names.Count > 1 ? $"{tab.Name}: {string.Join(", ", names)}" : names[0]) + "\nShift+끌기: 순서 바꾸기 · 다른 묶음 위에 놓으면 합치기",
            Child = grid,
        };
        b.SetResourceReference(Border.BackgroundProperty, active ? "Hunsu.Accent" : "Hunsu.GlassAlt");
        string key = tab.Key;
        b.PreviewMouseLeftButtonDown += (_, e) => BeginPending("tab", key, b, e);
        b.MouseLeftButtonUp += (_, _) => OnTabClick(key);
        var menu = new ContextMenu();
        var reset = new MenuItem { Header = "기본 묶음으로 되돌리기" };
        reset.Click += (_, _) => TabsReset?.Invoke();
        menu.Items.Add(reset);
        b.ContextMenu = menu;
        return b;
    }

    private void OnTabClick(string key)
    {
        var tab = _tabList.FirstOrDefault(t => t.Key == key);
        if (tab == null) return;
        if (!_tabs)
        {
            if (_heads.TryGetValue(key, out var head)) head.BringIntoView();
            return;
        }
        _activeKey = key;
        var members = tab.Members.Select(ViewOf).OfType<ModuleView>().ToList();
        _scroll.ScrollToTop();
        if (members.Count > 0 && members.All(m => !m.ShowsPanel))
        {
            foreach (var m in members) ModuleClicked?.Invoke(m.Id); // 모두 접힌 묶음: 고르면 펴진다(앱이 다시 넣는다)
            return;
        }
        Render();
    }
}
