// 기능: 엄마 패널(떼어 낸 패널 여러 개를 얇은 선으로 나눠 창 하나에 담기)의 묶기·빼기와, 핀 창을 놓을 때의 정리(ShellController의 일부).
// 사용법: 패널 제목줄·아이콘 우클릭 메뉴의 "붙은 패널을 한 틀로 묶기" → MergePins(mv): mv의 창과 자석으로 이어진 핀 창들을 맨 위 창에 모은다. "묶음에서 빼기" → SplitOut(mv): 그 패널이 원래 자리의 혼자 창으로 돌아간다.
//   저장: 담긴 패널은 ModulePlacement.Group = 주인 id(주인은 비어 있음). 다시 켤 때 PlaceAll이 주인을 먼저 놓고 나머지를 그 창에 담는다. 끄는 동안 같이 움직이기는 PinWindow.GroupPeers.
// 의존: Overlay/PinWindow(AddFrame, RemoveFrame, ResizeTo), Core/Layout(PinGroups, ModulePlacement.Group). 주인 창을 도크로 되돌리면 묶음을 먼저 푼다(주인이 바뀌면 자리 저장이 어긋나서).
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Native;
using ClockOut.Hunsu.Overlay;
using ClockOut.Hunsu.Ui.Frame;

namespace ClockOut.Hunsu.Shell;

internal sealed partial class ShellController
{
    private ModuleView? ModuleOf(PanelFrame f) => _modules.FirstOrDefault(m => ReferenceEquals(m.Frame, f));

    /// <summary>이 모듈의 핀 창에 다른 패널도 담겨 있는가(묶음의 일원인가).</summary>
    private bool InGroup(ModuleView mv) => _pins.TryGetValue(mv.Id, out var w) && w.Frames.Count > 1;

    private bool CanMerge(ModuleView mv) => _pins.ContainsKey(mv.Id) && ConnectedPins(mv.Id).Count > 0;

    /// <summary>mv의 창과 붙어 이어진 핀 창들을 맨 위 창(주인)에 모아 한 창으로. 높이는 모두 합친 만큼.</summary>
    internal void MergePins(ModuleView mv)
    {
        if (!_pins.TryGetValue(mv.Id, out var own)) return;
        var windows = new List<PinWindow> { own };
        windows.AddRange(ConnectedPins(mv.Id));
        windows = [.. windows.OrderBy(w => w.Rect.Y).ThenBy(w => w.Rect.X)];
        if (windows.Count < 2) return;
        var mother = windows[0];
        var ownerMv = ModuleOf(mother.Frames[0]);
        if (ownerMv == null) return;
        var union = mother.Rect;
        foreach (var w in windows.Skip(1))
        {
            union = PixelRect.FromLtrb(union.X, union.Y, Math.Max(union.Right, w.Rect.Right), Math.Max(union.Bottom, w.Rect.Bottom));
            foreach (var f in w.Frames)
            {
                var m = ModuleOf(f);
                if (m == null) continue;
                w.RemoveFrame(f);
                mother.AddFrame(f);
                _pins[m.Id] = mother;
                Save(m, p => p with { Group = ownerMv.Id });
            }
            w.Release();
        }
        // 맨 위 창의 너비는 그대로, 높이만 늘린다(창 모서리 손잡이로 나중에 줄일 수 있다).
        mother.ResizeTo(mother.Rect.Width, union.Bottom - mother.Rect.Y);
        Save(ownerMv, p => p with { PinRect = mother.Rect });
        _log.Write("pin_group", ("owner", ownerMv.Id), ("count", mother.Frames.Count));
        RefreshDocks();
    }

    /// <summary>묶음에서 패널 하나를 뺀다: 저장된 원래 자리의 혼자 창으로. 주인(맨 위)을 빼면 나머지도 모두 풀어 준다.</summary>
    internal void SplitOut(ModuleView mv)
    {
        if (!_pins.TryGetValue(mv.Id, out var w) || w.Frames.Count < 2) return;
        var members = ReferenceEquals(w.Frames[0], mv.Frame) ? [.. w.Frames.Skip(1)] : new List<PanelFrame> { mv.Frame };
        foreach (var f in members)
        {
            var m = ModuleOf(f);
            if (m == null) continue;
            w.RemoveFrame(f);
            _pins.Remove(m.Id);
            Save(m, p => p with { Group = null });
            Pin(m, null, restoring: true); // 묶기 전 저장된 자리(PinRect)로
        }
        RefreshDocks();
    }

    private const int DockSnapPx = 40;

    /// <summary>끄는 창이 엑셀 오른쪽 가장자리 가까이면 놓았을 때 어떻게 되는지 알려 주는 한 줄(사이드바 사용법 포함). 사이드바를 켠 동안은 안내하지 않는다.</summary>
    private string? DockDragHint(PixelRect rect)
    {
        if (_target == IntPtr.Zero || SidebarOn) return null;
        var ex = User32.VisibleBounds(_target);
        bool near = Math.Abs(rect.Right - ex.Right) <= DockSnapPx * 2 && rect.Bottom > ex.Y && rect.Y < ex.Bottom;
        return near ? "놓으면 오른쪽 패널 열에 붙어요 · 엑셀 밖에 붙이려면 훈 원 우클릭 → 사이드바" : null;
    }

    /// <summary>떼어 낸 창을 엑셀 창 오른쪽 가장자리 가까이에 놓았으면 사이드 패널(오른쪽 도크)로 붙인다(자석). 묶음이면 담긴 패널 모두. true면 이 창은 곧 닫힌다.</summary>
    private bool TryDockDrop(ModuleView mv, PixelRect rect)
    {
        if (_target == IntPtr.Zero || !_pins.TryGetValue(mv.Id, out var w)) return false;
        var ex = User32.VisibleBounds(_target);
        bool nearEdge = Math.Abs(rect.Right - ex.Right) <= DockSnapPx && rect.Bottom > ex.Y && rect.Y < ex.Bottom;
        if (!nearEdge) return false;
        var members = w.Frames.Select(ModuleOf).OfType<ModuleView>().ToList();
        // 끌기 처리 중에 창을 닫지 않도록 한 박자 뒤에 한다.
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            foreach (var m in members) MoveToDock(m, Places.EdgeRight);
            _log.Write("dock_snap", ("count", members.Count));
        }, System.Windows.Threading.DispatcherPriority.Background);
        return true;
    }

    /// <summary>핀 창에서 mv의 패널을 거둔다(도크로 되돌리기·편집 취소). 묶음이면: 주인이면 나머지를 먼저 풀고 창을 닫고, 담긴 패널이면 그것만 빼고 창은 둔다.</summary>
    private void ReleasePinWindow(ModuleView mv, PinWindow w)
    {
        Save(mv, p => p.Group == null ? p : p with { Group = null });
        if (w.Frames.Count > 1)
        {
            if (ReferenceEquals(w.Frames[0], mv.Frame))
            {
                foreach (var f in w.Frames.Skip(1).ToList())
                {
                    var m = ModuleOf(f);
                    if (m == null) continue;
                    w.RemoveFrame(f);
                    _pins.Remove(m.Id);
                    Save(m, p => p with { Group = null });
                    Pin(m, null, restoring: true);
                }
                w.Release();
            }
            else w.RemoveFrame(mv.Frame);
            return;
        }
        w.Release();
    }
}
