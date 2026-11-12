Attribute VB_Name = "CoHunsuQ_BridgeStats"
'==============================================================
' 기능: 진단 조회 bridge.stats. 이벤트 처리 시간 표본(마이크로초)과 보낸 신호 수를 돌려준다(0단계 측정용).
' 사용법: CoHunsu_Query "bridge.stats", "{}"   또는 "{""reset"":true}"로 표본 지우기
' 의존: CoHunsuMail, CoHunsuJson. 셀을 읽지 않는다.
'==============================================================
Option Explicit

Public Function Query_BridgeStats(ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim data As String
    data = CoHunsuMail_StatsJson()
    If InStr(1, argsJson, """reset"":true", vbBinaryCompare) > 0 Then CoHunsuMail_ResetStats
    Query_BridgeStats = CoHunsuJson_Ok(data)
    Exit Function
Fail:
    Query_BridgeStats = CoHunsuJson_Fail("query_failed", "bridge.stats 오류: " & Err.Description)
End Function
