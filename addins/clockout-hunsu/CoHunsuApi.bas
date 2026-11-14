Attribute VB_Name = "CoHunsuApi"
'==============================================================
' 기능: 틀(훈수꾼)이 Application.Run으로 부르는 진입점 3개(Hello, Query, Do). 이름으로 조회·명령 파일에 나눠 줄 뿐이다.
'       조회: hello, selection.summary, bridge.stats, errors, flow.neighbors, sheets, book.info, book.audit, names.  명령: sheet.activate, goto.
' 사용법: Application.Run "'clockout-hunsu.xlam'!CoHunsu_Query", "selection.summary", "{}"
'         인자와 반환은 모두 JSON 문자열. 어떤 경우에도 오류 창을 띄우지 않는다(docs/02 1장).
' 의존: CoHunsuJson, CoHunsuEntry, CoHunsuQ_SelectionSummary, CoHunsuQ_BridgeStats, CoHunsuQ_Errors, CoHunsuQ_Flow, CoHunsuQ_Sheets, CoHunsuQ_Book, CoHunsuQ_Names
' 참고: docs/02 2장은 진입점을 .cls로 적었으나 Application.Run은 표준 모듈의 프로시저만 부를 수 있어 .bas로 둔다(0단계 확인).
'==============================================================
Option Explicit

Public Const HUNSU_VERSION As String = "0.1.0"
Public Const HUNSU_PROTO As Long = 0

Public Function CoHunsu_Hello() As String
    On Error GoTo Fail
    CoHunsu_Start
    CoHunsu_Hello = CoHunsuJson_Ok(HelloData())
    Exit Function
Fail:
    CoHunsu_Hello = CoHunsuJson_Fail("query_failed", "hello 처리 중 오류: " & Err.Description)
End Function

' 조회는 읽기 전용이라 도는 동안 생긴 엑셀 이벤트는 모두 조회 자신이 일으킨 것이다(SpecialCells 등). 신호를 끄고 부른다.
Public Function CoHunsu_Query(ByVal qName As String, ByVal argsJson As String) As String
    CoHunsuMail_Mute True
    CoHunsu_Query = QueryInner(qName, argsJson)
    CoHunsuMail_Mute False
End Function

Private Function QueryInner(ByVal qName As String, ByVal argsJson As String) As String
    On Error GoTo Fail
    Select Case qName
        Case "hello"
            QueryInner = CoHunsu_Hello()
        Case "selection.summary"
            QueryInner = Query_SelectionSummary(argsJson)
        Case "bridge.stats"
            QueryInner = Query_BridgeStats(argsJson)
        Case "errors"
            QueryInner = Query_Errors(argsJson)
        Case "flow.neighbors"
            QueryInner = Query_FlowNeighbors(argsJson)
        Case "sheets"
            QueryInner = Query_Sheets(argsJson)
        Case "book.info"
            QueryInner = Query_BookInfo(argsJson)
        Case "book.audit"
            QueryInner = Query_BookAudit(argsJson)
        Case "names"
            QueryInner = Query_Names(argsJson)
        Case Else
            QueryInner = CoHunsuJson_Fail("query_unknown", "다리에 없는 조회: " & qName)
    End Select
    Exit Function
Fail:
    QueryInner = CoHunsuJson_Fail("query_failed", "조회 중 오류: " & Err.Description)
End Function

Public Function CoHunsu_Do(ByVal cmdName As String, ByVal argsJson As String) As String
    On Error GoTo Fail
    Select Case cmdName
        Case "sheet.activate"
            CoHunsu_Do = Do_SheetActivate(argsJson)
        Case "goto"
            CoHunsu_Do = Do_Goto(argsJson)
        Case Else
            CoHunsu_Do = CoHunsuJson_Fail("query_unknown", "다리에 없는 명령: " & cmdName)
    End Select
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
        CoHunsuJson_Pair("queries", "[""hello"",""selection.summary"",""bridge.stats"",""errors"",""flow.neighbors"",""sheets"",""book.info"",""book.audit"",""names""]") & "," & _
        CoHunsuJson_Pair("commands", "[""sheet.activate"",""goto""]") & "," & _
        CoHunsuJson_Pair("signals", "[""sel"",""change"",""sheet"",""book"",""calc""]") & "," & _
        CoHunsuJson_Pair("office", "{" & CoHunsuJson_Pair("version", CoHunsuJson_Str(Application.Version)) & "," & _
            CoHunsuJson_Pair("bits", CStr(bits)) & "}") & "}"
End Function
