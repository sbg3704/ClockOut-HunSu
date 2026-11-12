<#
기능: 훈수꾼 전체 빌드와 시험. C# 솔루션 빌드 → Core 시험(dotnet test) → (엑셀이 있으면) 다리 컴파일과 COM 시험.
사용법: pwsh -File tools\build.ps1              전부
        pwsh -File tools\build.ps1 -NoExcel     C#만(클라우드나 엑셀 없는 PC)
        pwsh -File tools\build.ps1 -Release     Release 구성
의존: .NET 10 SDK(global.json), 엑셀 데스크톱판(다리 단계만). 키보드·화면 조작 없음.
#>
param([switch]$NoExcel, [switch]$Release)
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [Text.Encoding]::UTF8 } catch {}
$repo = Split-Path -Parent $PSScriptRoot
$sln = Join-Path $repo 'src\ClockOut.Hunsu.slnx'
$cfg = if ($Release) { 'Release' } else { 'Debug' }
function Step([string]$name, [scriptblock]$body) {
    Write-Host "== $name" -ForegroundColor Cyan
    & $body
    if ($LASTEXITCODE -ne 0) { Write-Host "[실패] $name (종료 코드 $LASTEXITCODE)" -ForegroundColor Red; exit $LASTEXITCODE }
}
Step 'C# 빌드' { dotnet build $sln -c $cfg --nologo -v q }
Step 'Core 시험' { dotnet test (Join-Path $repo 'src\ClockOut.Hunsu.Tests') -c $cfg --nologo --no-build -v q }
if (-not $NoExcel) {
    Step '다리 컴파일' { pwsh -NoProfile -File (Join-Path $PSScriptRoot 'build_bridge.ps1') }
    Step '다리 COM 시험' { pwsh -NoProfile -File (Join-Path $PSScriptRoot 'test_bridge.ps1') }
}
Write-Host "모두 통과." -ForegroundColor Green
exit 0
