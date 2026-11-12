<#
기능: 다리(out\clockout-hunsu.xlam)를 보이지 않는 엑셀에 열고 COM으로 hello, selection.summary, bridge.stats, 없는 조회를 불러 JSON을 검증한다(docs/02 9장).
사용법: pwsh -File tools\test_bridge.ps1      (먼저 tools\build_bridge.ps1)
의존: 엑셀 데스크톱판. 시험용 새 통합 문서에 숫자, 글자, 수식, 오류를 넣어 쓴다(저장하지 않음). 키보드·화면 조작 없음.
      이 스크립트가 띄운 엑셀만 닫는다. 종료 코드: 0 모두 통과, 1 실패.
#>
param([string]$Xlam)
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [Text.Encoding]::UTF8 } catch {}
$repo = Split-Path -Parent $PSScriptRoot
if (-not $Xlam) { $Xlam = Join-Path $repo 'out\clockout-hunsu.xlam' }
if (-not (Test-Path $Xlam)) { Write-Host "[중단] $Xlam 이 없습니다. tools\build_bridge.ps1을 먼저 돌리세요." -ForegroundColor Red; exit 1 }
Add-Type -Namespace HunsuTest -Name Win -MemberDefinition '[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(System.IntPtr hWnd, out uint pid);' -ErrorAction SilentlyContinue

$script:fail = 0; $script:pass = 0
function Check([string]$name, [bool]$ok, [string]$detail = '') {
    if ($ok) { $script:pass++; Write-Host "  [통과] $name" } else { $script:fail++; Write-Host "  [실패] $name $detail" -ForegroundColor Red }
}
$xl = $null; $xlPid = 0
try {
    $xl = New-Object -ComObject Excel.Application
    $xl.Visible = $false; $xl.DisplayAlerts = $false
    $p = [uint32]0; [void][HunsuTest.Win]::GetWindowThreadProcessId([IntPtr]$xl.Hwnd, [ref]$p); $xlPid = [int]$p
    $null = $xl.Workbooks.Open($Xlam)
    $wb = $xl.Workbooks.Add()
    $ws = $wb.Worksheets.Item(1)
    $ws.Range('A1').Value2 = 10; $ws.Range('A2').Value2 = 20.5; $ws.Range('A3').Value2 = '글자'
    $ws.Range('A4').Formula = '=A1*2'; $ws.Range('A5').Formula = '=1/0'
    $run = { param($proc, $a1, $a2) if ($PSBoundParameters.Count -ge 3) { $xl.Run("'clockout-hunsu.xlam'!$proc", $a1, $a2) } else { $xl.Run("'clockout-hunsu.xlam'!$proc") } }

    Write-Host "hello"
    $h = (& $run 'CoHunsu_Hello') | ConvertFrom-Json
    Check 'ok와 proto 0' ($h.ok -eq $true -and $h.proto -eq 0)
    Check 'queries에 selection.summary' ($h.data.queries -contains 'selection.summary')
    Check 'office.bits' ($h.data.office.bits -in 32, 64) "bits=$($h.data.office.bits)"
    Write-Host "  판 $($h.data.version), 엑셀 $($h.data.office.version) $($h.data.office.bits)비트"

    Write-Host "selection.summary"
    $null = $ws.Range('A1:A6').Select()
    $s = (& $run 'CoHunsu_Query' 'selection.summary' '{}') | ConvertFrom-Json
    Check 'ok' ($s.ok -eq $true) ($s | ConvertTo-Json -Compress)
    Check 'addr $A$1:$A$6' ($s.data.addr -eq '$A$1:$A$6') $s.data.addr
    Check 'count 6, nonEmpty 5' ($s.data.count -eq 6 -and $s.data.nonEmpty -eq 5) "$($s.data.count)/$($s.data.nonEmpty)"
    Check 'sum 50.5 (10+20.5+20)' ([math]::Abs($s.data.sum - 50.5) -lt 1e-9) $s.data.sum
    Check 'min 10, max 20.5' ($s.data.min -eq 10 -and $s.data.max -eq 20.5)
    Check 'errors 1, formula 2, text 1, num 3' ($s.data.errors -eq 1 -and $s.data.types.formula -eq 2 -and $s.data.types.text -eq 1 -and $s.data.types.num -eq 3) ($s.data.types | ConvertTo-Json -Compress)
    $null = $ws.Range('C1').Select()
    $e = (& $run 'CoHunsu_Query' 'selection.summary' '{}') | ConvertFrom-Json
    Check '빈 칸: sum null, count 1' ($e.ok -and $null -eq $e.data.sum -and $e.data.count -eq 1 -and $e.data.nonEmpty -eq 0)
    $null = $ws.Range('A:A').Select()
    $t = [Diagnostics.Stopwatch]::StartNew()
    $c = (& $run 'CoHunsu_Query' 'selection.summary' '{}') | ConvertFrom-Json
    $t.Stop()
    Check "열 전체(104만 칸) 선택도 빠르게: $($t.ElapsedMilliseconds)ms" ($c.ok -and $c.data.count -eq 1048576 -and $c.data.nonEmpty -eq 5 -and $t.ElapsedMilliseconds -lt 500)
    $null = $ws.Range('A1:B2,A4').Select()
    $m = (& $run 'CoHunsu_Query' 'selection.summary' '{}') | ConvertFrom-Json
    Check '여러 영역 A1:B2,A4' ($m.ok -and $m.data.count -eq 5 -and [math]::Abs($m.data.sum - 50.5) -lt 1e-9) "$($m.data.addr) sum=$($m.data.sum)"

    Write-Host "오류 처리"
    $u = (& $run 'CoHunsu_Query' 'no.such' '{}') | ConvertFrom-Json
    Check 'query_unknown' ($u.ok -eq $false -and $u.code -eq 'query_unknown')
    $d = (& $run 'CoHunsu_Do' 'goto' '{}') | ConvertFrom-Json
    Check '명령 없음 → query_unknown' ($d.ok -eq $false -and $d.code -eq 'query_unknown')

    Write-Host "bridge.stats (틀 창이 없으면 신호를 보내지 않고 시간만 잰다)"
    $null = (& $run 'CoHunsu_Query' 'bridge.stats' '{"reset":true}')
    foreach ($i in 1..20) { $null = $ws.Cells.Item($i, 2).Select() }
    $b = (& $run 'CoHunsu_Query' 'bridge.stats' '{}') | ConvertFrom-Json
    Check 'events 20' ($b.ok -and $b.data.events -eq 20) "events=$($b.data.events)"
    $mx = ($b.data.samplesUs | Measure-Object -Maximum).Maximum
    Check "이벤트 처리 최대 ${mx}µs < 5000µs" ($mx -lt 5000)
    Check '설정 그대로(ScreenUpdating, EnableEvents, Calculation)' ($xl.ScreenUpdating -and $xl.EnableEvents -and $xl.Calculation -eq -4105)
}
catch {
    $script:fail++
    Write-Host "[오류] $($_.Exception.Message)" -ForegroundColor Red
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
Write-Host ""
Write-Host "통과 $script:pass, 실패 $script:fail"
if ($script:fail -gt 0) { exit 1 } else { exit 0 }
