<#
기능: 0단계 측정을 한 번에. 빌드(Release) → 다리 컴파일 → 측정 도구 실행 → out\bench\stage0_result.md 와 그림(out\bench\snap).
사용법: pwsh -File tools\bench\run_bench.ps1 [-N 100] [-NoBuild]
의존: tools\build.ps1, tools\build_bridge.ps1, 엑셀 데스크톱판.
주의: 측정하는 동안(약 1분) 화면 왼쪽 위에 짙은 회색 "시험 창"과 그 위의 오버레이가 잠깐 보인다.
      두 창 모두 포커스를 가져가지 않고 키보드·마우스를 쓰지 않는다. 엑셀은 보이지 않게 띄우고 끝나면 닫는다.
#>
param([int]$N = 100, [switch]$NoBuild)
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [Text.Encoding]::UTF8 } catch {}
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $NoBuild) {
    dotnet build (Join-Path $repo 'src\ClockOut.Hunsu.slnx') -c Release --nologo -v q
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    pwsh -NoProfile -File (Join-Path $repo 'tools\build_bridge.ps1')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
$exe = Join-Path $repo 'src\ClockOut.Hunsu\bin\Release\net10.0-windows\win-x64\ClockOut.Hunsu.exe'
$bench = Join-Path $repo 'tools\bench\ClockOut.Hunsu.Bench\bin\Release\net10.0-windows\win-x64\ClockOut.Hunsu.Bench.exe'
$out = Join-Path $repo 'out\bench'
Remove-Item (Join-Path $out 'snap') -Recurse -Force -ErrorAction SilentlyContinue
& $bench --app $exe --xlam (Join-Path $repo 'out\clockout-hunsu.xlam') --out $out --n $N
exit $LASTEXITCODE
