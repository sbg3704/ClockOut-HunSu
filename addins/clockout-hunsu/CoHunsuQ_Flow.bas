Attribute VB_Name = "CoHunsuQ_Flow"
'==============================================================
' 기능: 조회 flow.neighbors. 활성 셀이 바로 쓰는 칸(앞쪽, precedents)과 활성 셀을 바로 쓰는 칸(뒤쪽, dependents)을 같은 시트 안에서 돌려준다.
' 사용법: CoHunsu_Query "flow.neighbors", "{}"
'         -> {"sheet":"매출","addr":"C5","formula":"=A5/B5","hasFormula":true,"value":"2","crossSheet":false,
'             "precedents":{"count":2,"items":[{"addr":"A5","value":"10"}],"truncated":false},"dependents":{...}}
' 의존: CoHunsuJson. 읽기 전용(선택을 바꾸지 않음). 한계: DirectPrecedents와 DirectDependents는 다른 시트나 다른 통합 문서의 칸을 주지 않는다.
'       수식에 "!"가 있으면 crossSheet:true로 알려 화면이 "다른 시트도 씀"을 표시할 수 있게 한다(docs/02 5장 [0단계 확인] 한계).
'==============================================================
Option Explicit

Private Const ITEM_LIMIT As Long = 20
Private Const FORMULA_MAX As Long = 200

Public Function Query_FlowNeighbors(ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim c As Range, ws As Worksheet, f As String, hasF As Boolean
    On Error Resume Next
    Set c = ActiveCell
    On Error GoTo Fail
    If c Is Nothing Then
        Query_FlowNeighbors = CoHunsuJson_Fail("range_bad", "활성 셀이 없음")
        Exit Function
    End If
    Set ws = c.Worksheet
    hasF = c.HasFormula
    If hasF Then f = Left$(CStr(c.Formula), FORMULA_MAX)
    Query_FlowNeighbors = CoHunsuJson_Ok("{" & CoHunsuJson_Pair("sheet", CoHunsuJson_Str(ws.Name)) & "," & _
        CoHunsuJson_Pair("addr", CoHunsuJson_Str(c.Address(False, False))) & "," & _
        CoHunsuJson_Pair("formula", CoHunsuJson_Str(f)) & "," & _
        CoHunsuJson_Pair("hasFormula", IIf(hasF, "true", "false")) & "," & _
        CoHunsuJson_Pair("value", CoHunsuJson_Str(Left$(CStr(c.Text), 40))) & "," & _
        CoHunsuJson_Pair("crossSheet", IIf(InStr(1, f, "!", vbBinaryCompare) > 0, "true", "false")) & "," & _
        CoHunsuJson_Pair("precedents", Neighbors(c, True)) & "," & _
        CoHunsuJson_Pair("dependents", Neighbors(c, False)) & "}")
    Exit Function
Fail:
    Query_FlowNeighbors = CoHunsuJson_Fail("query_failed", "flow.neighbors 오류: " & Err.Description)
End Function

' 앞쪽/뒤쪽 칸 목록. 없거나 읽을 수 없으면(1004) 빈 목록.
Private Function Neighbors(ByVal c As Range, ByVal precedents As Boolean) As String
    Dim r As Range, area As Range, buf As String, n As Long, total As Double
    On Error Resume Next
    If precedents Then Set r = c.DirectPrecedents Else Set r = c.DirectDependents
    Err.Clear
    On Error GoTo Fail
    If r Is Nothing Then
        Neighbors = "{""count"":0,""items"":[],""truncated"":false}"
        Exit Function
    End If
    total = r.CountLarge
    For Each area In r.Areas
        If n >= ITEM_LIMIT Then Exit For
        If n > 0 Then buf = buf & ","
        buf = buf & "{" & CoHunsuJson_Pair("addr", CoHunsuJson_Str(area.Address(False, False))) & "," & _
            CoHunsuJson_Pair("value", CoHunsuJson_Str(AreaValue(area))) & "}"
        n = n + 1
    Next area
    Neighbors = "{" & CoHunsuJson_Pair("count", CoHunsuJson_Num(total)) & "," & CoHunsuJson_Pair("items", "[" & buf & "]") & "," & _
        CoHunsuJson_Pair("truncated", IIf(n < r.Areas.Count, "true", "false")) & "}"
    Exit Function
Fail:
    Neighbors = "{""count"":0,""items"":[],""truncated"":false}"
End Function

' 한 칸이면 그 칸의 보이는 값, 여러 칸이면 빈 글자(화면은 주소만 보여 줌).
Private Function AreaValue(ByVal area As Range) As String
    On Error Resume Next
    If area.CountLarge = 1 Then AreaValue = Left$(CStr(area.Text), 24)
End Function
