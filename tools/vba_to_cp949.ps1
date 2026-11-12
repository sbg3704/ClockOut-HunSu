<#
기능: addins/clockout-hunsu 의 VBA 소스(.bas, .cls, ThisWorkbook_*.txt)를 CP949 + CRLF로 맞춘다(docs/02 9장).
      UTF-8로 저장된 파일만 바꾸고, 이미 CP949인 파일은 줄바꿈만 CRLF로 맞춘다.
사용법: pwsh -File tools\vba_to_cp949.ps1            바꾸기
        pwsh -File tools\vba_to_cp949.ps1 -Check     바꾸지 않고 검사만(UTF-8이나 LF가 남아 있으면 종료 코드 1)
의존: .NET 인코딩(CodePagesEncodingProvider). 편집기로 고칠 때는 UTF-8로 고친 뒤 이 스크립트를 돌린다.
#>
param([string]$Dir, [switch]$Check)
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [Text.Encoding]::UTF8 } catch {}
try { [Text.Encoding]::RegisterProvider([Text.CodePagesEncodingProvider]::Instance) } catch {}
$repo = Split-Path -Parent $PSScriptRoot
if (-not $Dir) { $Dir = Join-Path $repo 'addins\clockout-hunsu' }
$utf8Strict = New-Object Text.UTF8Encoding($false, $true)
$cp949 = [Text.Encoding]::GetEncoding(949, (New-Object Text.EncoderExceptionFallback), (New-Object Text.DecoderExceptionFallback))
$bad = 0
$files = Get-ChildItem $Dir -File | Where-Object { $_.Extension -in '.bas', '.cls' -or $_.Name -like 'ThisWorkbook_*.txt' }
foreach ($f in $files) {
    $bytes = [IO.File]::ReadAllBytes($f.FullName)
    $hasHigh = $false; foreach ($b in $bytes) { if ($b -ge 0x80) { $hasHigh = $true; break } }
    $isUtf8 = $false
    if ($hasHigh) { try { $null = $utf8Strict.GetString($bytes); $isUtf8 = $true } catch { $isUtf8 = $false } }
    $text = if ($isUtf8) { $utf8Strict.GetString($bytes).TrimStart([char]0xFEFF) } else { $cp949.GetString($bytes) }
    $crlf = ($text -replace "`r`n", "`n") -replace "`n", "`r`n"
    $needs = $isUtf8 -or ($crlf -ne $text)
    if ($Check) {
        if ($needs) { Write-Host "[고칠 것] $($f.Name): $(if ($isUtf8) {'UTF-8'} else {'LF 줄바꿈'})" -ForegroundColor Yellow; $bad++ }
        continue
    }
    if (-not $needs) { continue }
    try { $out = $cp949.GetBytes($crlf) } catch { Write-Host "[중단] $($f.Name): CP949로 쓸 수 없는 글자가 있습니다." -ForegroundColor Red; exit 1 }
    [IO.File]::WriteAllBytes($f.FullName, $out)
    Write-Host "CP949+CRLF로 바꿈: $($f.Name)"
}
if ($Check -and $bad -gt 0) { exit 1 }
Write-Host "VBA 소스 $($files.Count)개 확인 완료."
exit 0
