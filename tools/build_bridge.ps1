<#
기능: 다리(addins/clockout-hunsu)를 보이지 않는 엑셀에서 컴파일해 out\clockout-hunsu.xlam으로 만든다.
      1) 소스가 CP949+CRLF인지 검사  2) Visible=False 엑셀의 새 통합 문서에 가져오기 + ThisWorkbook 코드
      3) VBE 'VBAProject 컴파일'(CommandBars Id 578)이 비활성이 되면 성공  4) .xlam(형식 55)으로 저장
사용법: pwsh -File tools\build_bridge.ps1 [-OutDir <폴더>]
의존: Windows용 엑셀 데스크톱판, 'VBA 프로젝트 개체 모델에 대한 액세스 신뢰'(이 스크립트는 그 설정을 바꾸지 않는다).
      칼퇴 카탈로그 저장소 tools/build_addin.ps1의 방식을 따랐다(소스가 이미 CP949라 변환 단계만 뺐다).
      키보드·화면 조작 없음. 이 스크립트가 띄운 엑셀만 닫는다. 종료 코드: 0 성공, 1 일반, 2 엑셀 없음, 3 VBA 접근 꺼짐, 4 컴파일 오류.
#>
param([string]$OutDir)
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [Text.Encoding]::UTF8 } catch {}
try { [Text.Encoding]::RegisterProvider([Text.CodePagesEncodingProvider]::Instance) } catch {}
$repo = Split-Path -Parent $PSScriptRoot
$src = Join-Path $repo 'addins\clockout-hunsu'
if (-not $OutDir) { $OutDir = Join-Path $repo 'out' }
$cp949 = [Text.Encoding]::GetEncoding(949)

& (Join-Path $PSScriptRoot 'vba_to_cp949.ps1') -Check
if ($LASTEXITCODE -ne 0) { Write-Host "[중단] VBA 소스가 CP949+CRLF가 아닙니다. tools\vba_to_cp949.ps1을 먼저 돌리세요." -ForegroundColor Red; exit 1 }

$code = 0; $xl = $null; $xlPid = 0
Add-Type -Namespace HunsuBuild -Name Win -MemberDefinition '[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(System.IntPtr hWnd, out uint pid);' -ErrorAction SilentlyContinue
try {
    try { $xl = New-Object -ComObject Excel.Application } catch { $code = 2; throw "엑셀(데스크톱판)을 실행할 수 없습니다." }
    $xl.Visible = $false; $xl.DisplayAlerts = $false; $xl.ScreenUpdating = $false
    $p = [uint32]0; [void][HunsuBuild.Win]::GetWindowThreadProcessId([IntPtr]$xl.Hwnd, [ref]$p); $xlPid = [int]$p
    $wb = $xl.Workbooks.Add()
    try { $vp = $wb.VBProject; $null = $vp.VBComponents.Count } catch { $code = 3; throw "엑셀이 VBA 프로젝트 접근을 막고 있습니다(보안 센터 > 매크로 설정 > 'VBA 프로젝트 개체 모델에 대한 액세스 신뢰')." }
    try { $vp.Name = 'ClockOut_hunsu' } catch {}
    $mods = Get-ChildItem $src -File | Where-Object { $_.Extension -in '.bas', '.cls' } | Sort-Object Name
    foreach ($m in $mods) { $null = $vp.VBComponents.Import($m.FullName) }
    $tw = Get-ChildItem $src -File -Filter 'ThisWorkbook_*.txt' | Select-Object -First 1
    if ($tw) { $vp.VBComponents.Item($wb.CodeName).CodeModule.AddFromString($cp949.GetString([IO.File]::ReadAllBytes($tw.FullName))) }
    Write-Host "모듈 $($mods.Count)개를 가져왔습니다. 컴파일합니다..."
    $btn = $xl.VBE.CommandBars.FindControl(1, 578)
    if ($null -eq $btn) { throw "VBE의 'VBAProject 컴파일' 명령을 찾지 못했습니다." }
    $xl.VBE.ActiveVBProject = $vp
    $btn.Execute()
    if ($btn.Enabled) {
        $where = ''
        try { $cp = $xl.VBE.ActiveCodePane; $sl = 0; $sc = 0; $el = 0; $ec = 0; $cp.GetSelection([ref]$sl, [ref]$sc, [ref]$el, [ref]$ec); $where = " 모듈 '$($cp.CodeModule.Parent.Name)' ${sl}번째 줄: $($cp.CodeModule.Lines($sl, 1).Trim())" } catch {}
        $code = 4; throw "컴파일 오류가 있습니다.$where"
    }
    New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
    $xlam = Join-Path $OutDir 'clockout-hunsu.xlam'
    Remove-Item $xlam -ErrorAction SilentlyContinue
    $wb.SaveAs($xlam, 55)
    $wb.Close($false)
    Write-Host "컴파일 성공. 저장: $xlam"
}
catch {
    Write-Host "[중단] $($_.Exception.Message)" -ForegroundColor Red
    if ($code -eq 0) { $code = 1 }
}
finally {
    if ($null -ne $xl) {
        try { foreach ($b in @($xl.Workbooks)) { try { $b.Saved = $true; $b.Close($false) } catch {} } } catch {}
        try { $xl.Quit() } catch {}
        try { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($xl) } catch {}
        $xl = $null; [GC]::Collect(); [GC]::WaitForPendingFinalizers()
    }
    if ($xlPid -gt 0) {
        for ($i = 0; $i -lt 40; $i++) { if (-not (Get-Process -Id $xlPid -ErrorAction SilentlyContinue)) { break }; Start-Sleep -Milliseconds 250 }
        Stop-Process -Id $xlPid -Force -ErrorAction SilentlyContinue
    }
}
exit $code
