# 기능: 프로그램을 바꾼 PR이 판 번호(Directory.Build.props의 Version)를 올렸는지 확인한다. 안 올렸거나 모양이 X.Y.ZZZ가 아니거나 main보다 낮으면 실패(종료 코드 1).
# 사용법: pwsh -File tools\check_version.ps1 [-Base origin/main]    (tools\build.ps1이 부른다. 기준을 못 찾으면 모양만 보고 통과)
# 의존: git, pwsh. 파이썬이 필요 없다(사용자 PC에는 없음). 파일을 바꾸지 않는다. "프로그램"은 src/, modules/, addins/ 아래를 말한다(문서·협업·tools만 바꾼 PR은 올리지 않아도 된다).
param([string]$Base = 'origin/main')
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)
function Read-Ver([string]$text, [bool]$strict) {
    $pat = if ($strict) { '<Version>(\d+)\.(\d+)\.(\d{3,})</Version>' } else { '<Version>(\d+)\.(\d+)\.(\d+)</Version>' }
    $m = [regex]::Match("$text", $pat)
    if (-not $m.Success) { return $null }
    return [version]::new([int]$m.Groups[1].Value, [int]$m.Groups[2].Value, [int]$m.Groups[3].Value)
}
function Show([version]$v) { if ($v) { '{0}.{1}.{2:D3}' -f $v.Major, $v.Minor, $v.Build } else { '?' } }

$cur = Read-Ver (Get-Content Directory.Build.props -Raw -Encoding UTF8) $true
if (-not $cur) { Write-Host '[실패] Directory.Build.props의 <Version>이 X.Y.ZZZ(끝 세 자리) 모양이 아닙니다.'; exit 1 }
$oldText = git show "${Base}:Directory.Build.props" 2>$null
if ($LASTEXITCODE -ne 0) { Write-Host "[알림] 기준 $Base 를 찾지 못해 모양만 확인했습니다(판 $(Show $cur))."; exit 0 }
$old = Read-Ver ($oldText -join "`n") $false
$names = git diff --name-only "$Base...HEAD" 2>$null
$changed = @()
if ($LASTEXITCODE -eq 0) { $changed = @($names | Where-Object { $_ -match '^(src/|modules/|addins/|Directory\.Build\.props)' }) }
if ($changed.Count -gt 0 -and $old -and $cur -le $old) {
    Write-Host "[실패] 프로그램 파일이 바뀌었는데($($changed.Count)개) 판 번호가 그대로입니다: $(Show $cur) (main $(Show $old))."
    Write-Host '  Directory.Build.props의 <Version>을 올리세요(+1 고침, +10 기능, +100 큰 변화). 다른 계정의 열린 PR과 같은 번호를 고르지 않게 협업 노트를 보세요.'
    exit 1
}
Write-Host "판 번호 확인 통과: $(Show $cur) (main $(Show $old))"
exit 0
