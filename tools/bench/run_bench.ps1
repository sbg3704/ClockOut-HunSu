<#
기능: 0단계 측정을 한 번에. 빌드(Release, out\benchbin) → 다리 컴파일 → 측정 도구 실행 → out\bench\stage0_result.md 와 그림(out\bench\snap).
사용법: pwsh -File tools\bench\run_bench.ps1 [-N 100] [-NoBuild]
의존: tools\build_bridge.ps1, 엑셀 데스크톱판.
주의: 측정하는 동안(약 1분) 화면 왼쪽 위에 짙은 회색 "시험 창"과 그 위의 오버레이가 잠깐 보인다.
      두 창 모두 포커스를 가져가지 않고 키보드·마우스를 쓰지 않는다. 엑셀은 보이지 않게 띄우고 끝나면 닫는다.
      앱과 측정 도구는 out\benchbin에 따로 빌드한다. 사용자가 src\...\bin의 훈수꾼을 켜 두어 파일이 잠겨 있어도 측정할 수 있게(2026-11-12).
      단, 다른 훈수꾼이 켜져 있으면 다리 신호가 그쪽으로 갈 수 있다. 측정 도구가 "앱 신호 창 찾기" 확인에서 알려 준다.
#>
param([int]$N = 100, [switch]$NoBuild)
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [Text.Encoding]::UTF8 } catch {}
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$bin = Join-Path $repo 'out\benchbin'
if (-not $NoBuild) {
    dotnet build (Join-Path $repo 'src\ClockOut.Hunsu\ClockOut.Hunsu.csproj') -c Release --nologo -v q -o (Join-Path $bin 'app')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet build (Join-Path $repo 'tools\bench\ClockOut.Hunsu.Bench\ClockOut.Hunsu.Bench.csproj') -c Release --nologo -v q -o (Join-Path $bin 'bench')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    pwsh -NoProfile -File (Join-Path $repo 'tools\build_bridge.ps1')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
$exe = Join-Path $bin 'app\ClockOut.Hunsu.exe'
$bench = Join-Path $bin 'bench\ClockOut.Hunsu.Bench.exe'
$out = Join-Path $repo 'out\bench'
Remove-Item (Join-Path $out 'snap') -Recurse -Force -ErrorAction SilentlyContinue
& $bench --app $exe --xlam (Join-Path $repo 'out\clockout-hunsu.xlam') --out $out --n $N
exit $LASTEXITCODE
