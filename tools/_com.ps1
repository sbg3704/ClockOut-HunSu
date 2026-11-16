<#
기능: 엑셀 COM 호출이 "피호출자가 호출을 거부했습니다(RPC_E_CALL_REJECTED 0x80010001)" 또는 0x8001010A로 거부되면 잠시 기다렸다 다시 부른다. 엑셀이 막 켜졌거나 통합 문서를 여는 중이면 바빠서 생기는 일시적인 거부다.
사용법: . "$PSScriptRoot\_com.ps1";  $wb = Invoke-ComRetry { $xl.Workbooks.Open($path) }
의존: 없음. 키보드·화면 조작 없음. 최대 약 30초(0.5초 간격 60번) 기다린 뒤에도 거부되면 원래 오류를 그대로 던진다.
#>
function Invoke-ComRetry([scriptblock]$Body, [int]$Tries = 60, [int]$DelayMs = 500) {
    for ($i = 1; ; $i++) {
        try { return (& $Body) }
        catch {
            $hr = 0
            if ($_.Exception -is [System.Runtime.InteropServices.COMException]) { $hr = $_.Exception.ErrorCode }
            elseif ($_.Exception.InnerException -is [System.Runtime.InteropServices.COMException]) { $hr = $_.Exception.InnerException.ErrorCode }
            $busy = ($hr -eq -2147418111 -or $hr -eq -2147417846) -or ($_.Exception.Message -match '0x80010001|0x8001010A|RPC_E_CALL_REJECTED|RPC_E_SERVERCALL_RETRYLATER')
            if (-not $busy -or $i -ge $Tries) { throw }
            Start-Sleep -Milliseconds $DelayMs
        }
    }
}
