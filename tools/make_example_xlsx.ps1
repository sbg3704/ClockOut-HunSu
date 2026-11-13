<#
기능: 훈수꾼을 눈으로 확인할 예제 통합 문서(out\훈수꾼_확인용.xlsx)를 만든다. 보이지 않는 엑셀(Visible=False)에서 만들고 저장한 뒤 닫는다.
      시트: 안내(무엇을 눌러 볼지) / 매출(숫자 표와 합계 수식) / 섞임(숫자·글자·날짜·빈칸·수식·오류) / 큰 표(2만 칸, 열 전체 선택 시험)
사용법: pwsh -File tools\make_example_xlsx.ps1 [-Out <파일>]
의존: 엑셀 데스크톱판. 이 스크립트가 띄운 엑셀만 닫는다. 키보드·화면 조작 없음.
#>
param([string]$Out)
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [Text.Encoding]::UTF8 } catch {}
$repo = Split-Path -Parent $PSScriptRoot
if (-not $Out) { $Out = Join-Path $repo 'out\훈수꾼_확인용.xlsx' }
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Out) | Out-Null
Add-Type -Namespace HunsuEx -Name Win -MemberDefinition '[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(System.IntPtr hWnd, out uint pid);' -ErrorAction SilentlyContinue

# PowerShell 7 COM에서 Range('..').Value2 = 숫자·배열을 한 줄로 쓰면 형 변환 오류가 나서, 범위를 변수로 받아 넣는다.
function Put($range, $value) { try { [void]$range.GetType().InvokeMember('Value2', [Reflection.BindingFlags]::SetProperty, $null, $range, @(,$value)) } catch { throw "값 넣기 실패($($range.Address())): $($_.Exception.InnerException.Message)" } }

