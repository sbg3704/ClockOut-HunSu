# 기능: 훈수꾼 묶음(zip)이 칼퇴 하위 프로그램 안내문 규칙(claude-workspace docs/하위프로그램_안내문_규칙.md)을 지켰는지 검사한다. 어기면 종료 코드 1.
#       검사: 맨 위 hunsu.app.json의 version(X.Y.Z, 앞 v 없음, 기대한 판과 같음) / hunsu.guide.md·hunsu.changes.md가 같은 자리(맨 위)에 있음 /
#             UTF-8, 256KB 이하, 줄 600개 이하 / HTML·표·링크·그림·중첩 글머리 없음, 코드 울타리 닫힘 / 패치노트 맨 위가 "## v<이번 판> (날짜)"이고 판이 내림차순.
# 사용법: pwsh -File tools\check_guide.ps1 -Zip out\pack\hunsu.zip [-Version 0.0.162]     (tools\pack_app.ps1이 zip을 만든 직후 자동으로 부른다)
# 의존: pwsh 7만(파이썬 필요 없음). 파일을 바꾸지 않는다.
param([Parameter(Mandatory)][string]$Zip, [string]$Version = '')
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [Text.Encoding]::UTF8 } catch {}
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$errors = New-Object Collections.Generic.List[string]
function Fail([string]$m) { $script:errors.Add($m) }
$maxBytes = 256KB
$maxLines = 600

function Read-Entry($za, [string]$name) {
    $e = $za.Entries | Where-Object { $_.FullName -eq $name } | Select-Object -First 1
    if (-not $e) { return $null }
    $ms = New-Object IO.MemoryStream
    $s = $e.Open(); try { $s.CopyTo($ms) } finally { $s.Dispose() }
    return ,$ms.ToArray()
}
function Decode([byte[]]$bytes, [string]$label) {
    if ($bytes.Length -gt $maxBytes) { Fail "$label 가 256KB를 넘습니다($($bytes.Length) 바이트)."; return $null }
    $enc = New-Object Text.UTF8Encoding($false, $true)
    $off = 0
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) { $off = 3 }
    try { return $enc.GetString($bytes, $off, $bytes.Length - $off) } catch { Fail "$label 가 UTF-8이 아닙니다."; return $null }
}
function Check-Markdown([string]$text, [string]$label) {
    $lines = $text -split "`r?`n"
    if (($lines | Where-Object { $_.Trim() -ne '' }).Count -gt $maxLines) { Fail "$label 의 내용 줄이 $maxLines 개를 넘습니다(칼퇴가 그릴 조각 최대 600개)." }
    $inCode = $false; $n = 0
    foreach ($l in $lines) {
        $n++
        if ($l -match '^\s*```') { $inCode = -not $inCode; continue }
        if ($inCode) { continue }
        if ($l -match '</?[A-Za-z!][^>]*>') { Fail "$label ${n}줄: HTML은 지원하지 않습니다." }
        if ($l -match '^\s*\|.*\|\s*$' -or $l -match '^\s*\|?\s*:?-{3,}:?\s*\|') { Fail "$label ${n}줄: 표는 지원하지 않습니다." }
        if ($l -match '!\[[^\]]*\]\(' -or $l -match '\[[^\]]+\]\([^)]*\)' -or $l -match '(?i)https?://|\bwww\.') { Fail "$label ${n}줄: 링크·그림·주소는 지원하지 않습니다." }
        if ($l -match '^\s+[-*]\s') { Fail "$label ${n}줄: 글머리는 한 단계만 지원합니다(들여쓰기 금지)." }
    }
    if ($inCode) { Fail ($label + ' : 코드 울타리(세 개의 역따옴표)가 닫히지 않았습니다.') }
}

if (-not (Test-Path $Zip)) { Write-Host "[실패] zip이 없습니다: $Zip"; exit 1 }
$za = [IO.Compression.ZipFile]::OpenRead((Resolve-Path $Zip).Path)
try {
    if ($za.Entries | Where-Object { $_.FullName -like '*\*' }) { Fail '묶음 항목 이름에 역슬래시(\)가 있습니다(슬래시 / 로 써야 칼퇴가 읽습니다).' }
    $mb = Read-Entry $za 'hunsu.app.json'
    $ver = ''
    if ($null -eq $mb) { Fail '묶음 맨 위에 hunsu.app.json이 없습니다.' }
    else {
        $mt = Decode $mb 'hunsu.app.json'
        try { $man = $mt | ConvertFrom-Json } catch { $man = $null; Fail 'hunsu.app.json이 JSON이 아닙니다.' }
        if ($man) {
            $ver = "$($man.version)"
            if ($ver -notmatch '^\d+\.\d+\.\d+$') { Fail "app.json version이 X.Y.Z 모양이 아닙니다(앞에 v 금지): '$ver'" }
            if ($Version -and $ver -ne $Version) { Fail "app.json version($ver)이 이번 판($Version)과 다릅니다." }
        }
    }
    foreach ($spec in @(@('hunsu.guide.md', '사용법'), @('hunsu.changes.md', '패치노트'))) {
        $b = Read-Entry $za $spec[0]
        if ($null -eq $b) { Fail "묶음 맨 위에 $($spec[0]) ($($spec[1]))가 없습니다."; continue }
        $t = Decode $b $spec[0]
        if ($null -eq $t) { continue }
        if ($t.Trim().Length -eq 0) { Fail "$($spec[0]) 가 비었습니다."; continue }
        Check-Markdown $t $spec[0]
        if ($spec[0] -eq 'hunsu.changes.md') {
            $vers = @([regex]::Matches($t, '(?m)^##\s+v(\d+)\.(\d+)\.(\d+)\s+\((\d{4}-\d{2}-\d{2})\)\s*$') | ForEach-Object { [version]::new([int]$_.Groups[1].Value, [int]$_.Groups[2].Value, [int]$_.Groups[3].Value) })
            if ($vers.Count -eq 0) { Fail 'hunsu.changes.md에 "## v0.0.000 (YYYY-MM-DD)" 모양의 판 제목이 없습니다.' }
            else {
                $top = '{0}.{1}.{2:D3}' -f $vers[0].Major, $vers[0].Minor, $vers[0].Build
                if ($ver -and ([version]$ver) -ne $vers[0]) { Fail "패치노트 맨 위 판(v$top)이 app.json version($ver)과 다릅니다. 판을 올리면 패치노트 맨 위에 항목을 더하세요." }
                for ($i = 1; $i -lt $vers.Count; $i++) { if ($vers[$i] -ge $vers[$i - 1]) { Fail "패치노트 판이 내림차순(최신이 위)이 아닙니다: v$($vers[$i - 1]) 다음 v$($vers[$i])"; break } }
            }
            $allHeads = [regex]::Matches($t, '(?m)^##\s+v\S*.*$') | ForEach-Object { $_.Value }
            if ($allHeads.Count -ne $vers.Count) { Fail '패치노트에 "## v<판> (날짜)" 모양이 아닌 판 제목이 있습니다.' }
        }
    }
} finally { $za.Dispose() }

if ($errors.Count -gt 0) {
    Write-Host '[실패] 안내문 규칙을 어겼습니다(claude-workspace docs/하위프로그램_안내문_규칙.md):' -ForegroundColor Red
    $errors | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}
Write-Host "안내문 검사 통과: 판 $ver, 사용법·패치노트 모두 규칙대로입니다."
exit 0
