// 기능: 창 밖 사이드바 모드(ShellController의 일부). 설정 sidebar가 left/right면 도크 패널을 모두 사이드바 안에 구역으로 쌓고 왼쪽 도크(훈 원)·오른쪽 도크는 감춘다. 앱 바라 엑셀이 남은 영역으로 줄어든다. 끄면 예전 배치(왼쪽 반쯤 숨은 도크 + 오른쪽 패널)로 돌아간다.
// 사용법: 생성자가 CreateSidebar()를, Follow/OnTargetGone/ToggleHideAll/ApplySetting이 UpdateSidebar()를, RefreshDocks가 SidebarOn으로 분기한다. 설정 창 [일반]의 "사이드바" 선택이 켜고 끈다.
// 의존: Overlay/SidebarWindow, Native/AppBar, Core/Layout/SideTabs, 설정 sidebar·sidebarWidth. 폭은 끌어 바꾼 값이 sidebarWidth로, 묶음 탭 구성은 layout.json sideTabs로, 모듈 높이는 SideHeight로 저장된다(docs/19).
using ClockOut.Hunsu.Core.Layout;
using ClockOut.Hunsu.Native;
using ClockOut.Hunsu.Overlay;

namespace ClockOut.Hunsu.Shell;

internal sealed partial class ShellController
{
    private SidebarWindow? _sidebar;

    /// <summary>사이드바가 지금 켜져 있고 엑셀이 있어 가장자리를 예약했는가(패널이 거기 쌓이는 모드).</summary>
    private bool SidebarOn => _settings.Sidebar != "off" && _sidebar is { Reserved: true };

    private void CreateSidebar()
    {
        _sidebar = new SidebarWindow();
        _sidebar.ModuleClicked += OnModuleIcon;
        _sidebar.WidthChanged += w => ApplySetting("sidebarWidth", w);
        _sidebar.FooterClicked += OnSidebarFooter;
        // 묶음 탭 끌어 옮기기: 배치 잠금 "자유", 부르기(Ctrl+Alt+H) 중, 또는 Shift를 누른 채(떼어 낸 패널과 같은 규칙).
        _sidebar.DragAllowed = () => _summoned || _settings.LayoutLock == "free" || User32.ShiftDown();
        _sidebar.TabsChanged += tabs =>
        {
            _store.State.SideTabs = SideTabs.Persist(_store.State.SideTabs, tabs);
            _store.SaveSoon();
            _log.Write("sidebar_tabs", ("tabs", tabs.Count));
            RefreshDocks();
        };
        _sidebar.TabsReset += () =>
        {
            _store.State.SideTabs = [];
            _store.SaveSoon();
            _log.Write("sidebar_tabs_reset");
            RefreshDocks();
        };
        _sidebar.HeightChanged += (id, h) =>
        {
            var mv = _modules.FirstOrDefault(m => m.Id == id);
            if (mv != null) Save(mv, p => p with { SideHeight = h });
        };
    }

    /// <summary>사이드바에 넣을 묶음 탭: 저장된 구성(layout.json의 sideTabs)이 있으면 그대로, 없으면 기능 묶음(order 범위).</summary>
    private IReadOnlyList<SideTab> SidebarTabs(IEnumerable<ModuleView> docked) =>
        SideTabs.Build(docked.Select(m => new SideTabModule(m.Id, m.Module.Manifest.Name, m.Placement.Order, m.Module.Manifest.Icon)), _store.State.SideTabs);

    /// <summary>엑셀이 없어졌다: 예약을 풀고(화면 가장자리를 돌려줌) 도크 배치로 되돌려 둔다. 엑셀이 다시 보이면 Follow가 UpdateSidebar로 켠다.</summary>
    private void UpdateSidebarOffNoTarget()
    {
        if (_sidebar is not { Reserved: true }) return;
        // 바로 풀지 않고 3초 기다린다: 엑셀 창이 잠깐 사라졌다 돌아오는 일(창 바꾸기, 통합 문서 닫기)에 예약을 풀었다 다시 하면 엑셀이 커졌다 작아졌다 한다.
        _sidebarOff ??= new System.Windows.Threading.DispatcherTimer(TimeSpan.FromSeconds(3), System.Windows.Threading.DispatcherPriority.Background, (_, _) =>
        {
            _sidebarOff!.Stop();
            if (_sidebar is not { Reserved: true } || Excel.ExcelWindows.FindVisibleMain() != IntPtr.Zero) return; // 엑셀이 돌아왔으면 Follow가 이미 맡았다
            _sidebar.SetActive(false);
            _log.Write("sidebar_released");
            RefreshDocks();
        }, System.Windows.Application.Current.Dispatcher);
        _sidebarOff.Stop();
        _sidebarOff.Start();
    }

    private System.Windows.Threading.DispatcherTimer? _sidebarOff;

    /// <summary>활동 막대 맨 아래 일반 단추: 설정 창, 모듈 관리, 모양, 작업 프로필, 탭↔쌓기, 엑셀 정리, 사이드바 접기·펴기.</summary>
    private void OnSidebarFooter(string key)
    {
        switch (key)
        {
            case "settings": OpenManager("general"); break;
            case "modules": OpenManager("modules"); break;
            case "look": OpenManager("look"); break;
            case "profiles": OpenManager("profiles"); break;
            case "view": ApplySetting("sidebarTabs", !_settings.SidebarTabs); break;
            case "cleanup": OpenExcelCleanup(); break;
            case "stay": ApplySetting("sidebarStay", !_settings.SidebarStay); break;
            case "fold": ApplySetting("sidebarCollapsed", !_settings.SidebarCollapsed); break;
        }
    }

    /// <summary>설정과 엑셀 유무에 맞춰 사이드바를 켜거나 끄고, 패널을 사이드바나 도크에 다시 놓는다.</summary>
    private void UpdateSidebar(bool refresh = true)
    {
        if (_sidebar == null) return;
        bool was = _sidebar.Reserved;
        if (_target != IntPtr.Zero) _sidebarOff?.Stop(); // 엑셀이 돌아왔다: 풀기 예약 취소
        bool want = _settings.Sidebar != "off" && !_hiddenAll && _target != IntPtr.Zero;
        _sidebar.Configure(_settings.Sidebar, _settings.SidebarWidth, _settings.SidebarTabs, _settings.SidebarCollapsed, _settings.SidebarStay);
        _sidebar.SetActive(want, _target);
        _log.Write("sidebar", ("side", _settings.Sidebar), ("width", _settings.SidebarWidth), ("active", _sidebar.Reserved));
        if (refresh || was != _sidebar.Reserved) RefreshDocks(); // 창만 바뀐 것(Follow)이면 패널을 다시 놓지 않는다
    }
}