$xl = $null; $xlPid = 0
try {
    $xl = New-Object -ComObject Excel.Application
    $p = [uint32]0; [void][HunsuEx.Win]::GetWindowThreadProcessId([IntPtr]$xl.Hwnd, [ref]$p); $xlPid = [int]$p
    $xl.Visible = $false; $xl.DisplayAlerts = $false
    $wb = $xl.Workbooks.Add()
    while ($wb.Worksheets.Count -lt 4) { $null = $wb.Worksheets.Add([Type]::Missing, $wb.Worksheets.Item($wb.Worksheets.Count)) }
    $s1 = $wb.Worksheets.Item(1); $s1.Name = '안내'
    $s2 = $wb.Worksheets.Item(2); $s2.Name = '매출'
    $s3 = $wb.Worksheets.Item(3); $s3.Name = '섞임'
    $s4 = $wb.Worksheets.Item(4); $s4.Name = '큰 표'

    # --- 안내 ---
    $lines = @(
        '훈수꾼 확인용 예제',
        '',
        '1. 훈수꾼을 켭니다(칼퇴 레일 [켜기], 또는 tools\try_hunsu.ps1).',
        '2. 엑셀 창 오른쪽 위에 "훈수꾼" 알약과 "선택 요약" 패널이 붙는지 봅니다.',
        '3. [매출] 시트에서 B3:F14를 고르면 합계·평균·최소·최대가 바로 바뀝니다.',
        '4. [섞임] 시트에서 A2:A12를 고르면 숫자·글자·수식 개수와 "오류 n" 배지가 보입니다.',
        '5. [큰 표] 시트에서 A열 머리글을 눌러 열 전체를 골라 봅니다(104만 칸이어도 바로 나옴).',
        '6. 패널 제목줄: ◐ 투명도(평소 흐림, 올리면 선명) / 핀 = 떼어 내기 / ∧ = 접기(오른쪽 손잡이로).',
        '7. 떼어 낸 창은 평소 클릭이 지나갑니다. Ctrl+Alt+H를 누르면 끌어 옮기거나 핀 단추로 되돌릴 수 있습니다.',
        '8. Ctrl+Alt+Shift+H: 전부 숨기기(한 번 더 누르면 다시 보임).',
        '9. 엑셀 창을 끌고, 크기를 바꾸고, 최소화해 보세요. 훈수꾼이 따라갑니다.',
        '10. 끄기: 작업 표시줄 오른쪽 "훈" 아이콘 → [훈수꾼 끄기]. 다시 켜면 같은 자리에 나옵니다.',
        '',
        '이 파일의 숫자는 시험용 가짜 자료입니다. 훈수꾼은 셀 값을 바꾸지 않습니다.'
    )
    for ($i = 0; $i -lt $lines.Count; $i++) { $s1.Cells.Item($i + 1, 1).Value2 = $lines[$i] }
    $s1.Range('A1').Font.Size = 16; $s1.Range('A1').Font.Bold = $true
    $s1.Columns.Item(1).ColumnWidth = 110

    # --- 매출: 12달 × 5팀 ---
    $teams = '영업1팀', '영업2팀', '기술팀', '지원팀', '해외팀'
    Put $s2.Range('A1') '월별 매출(만 원)'; $s2.Range('A1').Font.Bold = $true
    Put $s2.Range('A2') ('월')
    # 칸 하나씩 넣지 않고 배열로 한 번에 넣는다(PowerShell COM에서 Cells.Item(...).Value2 대입이 숫자를 거부하는 문제도 피함).
    $head = New-Object 'object[,]' 1, 6
    for ($t = 0; $t -lt 5; $t++) { $head[0, $t] = $teams[$t] }
    $head[0, 5] = '합계'
    Put $s2.Range('B2:G2') ($head)
    $months = New-Object 'object[,]' 12, 1
    for ($m = 0; $m -lt 12; $m++) { $months[$m, 0] = "$($m + 1)월" }
    Put $s2.Range('A3:A14') ($months)
    # 숫자는 PowerShell 배열로 넘기면 글자로 들어가서(확인함), 수식으로 채운 뒤 값으로 굳힌다(고정된 가짜 자료).
    $sales = $s2.Range('B3:F14')
    $sales.Formula = '=ROUND(800+MOD(ROW()*131+COLUMN()*57,1700)+MOD(ROW()*COLUMN(),10)/10,1)'
    $salesValues = $sales.Value2
    Put $sales $salesValues
    $s2.Range('G3:G14').Formula = '=SUM(B3:F3)'   # 상대 참조라 줄마다 맞게 바뀐다
    Put $s2.Range('A15') ('합계')
    foreach ($c in 'B', 'C', 'D', 'E', 'F', 'G') { $s2.Range("${c}15").Formula = "=SUM(${c}3:${c}14)" }
    Put $s2.Range('A16') ('평균')
    foreach ($c in 'B', 'C', 'D', 'E', 'F', 'G') { $s2.Range("${c}16").Formula = "=AVERAGE(${c}3:${c}14)" }
    $s2.Range('A2:G2').Font.Bold = $true; $s2.Range('A15:G16').Font.Bold = $true
    $s2.Range('B3:G16').NumberFormat = '#,##0.0'

    # --- 섞임: 여러 종류 ---
    Put $s3.Range('A1') '여러 종류(숫자·글자·날짜·빈칸·수식·오류)'; $s3.Range('A1').Font.Bold = $true
    Put $s3.Range('A2') (120)
    Put $s3.Range('A3') (45.5)
    Put $s3.Range('A4') ('글자')
    $s3.Range('A5').Formula = '=DATE(2026,11,12)'
    # A6은 빈칸
    $s3.Range('A7').Formula = '=A2*2'
    $s3.Range('A8').Formula = '=A2/0'
    $s3.Range('A9').Formula = '=NA()'
    $s3.Range('A10').Formula = '=VLOOKUP("없음",A2:A4,1,FALSE)'
    Put $s3.Range('A11') (-30)
    Put $s3.Range('A12') ('메모')
    Put $s3.Range('B2') ('← A2:A12를 골라 보세요: 오류 3개, 수식 5개')
    $s3.Columns.Item(1).ColumnWidth = 14

    # --- 큰 표: 2000행 × 10열 ---
    # PowerShell에서 큰 배열을 넘기면 엑셀이 거부한다(0x800A03EC). 수식으로 채운 뒤 값으로 굳힌다.
    $big = $s4.Range('A1:J2000')
    $big.Formula = '=ROUND(MOD(ROW()*37+COLUMN()*101,997)+COLUMN()/10,2)'
    $frozen = $big.Value2
    Put $big $frozen

    $s1.Activate()
    $wb.SaveAs($Out, 51)   # 51 = xlsx
    $wb.Close($false)
    Write-Host "만들었습니다: $Out"
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
