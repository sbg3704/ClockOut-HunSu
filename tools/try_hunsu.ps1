<#
기능: 사용자가 직접 훈수꾼을 눈으로 확인할 때 쓰는 실행기. 새 엑셀 창을 보이게 띄워 다리(out\clockout-hunsu.xlam)와 예제 통합 문서를 열고 훈수꾼을 켠다.
      칼퇴 레일로 설치하지 않아도 숫자(합계·평균)가 나오게 하려는 것이다. 다리는 이 엑셀 창에서만 열리고 엑셀 추가기능 목록에 등록되지 않는다.
사용법: pwsh -File tools\try_hunsu.ps1            (먼저 tools\build.ps1 또는 dotnet build -c Release, tools\make_example_xlsx.ps1)
        끝낼 때: 작업 표시줄 "훈" 아이콘 → [훈수꾼 끄기], 엑셀은 평소처럼 닫는다(예제 파일은 저장하지 않아도 됨).
의존: 엑셀 데스크톱판, src\ClockOut.Hunsu\bin\Release\...\ClockOut.Hunsu.exe(없으면 Debug), out\clockout-hunsu.xlam, out\훈수꾼_확인용.xlsx
주의: 사람이 직접 실행하는 도구다(엑셀을 보이게 띄운다). Claude와 측정 도구는 쓰지 않는다(사용자 PC 화면 조작 금지 규칙).
#>
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [Text.Encoding]::UTF8 } catch {}
$repo = Split-Path -Parent $PSScriptRoot
$xlam = Join-Path $repo 'out\clockout-hunsu.xlam'
$book = Join-Path $repo 'out\훈수꾼_확인용.xlsx'
$exe = @('Release', 'Debug') | ForEach-Object { Join-Path $repo "src\ClockOut.Hunsu\bin\$_\net10.0-windows\win-x64\ClockOut.Hunsu.exe" } | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not (Test-Path $xlam)) { Write-Host "다리가 없습니다. 먼저: pwsh -File tools\build_bridge.ps1" -ForegroundColor Yellow; exit 1 }
if (-not (Test-Path $book)) { Write-Host "예제 파일이 없습니다. 먼저: pwsh -File tools\make_example_xlsx.ps1" -ForegroundColor Yellow; exit 1 }
if (-not $exe) { Write-Host "훈수꾼 실행 파일이 없습니다. 먼저: dotnet build src\ClockOut.Hunsu.slnx -c Release" -ForegroundColor Yellow; exit 1 }

$xl = New-Object -ComObject Excel.Application
$xl.Visible = $true
$null = $xl.Workbooks.Open($xlam)
$null = $xl.Workbooks.Open($book)
$xl.Run("'clockout-hunsu.xlam'!CoHunsu_Start")
[void][Runtime.InteropServices.Marshal]::ReleaseComObject($xl)   # 엑셀은 사용자가 계속 쓴다(닫지 않음)
if (-not (Get-Process ClockOut.Hunsu -ErrorAction SilentlyContinue)) { Start-Process $exe }
Write-Host "엑셀과 훈수꾼을 켰습니다. [안내] 시트의 순서대로 확인해 보세요."
