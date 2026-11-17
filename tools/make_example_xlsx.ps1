<#
기능: 훈수꾼을 눈으로 확인할 시나리오 통합 문서(out\훈수꾼_확인용.xlsx)를 만든다. 모듈마다 시험할 시트와 "나와야 하는 것"이 들어 있다(첫 시트 [안내]).
      1) 저장소의 tools\scenarios\훈수꾼_시나리오.xlsx(tools\make_scenarios.py가 만든 작은 파일, 엑셀 없이 만들어 둠)를 복사한다.
      2) 보이지 않는 엑셀(Visible=False)로 열어 큰 시트 둘을 더하고 저장한 뒤 닫는다: [큰 표](2만 줄 x 10열), [대용량](10만 줄 x 12열). 범위 끌기·열 전체 선택 시험용.
         2)가 실패해도 1)의 파일은 그대로 쓸 수 있다(경고만 낸다).
사용법: pwsh -File tools\make_example_xlsx.ps1 [-Out <파일>] [-NoBig]
의존: 엑셀 데스크톱판(2단계만). 이 스크립트가 띄운 엑셀만 닫는다. 키보드·화면 조작 없음. 새 모듈을 더하면 make_scenarios.py의 SCENARIOS에 한 줄(docs/07 4장).
#>
param([string]$Out, [switch]$NoBig)
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [Text.Encoding]::UTF8 } catch {}
$repo = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot '_com.ps1')
if (-not $Out) { $Out = Join-Path $repo 'out\훈수꾼_확인용.xlsx' }
$src = Join-Path $repo 'tools\scenarios\훈수꾼_시나리오.xlsx'
if (-not (Test-Path $src)) { Write-Host "[중단] 시나리오 원본이 없습니다: $src" -ForegroundColor Red; exit 1 }
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Out) | Out-Null
Copy-Item $src $Out -Force
Write-Host "시나리오 문서를 복사했습니다: $Out"
$linkSrc = Join-Path $repo 'tools\scenarios\훈수꾼_링크시험.xlsx'   # 외부 링크 시나리오만 따로(끊긴 링크의 업데이트 경고가 메인 시험을 막지 않게)
if (Test-Path $linkSrc) { Copy-Item $linkSrc (Join-Path (Split-Path -Parent $Out) '훈수꾼_링크시험.xlsx') -Force; Write-Host "링크 시험 문서도 복사했습니다." }
if ($NoBig) { exit 0 }
# 관리자 권한 pwsh에서 엑셀을 띄우면 관리자 엑셀이 되어, 일반 권한 훈수꾼이 그 엑셀과 말을 못 한다(2026-11-17). 큰 시트는 건너뛴다(시나리오 문서는 그대로 쓸 수 있다).
if ([Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "[건너뜀] 관리자 권한 PowerShell입니다. 큰 시트를 더하려고 엑셀을 띄우면 관리자 엑셀이 되어 훈수꾼이 읽지 못하므로 건너뜁니다. 일반 PowerShell에서 다시 하면 큰 시트가 더해집니다." -ForegroundColor Yellow
    exit 0
}

Add-Type -Namespace HunsuEx -Name Win -MemberDefinition '[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(System.IntPtr hWnd, out uint pid);' -ErrorAction SilentlyContinue

# 수식으로 채운 뒤 값으로 굳힌다(고정된 가짜 자료). 큰 배열을 PowerShell에서 넘기면 엑셀이 거부하므로(0x800A03EC) 엑셀 안에서 복사·값 붙여넣기로 한다.
function Add-BigSheet($wb, [string]$name, [string]$addr) {
    $ws = $wb.Worksheets.Add([Type]::Missing, $wb.Worksheets.Item($wb.Worksheets.Count))
    $ws.Name = $name
    $big = $ws.Range($addr)
    $big.Formula = '=ROUND(MOD(ROW()*37+COLUMN()*101,997)+COLUMN()/10,2)'
    try { $big.Copy() | Out-Null; $big.PasteSpecial(-4163) | Out-Null; $big.Application.CutCopyMode = 0 }
    catch { Write-Host "  (값으로 굳히지 못해 수식 그대로 둡니다: $name)" -ForegroundColor Yellow }
}

$xl = $null; $xlPid = 0
try {
    $xl = New-Object -ComObject Excel.Application
    $p = [uint32]0; [void][HunsuEx.Win]::GetWindowThreadProcessId([IntPtr]$xl.Hwnd, [ref]$p); $xlPid = [int]$p
    $xl.Visible = $false; $xl.DisplayAlerts = $false; $xl.ScreenUpdating = $false
    $wb = Invoke-ComRetry { $xl.Workbooks.Open($Out, 0) }    # 0 = 외부 링크를 갱신하지 않음
    Add-BigSheet $wb '큰 표' 'A1:J20000'
    Add-BigSheet $wb '대용량' 'A1:L100000'
    $wb.Worksheets.Item('안내').Activate()
    $wb.Save()
    $wb.Close($false)
    Write-Host "큰 시트(큰 표, 대용량)를 더했습니다."
}
catch {
    Write-Host "[경고] 큰 시트를 더하지 못했습니다(시나리오 문서는 그대로 쓸 수 있습니다): $($_.Exception.Message)" -ForegroundColor Yellow
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
