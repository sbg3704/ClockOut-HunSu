// 기능: 모듈 하나의 화면 묶음. 데이터(DataScope), 부품(ViewRenderer), 패널 틀(PanelFrame), 손잡이(DockTab), 도크 칸(Slot)과 지금 배치를 갖는다.
// 사용법: var mv = new ModuleView(loaded, placement); dock.Add(mv); mv.SetData(topic, data); mv.Expand(true, done => ...)
//   패널 틀은 한 번에 한 곳(도크 칸 또는 핀 창)에만 들어간다: DetachFrame()으로 빼서 옮긴다.
// 의존: Ui(Frame, Render), Core(View, Layout). 엑셀이나 파일을 모른다(배치 저장은 ShellController).
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Core.View;
using ClockOut.Hunsu.Ui.Frame;
using ClockOut.Hunsu.Ui.Render;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Shell;

internal sealed class ModuleView
{
    private readonly DataScope _scope = new();
    private readonly ViewRenderer _renderer;
    private readonly Grid _slot = new();

    public ModuleView(LoadedModule module, EffectivePlacement placement)
    {
        Module = module;
        Placement = placement;
        _renderer = new ViewRenderer(module.View);
        _renderer.CommandRequested += (command, args) => CommandRequested?.Invoke(command, args);
        Frame = new PanelFrame { Title = module.Manifest.Name, Body = _renderer.Root };
        Frame.SetOpacityLevel(placement.Opacity);
        Tab = new DockTab { HorizontalAlignment = HorizontalAlignment.Right };
        Frame.SizeChanged += (_, e) => { if (e.NewSize.Height > 0) LastPanelHeight = e.NewSize.Height; };
        _slot.Children.Add(Tab);
        _slot.Children.Add(Frame);
        Refresh();
        ApplyCollapsed(placement.Collapsed, animate: false, done: null);
    }

    /// <summary>단추나 목록 줄을 눌렀다(명령 이름, 인자 JSON). 허용 여부는 ShellController가 확인한다.</summary>
    public event Action<string, string>? CommandRequested;

    public LoadedModule Module { get; }
    public string Id => Module.Manifest.Id;
    public EffectivePlacement Placement { get; set; }
    public PanelFrame Frame { get; }
    public DockTab Tab { get; }

    /// <summary>도크에 넣을 칸(손잡이와 패널 틀을 겹쳐 두고 하나만 보인다).</summary>
    public FrameworkElement Slot => _slot;

    public bool InDock => Places.IsDock(Placement.Place);

    /// <summary>마지막으로 잰 펼친 패널 높이(접힌 동안은 잴 수 없어 기억해 둔다). 처음에는 어림값.</summary>
    public double LastPanelHeight { get; private set; } = 260;

    /// <summary>공간이 모자라 틀이 접은 상태(사용자가 접은 것과 따로).</summary>
    public bool ForcedCollapsed { get; private set; }

    public bool ShowsPanel => InDock && !Placement.Collapsed && !ForcedCollapsed;

    public bool Wants(string topic) => Module.Manifest.Data.Any(d => d.Topic == topic);

    public void SetData(string topic, JsonElement data)
    {
        _scope.Set(topic, data);
        Refresh();
    }

    /// <summary>엑셀이 바쁘거나 조회가 실패하면 마지막 내용을 흐리게 둔다(docs/04 6장).</summary>
    public void SetStale(bool stale) => _renderer.Root.Opacity = stale ? 0.5 : 1.0;

    private void Refresh()
    {
        _renderer.Update(_scope);
        var (title, badge, tone) = _renderer.TabText(_scope, Module.Manifest.Name);
        Tab.SetContent(title, badge, tone);
    }

    public void SetForcedCollapsed(bool forced)
    {
        if (ForcedCollapsed == forced) return;
        ForcedCollapsed = forced;
        ApplyCollapsed(Placement.Collapsed || forced, animate: false, done: null);
    }

    /// <summary>펼치기·접기. 펼칠 때는 fast 120ms 동안 투명도와 위치만 움직인다(docs/03 6장). done은 움직임이 끝난 뒤 첫 프레임 시각.</summary>
    public void ApplyCollapsed(bool collapsed, bool animate, Action<long>? done)
    {
        if (!_slot.Children.Contains(Frame)) { done?.Invoke(0); return; }
        Tab.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;
        Frame.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        if (collapsed || !animate)
        {
            Frame.BeginAnimation(UIElement.OpacityProperty, null);
            Frame.RenderTransform = Transform.Identity;
            if (!collapsed) Frame.SetOpacityLevel(Frame.OpacityLevel);
            if (done != null) Diagnostics.Frames.Next(done);
            return;
        }
        var dur = ThemeKeys.Get<Duration>(Frame, ThemeKeys.Fast);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var shift = new TranslateTransform(0, 6);
        Frame.RenderTransform = shift;
        var fade = new DoubleAnimation(0, Core.Layout.OpacityLevels.RestOpacity(Frame.OpacityLevel), dur) { EasingFunction = ease };
        fade.Completed += (_, _) => { if (done != null) Diagnostics.Frames.Next(done); };
        Frame.BeginAnimation(UIElement.OpacityProperty, fade);
        shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, dur) { EasingFunction = ease });
    }

    /// <summary>패널 틀을 도크 칸에서 뺀다(핀 창으로 옮기기 전에).</summary>
    public PanelFrame DetachFrame()
    {
        if (Frame.Parent is Panel p) p.Children.Remove(Frame);
        else if (Frame.Parent is Decorator d) d.Child = null;
        Frame.Visibility = Visibility.Visible;
        Frame.BeginAnimation(UIElement.OpacityProperty, null);
        Frame.RenderTransform = Transform.Identity;
        return Frame;
    }

    /// <summary>패널 틀을 도크 칸으로 되돌린다.</summary>
    public void AttachFrameToSlot()
    {
        if (Frame.Parent != null && Frame.Parent != _slot) DetachFrame();
        if (!_slot.Children.Contains(Frame)) _slot.Children.Add(Frame);
        Frame.IsPinned = false;
        ApplyCollapsed(Placement.Collapsed || ForcedCollapsed, animate: false, done: null);
    }
}
