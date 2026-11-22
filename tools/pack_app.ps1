<#
기능: 칼퇴 레일로 나눠 줄 훈수꾼 묶음(hunsu.zip)을 만든다. 칼퇴의 "apps 전용 설치 길"(칼퇴 docs/03 apps, 2026-11-12) 규격을 따른다.
      묶음 구성(zip 맨 위 기준):
        hunsu.app.json                 묶음 선언(contract_version 2, commands.actions·layout 탭, id, version, name, exe, addins, runtime, quitMessage, settingsMessage, settingsArgs·commands.settings, modules, memoryMb·startMs는 assets\load.json이 있을 때만)
                                       modules = 칼퇴 [모듈 관리]에서 켜고 끄는 하위 모듈 목록(칼퇴 docs/10 1장). modules\<id>\<id>.hunsu.json에서 만든다:
                                       id = 모듈 id, name = 이름, summary = 설명의 첫 문장, default = true(훈수꾼 안의 보이기·숨기기와 따로)
        app\ClockOut.Hunsu.exe 외      self-contained win-x64 publish(.NET 10 런타임을 따로 설치하지 않아도 됨 = "필수 lib"), ReadyToRun
        addins\clockout-hunsu.zip      다리 추가기능 패키지(clockout-hunsu.xlam + clockout-hunsu.clockout.json). 칼퇴가 기존 추가기능 흐름으로 설치
        hunsu.ico                      칼퇴 레일 아이콘(assets\icon\hunsu.ico, 칼퇴 docs/11 8장: 묶음 맨 위 <id>.ico를 이름으로 찾음)
        hunsu.guide.md, hunsu.changes.md   칼퇴 소개 화면의 사용법·패치노트(assets\guide에서 복사, 규칙은 claude-workspace docs/하위프로그램_안내문_규칙.md)
        LICENSE.txt, README.txt
      zip을 만든 직후 tools\check_guide.ps1이 안내문 규칙을 검사하고, 어기면 포장이 실패한다.
      끝나면 SHA-256과 크기, 카탈로그 apps 항목 예시를 출력한다(out\pack\hunsu.zip, .sha256, catalog-entry.json, release-record.json).
      SHA 장부(releases\hunsu_sha256.md)에 같은 판이 다른 SHA로 이미 있으면 실패한다(한 판 = 한 SHA, 서버 장부·카탈로그가 같은 값으로 판별, 2026-11-22).
사용법: pwsh -File tools\pack_app.ps1 -Version 0.1.000 [-SkipBridge]
        -SkipBridge: 엑셀 없이(이미 만든 out\clockout-hunsu.xlam을 그대로 씀)
