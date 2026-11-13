<#
기능: 칼퇴 레일로 나눠 줄 훈수꾼 묶음(hunsu.zip)을 만든다. 칼퇴의 "apps 전용 설치 길"(칼퇴 docs/03 apps, 2026-11-12) 규격을 따른다.
      묶음 구성(zip 맨 위 기준):
        hunsu.app.json                 묶음 선언(id, version, name, exe, addins, runtime, quitMessage)
        app\ClockOut.Hunsu.exe 외      self-contained win-x64 publish(.NET 10 런타임을 따로 설치하지 않아도 됨 = "필수 lib"), ReadyToRun
        addins\clockout-hunsu.zip      다리 추가기능 패키지(clockout-hunsu.xlam + clockout-hunsu.clockout.json). 칼퇴가 기존 추가기능 흐름으로 설치
        LICENSE.txt, README.txt
      끝나면 SHA-256과 크기, 카탈로그 apps 항목 예시를 출력한다(out\pack\hunsu.zip, .sha256, catalog-entry.json).
사용법: pwsh -File tools\pack_app.ps1 -Version 0.1.000 [-SkipBridge]   (Windows PowerShell 5.1로 돌려도 같은 zip이 나온다)
        -SkipBridge: 엑셀 없이(이미 만든 out\clockout-hunsu.xlam을 그대로 씀)
의존: .NET 10 SDK, tools\build_bridge.ps1(엑셀 데스크톱판). 공개 Release·카탈로그 수정은 하지 않는다(사용자가 직접).
#>
param([string]$Version = '0.0.001', [switch]$SkipBridge)
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [Text.Encoding]::UTF8 } catch {}
$repo = Split-Path -Parent $PSScriptRoot
if ($Version -notmatch '^\d+\.\d+\.\d{3,}$') { Write-Host "[중단] 판 번호는 X.Y.ZZZ 모양이어야 합니다(칼퇴 규칙): $Version" -ForegroundColor Red; exit 1 }
$out = Join-Path $repo 'out\pack'
$stage = Join-Path $out 'stage'
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'app'), (Join-Path $stage 'addins') | Out-Null

Write-Host "== 1) self-contained publish (win-x64, ReadyToRun)"
dotnet publish (Join-Path $repo 'src\ClockOut.Hunsu\ClockOut.Hunsu.csproj') -c Release -r win-x64 --self-contained true `
    -p:PublishReadyToRun=true -p:DebugType=none -p:GenerateDocumentationFile=false -o (Join-Path $stage 'app') --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Host "[중단] publish 실패" -ForegroundColor Red; exit $LASTEXITCODE }

Write-Host "== 2) 다리 추가기능 패키지"
if (-not $SkipBridge) {
    pwsh -NoProfile -File (Join-Path $repo 'tools\build_bridge.ps1')
    if ($LASTEXITCODE -ne 0) { Write-Host "[중단] 다리 컴파일 실패" -ForegroundColor Red; exit $LASTEXITCODE }
}
$xlam = Join-Path $repo 'out\clockout-hunsu.xlam'
if (-not (Test-Path $xlam)) { Write-Host "[중단] $xlam 이 없습니다." -ForegroundColor Red; exit 1 }
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$bridgeZip = Join-Path $stage 'addins\clockout-hunsu.zip'
$za = [IO.Compression.ZipFile]::Open($bridgeZip, 'Create')
try {
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($za, $xlam, 'clockout-hunsu.xlam')
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($za, (Join-Path $repo 'addins\clockout-hunsu\clockout-hunsu.clockout.json'), 'clockout-hunsu.clockout.json')
} finally { $za.Dispose() }

Write-Host "== 3) 묶음 선언, 라이선스, 안내"
$manifest = [ordered]@{
    spec = 0; id = 'hunsu'; version = $Version; name = '훈수꾼'
    exe = 'app/ClockOut.Hunsu.exe'; addins = @('addins/clockout-hunsu.zip')
    runtime = 'self-contained'; quitMessage = 'ClockOut.App.Quit'
}
$utf8 = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllText((Join-Path $stage 'hunsu.app.json'), ($manifest | ConvertTo-Json -Depth 3), $utf8)
Copy-Item (Join-Path $repo 'LICENSE.txt') (Join-Path $stage 'LICENSE.txt')
[IO.File]::WriteAllText((Join-Path $stage 'README.txt'), "훈수꾼 $Version`r`n칼퇴 좌측 레일에서 설치·켜기·끄기를 합니다. 직접 실행: app\ClockOut.Hunsu.exe`r`n.NET 런타임을 따로 설치하지 않아도 됩니다(self-contained).`r`n", $utf8)

Write-Host "== 4) zip과 SHA-256"
$zip = Join-Path $out 'hunsu.zip'
Remove-Item $zip -ErrorAction SilentlyContinue
# CreateFromDirectory는 Windows PowerShell 5.1(.NET Framework)에서 항목 이름을 역슬래시(app\x.dll)로 넣어
# 칼퇴가 "안전하지 않은 경로"로 거부한다(docs/06, 2026-11-12). 항목 이름을 직접 '/'로 만들어 어떤 PowerShell에서도 같게 한다.
$stageFull = (Resolve-Path $stage).Path.TrimEnd('\') + '\'
$zz = [IO.Compression.ZipFile]::Open($zip, 'Create')
try {
    foreach ($f in Get-ChildItem $stage -Recurse -File) {
        $name = $f.FullName.Substring($stageFull.Length).Replace('\', '/')
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zz, $f.FullName, $name, [IO.Compression.CompressionLevel]::Optimal)
    }
} finally { $zz.Dispose() }
$za = [IO.Compression.ZipFile]::OpenRead($zip)
try { $badNames = @($za.Entries | Where-Object { $_.FullName.Contains('\') }).Count } finally { $za.Dispose() }
if ($badNames -gt 0) { Write-Host "[중단] zip 항목 이름에 역슬래시가 $badNames 개 있습니다." -ForegroundColor Red; exit 1 }
$sha = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
$size = (Get-Item $zip).Length
"$sha  hunsu.zip" | Set-Content -Encoding ascii (Join-Path $out 'hunsu.zip.sha256')
$files = (Get-ChildItem $stage -Recurse -File).Count
$entry = [ordered]@{
    id = 'hunsu'; name = '훈수꾼'; version = $Version; icon = 'hunsu'
    summary = '엑셀 창 위에 선택 요약과 점검 결과를 겹쳐 보여 줍니다.'
    url = "https://github.com/sbg3704/ClockOut-Catalog/releases/download/app-hunsu-$Version/hunsu.zip"
    sha256 = $sha; size = $size; host = 'excel'
}
[IO.File]::WriteAllText((Join-Path $out 'catalog-entry.json'), ($entry | ConvertTo-Json -Depth 3), $utf8)
Write-Host ""
Write-Host "완료: $zip"
Write-Host ("  크기 {0:N1} MB, 파일 {1}개, SHA-256 {2}" -f ($size / 1MB), $files, $sha)
Write-Host "  카탈로그 apps 항목 예시: $(Join-Path $out 'catalog-entry.json') (Release 태그 app-hunsu-$Version, 올리기는 사용자가 직접)"
exit 0
