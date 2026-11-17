// 기능: 창 밖 사이드바 모드(ShellController의 일부). 설정 sidebar가 left/right면 도크 패널을 모두 사이드바 안에 구역으로 쌓고 왼쪽 도크(훈 원)·오른쪽 도크는 감춘다. 앱 바라 엑셀이 남은 영역으로 줄어든다. 끄면 예전 배치(왼쪽 반쯤 숨은 도크 + 오른쪽 패널)로 돌아간다.
// 사용법: 생성자가 CreateSidebar()를, Follow/OnTargetGone/ToggleHideAll/ApplySetting이 UpdateSidebar()를, RefreshDocks가 SidebarOn으로 분기한다. 설정 창 [일반]의 "사이드바" 선택이 켜고 끈다.
// 의존: Overlay/SidebarWindow, Native/AppBar, 설정 sidebar·sidebarWidth. 폭은 끌어 바꾼 값이 sidebarWidth로 저장된다.
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
    }

    /// <summary>엑셀이 없어졌다: 예약을 풀고(화면 가장자리를 돌려줌) 도크 배치로 되돌려 둔다. 엑셀이 다시 보이면 Follow가 UpdateSidebar로 켠다.</summary>
    private void UpdateSidebarOffNoTarget()
    {
        if (_sidebar is not { Reserved: true }) return;
        _sidebar.SetActive(false);
        RefreshDocks();
    }

    /// <summary>활동 막대 맨 아래 일반 단추: 설정 창, 모듈 관리, 모양, 작업 프로필, 탭↔쌓기, 엑셀 정리, 사이드바 끄기.</summary>
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
            case "off": ApplySetting("sidebar", "off"); break;
        }
    }

    /// <summary>설정과 엑셀 유무에 맞춰 사이드바를 켜거나 끄고, 패널을 사이드바나 도크에 다시 놓는다.</summary>
    private void UpdateSidebar()
    {
        if (_sidebar == null) return;
        bool want = _settings.Sidebar != "off" && !_hiddenAll && _target != IntPtr.Zero;
        _sidebar.Configure(_settings.Sidebar, _settings.SidebarWidth, _settings.SidebarTabs);
        _sidebar.SetActive(want, _target);
        _log.Write("sidebar", ("side", _settings.Sidebar), ("width", _settings.SidebarWidth), ("active", _sidebar.Reserved));
        RefreshDocks();
    }
}