의존: .NET 10 SDK, tools\build_bridge.ps1(엑셀 데스크톱판). 공개 Release·카탈로그 수정은 하지 않는다(사용자가 직접).
#>
param([string]$Version = '0.0.001', [switch]$SkipBridge)
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [Text.Encoding]::UTF8 } catch {}
$repo = Split-Path -Parent $PSScriptRoot
if ($PSVersionTable.PSVersion.Major -lt 7) { Write-Host "[중단] PowerShell 7(pwsh)로 실행하세요: pwsh -File tools\pack_app.ps1 -Version $Version" -ForegroundColor Red; exit 1 }
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
    spec = 0; contract_version = 2; id = 'hunsu'; version = $Version; name = '훈수꾼'   # 계약 판(칼퇴 docs/11, 2026-11-22)
    exe = 'app/ClockOut.Hunsu.exe'; addins = @('addins/clockout-hunsu.zip')
    runtime = 'self-contained'; quitMessage = 'ClockOut.App.Quit'
    settingsMessage = 'ClockOut.App.Settings'; settingsArgs = @('--settings')   # 칼퇴 레일 [설정](2026-11-22, Core/AppMessages). 옛 칼퇴용으로 남김
    commands = [ordered]@{ settings = @('--settings')                              # 새 계약(칼퇴 docs/11): 둘 다 있으면 commands 우선
        # 칼퇴 [훈수꾼 설정] 탭 버튼이 부르는 명령(계약 2). 쪽 이름은 Core/SettingsPages와 같아야 한다(docs/22 12장).
        actions = [ordered]@{
            'wizard' = @('--settings', 'wizard'); 'settings.modules' = @('--settings', 'modules')
            'settings.look' = @('--settings', 'look'); 'settings.options' = @('--settings', 'options')
            'settings.profiles' = @('--settings', 'profiles'); 'settings.general' = @('--settings', 'general')
        }
    }
    # 칼퇴 소개 화면 탭(계약 2, 칼퇴 docs/11 4장): 모양은 칼퇴가 그리고 훈수꾼은 무엇을 어떤 순서로만 정한다.
    layout = [ordered]@{ tabs = @(
        [ordered]@{ id = 'modules' }
        [ordered]@{ id = 'hunsu'; title = '훈수꾼 설정'; kind = 'buttons'; summary = '훈수꾼 설정 창의 원하는 쪽을 바로 열어요. 꺼져 있으면 설정 창만 떠요.'
            groups = @(
                [ordered]@{ title = '처음이라면'; buttons = @(
                    [ordered]@{ id = 'wizard'; label = '설정 마법사'; tip = '하는 일·자리·모양을 1분 안에 맞춰요'; emphasis = 'primary'; action = 'wizard' }) }
                [ordered]@{ title = '바로 가기'; buttons = @(
                    [ordered]@{ id = 'modules'; label = '모듈 보이기·순서'; tip = '훈수꾼 안에서 모듈을 보이고 숨기고 묶어요'; action = 'settings.modules' }
                    [ordered]@{ id = 'look'; label = '모양 바꾸기'; tip = '패널 룩·테마·아이콘'; action = 'settings.look' }
                    [ordered]@{ id = 'options'; label = '모듈 옵션'; tip = '모듈마다 세부 옵션'; action = 'settings.options' }
                    [ordered]@{ id = 'profiles'; label = '프로필'; tip = '정산·검수 같은 일에 맞는 모듈만 켜요'; action = 'settings.profiles' }
                    [ordered]@{ id = 'general'; label = '일반 설정'; tip = '테마·패널 너비·배치 잠금·시작 동작'; action = 'settings.general' }) }
            ) }
        [ordered]@{ id = 'guide' }
        [ordered]@{ id = 'changes' }
    ) }
}
# 하위 모듈 목록: 모듈 선언 하나가 출처다(이름·설명을 두 번 적지 않음). 설명이 길어 첫 문장만 한 줄 설명으로 쓴다.
$subs = foreach ($d in Get-ChildItem (Join-Path $repo 'modules') -Directory) {
    $decl = Join-Path $d.FullName "$($d.Name).hunsu.json"
    if (-not (Test-Path $decl)) { continue }
    $m = Get-Content $decl -Raw -Encoding utf8 | ConvertFrom-Json
    if ($m.id -ne $d.Name) { Write-Host "[중단] 모듈 폴더 이름과 id가 다릅니다: $($d.Name)" -ForegroundColor Red; exit 1 }
    if ($m.id -notmatch '^[A-Za-z0-9_-]{1,64}$') { Write-Host "[중단] 칼퇴 규격에 안 맞는 모듈 id: $($m.id)" -ForegroundColor Red; exit 1 }
    $first = ([string]$m.description -split '(?<=다\.)\s')[0].Trim()
    [pscustomobject]@{ order = [int]$m.order; entry = [ordered]@{ id = $m.id; name = $m.name; summary = $first; default = $true } }
}
$subs = @($subs | Sort-Object order | ForEach-Object { $_.entry })
if ($subs.Count -gt 64) { Write-Host "[중단] 하위 모듈이 64개를 넘습니다(칼퇴 규격): $($subs.Count)" -ForegroundColor Red; exit 1 }
$manifest.modules = $subs
# 칼퇴 계기판 값(memoryMb·startMs, 칼퇴 docs/10 3장): 지어내지 않고 잰 값만. assets\load.json이 있을 때만 넣는다.
# 값은 훈수꾼 기록(--log)의 load 줄(startMs, memoryMb)을 PC에서 몇 번 재 보고 큰 쪽으로 적는다(docs/22 5장).
$loadFile = Join-Path $repo 'assets\load.json'
if (Test-Path $loadFile) {
    $load = Get-Content $loadFile -Raw -Encoding utf8 | ConvertFrom-Json
    if ($null -ne $load.memoryMb) {
        $mb = [double]$load.memoryMb
        if ($mb -lt 0 -or $mb -gt 100000) { Write-Host "[중단] load.json memoryMb 값이 칼퇴 범위(0~100000)를 벗어납니다: $mb" -ForegroundColor Red; exit 1 }
        $manifest.memoryMb = $mb
    }
    if ($null -ne $load.startMs) {
        $ms = [int]$load.startMs
        if ($ms -lt 0 -or $ms -gt 3600000) { Write-Host "[중단] load.json startMs 값이 칼퇴 범위(0~3600000)를 벗어납니다: $ms" -ForegroundColor Red; exit 1 }
        $manifest.startMs = $ms
    }
}
$utf8 = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllText((Join-Path $stage 'hunsu.app.json'), ($manifest | ConvertTo-Json -Depth 12), $utf8)
foreach ($g in 'hunsu.guide.md', 'hunsu.changes.md') {
    $src = Join-Path $repo "assets\guide\$g"
    if (-not (Test-Path $src)) { Write-Host "[중단] $src 가 없습니다(칼퇴 안내문 규칙)." -ForegroundColor Red; exit 1 }
    Copy-Item $src (Join-Path $stage $g)
}
$icon = Join-Path $repo 'assets\icon\hunsu.ico'   # 칼퇴 레일 아이콘(칼퇴 docs/11 8장). 없으면 칼퇴는 글자 아이콘을 쓰지만, 있어야 할 파일이라 없으면 멈춘다
if (-not (Test-Path $icon)) { Write-Host "[중단] $icon 이 없습니다(칼퇴 레일 아이콘)." -ForegroundColor Red; exit 1 }
Copy-Item $icon (Join-Path $stage 'hunsu.ico')
Copy-Item (Join-Path $repo 'LICENSE.txt') (Join-Path $stage 'LICENSE.txt')
[IO.File]::WriteAllText((Join-Path $stage 'README.txt'), "훈수꾼 $Version`r`n칼퇴 좌측 레일에서 설치·켜기·끄기를 합니다. 직접 실행: app\ClockOut.Hunsu.exe`r`n.NET 런타임을 따로 설치하지 않아도 됩니다(self-contained).`r`n", $utf8)

