Attribute VB_Name = "CoHunsuApi"
'==============================================================
' 기능: 틀(훈수꾼)이 Application.Run으로 부르는 진입점 3개(Hello, Query, Do). 이름으로 조회·명령 파일에 나눠 줄 뿐이다.
' 사용법: Application.Run "'clockout-hunsu.xlam'!CoHunsu_Query", "selection.summary", "{}"
'         인자와 반환은 모두 JSON 문자열. 어떤 경우에도 오류 창을 띄우지 않는다(docs/02 1장).
' 의존: CoHunsuJson, CoHunsuEntry, CoHunsuQ_SelectionSummary, CoHunsuQ_BridgeStats
' 참고: docs/02 2장은 진입점을 .cls로 적었으나 Application.Run은 표준 모듈의 프로시저만 부를 수 있어 .bas로 둔다(0단계 확인).
'==============================================================
Option Explicit

Public Const HUNSU_VERSION As String = "0.0.1"
Public Const HUNSU_PROTO As Long = 0

Public Function CoHunsu_Hello() As String
    On Error GoTo Fail
    CoHunsu_Start
    CoHunsu_Hello = CoHunsuJson_Ok(HelloData())
    Exit Function
Fail:
    CoHunsu_Hello = CoHunsuJson_Fail("query_failed", "hello 처리 중 오류: " & Err.Description)
End Function

Public Function CoHunsu_Query(ByVal qName As String, ByVal argsJson As String) As String
    On Error GoTo Fail
    Select Case qName
        Case "hello"
            CoHunsu_Query = CoHunsu_Hello()
        Case "selection.summary"
            CoHunsu_Query = Query_SelectionSummary(argsJson)
        Case "bridge.stats"
            CoHunsu_Query = Query_BridgeStats(argsJson)
        Case Else
            CoHunsu_Query = CoHunsuJson_Fail("query_unknown", "다리에 없는 조회: " & qName)
    End Select
    Exit Function
Fail:
    CoHunsu_Query = CoHunsuJson_Fail("query_failed", "조회 중 오류: " & Err.Description)
End Function

Public Function CoHunsu_Do(ByVal cmdName As String, ByVal argsJson As String) As String
    On Error GoTo Fail
    ' 0단계 최소판에는 명령이 없다(goto, sheet.activate는 3단계).
    CoHunsu_Do = CoHunsuJson_Fail("query_unknown", "다리에 없는 명령: " & cmdName)
    Exit Function
Fail:
    CoHunsu_Do = CoHunsuJson_Fail("query_failed", "명령 중 오류: " & Err.Description)
End Function

Private Function HelloData() As String
    Dim bits As Long
#If Win64 Then
    bits = 64
#Else
    bits = 32
#End If
    HelloData = "{" & CoHunsuJson_Pair("version", CoHunsuJson_Str(HUNSU_VERSION)) & "," & _
        CoHunsuJson_Pair("proto", CStr(HUNSU_PROTO)) & "," & _
        CoHunsuJson_Pair("queries", "[""hello"",""selection.summary"",""bridge.stats""]") & "," & _
        CoHunsuJson_Pair("commands", "[]") & "," & _
        CoHunsuJson_Pair("signals", "[""sel""]") & "," & _
        CoHunsuJson_Pair("office", "{" & CoHunsuJson_Pair("version", CoHunsuJson_Str(Application.Version)) & "," & _
            CoHunsuJson_Pair("bits", CStr(bits)) & "}") & "}"
End Function