Write-Host "== 4) zip과 SHA-256"
$zip = Join-Path $out 'hunsu.zip'
Remove-Item $zip -ErrorAction SilentlyContinue
# 항목 이름은 어떤 PowerShell에서도 '/'로 쓴다(5.1의 CreateFromDirectory는 '\'로 써서 칼퇴가 거부했다, docs/06).
$zs = [IO.Compression.ZipFile]::Open($zip, 'Create')
try {
    $root = (Resolve-Path $stage).Path.TrimEnd('\')
    foreach ($f in Get-ChildItem $stage -Recurse -File) {
        $entryName = $f.FullName.Substring($root.Length + 1).Replace('\', '/')
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zs, $f.FullName, $entryName, [IO.Compression.CompressionLevel]::Optimal)
    }
} finally { $zs.Dispose() }
pwsh -NoProfile -File (Join-Path $repo 'tools\check_guide.ps1') -Zip $zip -Version $Version
if ($LASTEXITCODE -ne 0) { Remove-Item $zip -ErrorAction SilentlyContinue; Write-Host '[중단] 안내문 검사 실패: 패치노트 맨 위 항목과 hunsu.guide.md를 고치세요(assets\guide).' -ForegroundColor Red; exit 1 }
$sha = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
$size = (Get-Item $zip).Length
"$sha  hunsu.zip" | Set-Content -Encoding ascii (Join-Path $out 'hunsu.zip.sha256')
$files = (Get-ChildItem $stage -Recurse -File).Count
# 한 판 = 한 SHA. 장부에 같은 판이 다른 SHA로 있으면 이미 내보낸 판이므로 판을 올려야 한다.
$ledger = Join-Path $repo 'releases\hunsu_sha256.md'
if (Test-Path $ledger) {
    $row = Select-String -Path $ledger -Pattern "^\|\s*$([regex]::Escape($Version))\s*\|" | Select-Object -First 1
    if ($row -and $row.Line -notmatch $sha) {
        Remove-Item $zip -ErrorAction SilentlyContinue
        Write-Host "[중단] 판 $Version 은 SHA 장부에 다른 SHA로 이미 있습니다(releases\hunsu_sha256.md). 판을 올려 다시 만드세요." -ForegroundColor Red; exit 1
    }
}
$commit = (git -C $repo rev-parse --short HEAD 2>$null)
$record = [ordered]@{
    id = 'hunsu'; version = $Version; file = 'hunsu.zip'; sha256 = $sha; size = $size
    tag = "app-hunsu-$Version"; built = (Get-Date).ToString('yyyy-MM-dd'); commit = "$commit"
    ledgerRow = "| $Version | $((Get-Date).ToString('yyyy-MM-dd')) | $sha | $size | app-hunsu-$Version |"
}
[IO.File]::WriteAllText((Join-Path $out 'release-record.json'), ($record | ConvertTo-Json -Depth 3), $utf8)
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
Write-Host "  SHA 장부 한 줄(Release를 올린 뒤 releases\hunsu_sha256.md에 더함): $($record.ledgerRow)"
Write-Host "  카탈로그 apps 항목 예시: $(Join-Path $out 'catalog-entry.json') (Release 태그 app-hunsu-$Version, 올리기는 사용자가 직접)"
exit 0
