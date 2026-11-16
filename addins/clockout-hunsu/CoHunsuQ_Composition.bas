Attribute VB_Name = "CoHunsuQ_Composition"
'==============================================================
' 기능: 조회 composition. 지금 시트 사용 범위의 구성: 수식 칸, 숫자 상수 칸, 글자 상수 칸 수와 나머지(빈 칸 등). 값은 읽지 않고 개수만 센다.
' 사용법: CoHunsu_Query "composition", "{}" -> {"sheet":"매출","cells":1200,"formulas":300,"numbers":500,"texts":100,"others":300,"skipped":false}
' 의존: CoHunsuJson. 읽기 전용. 사용 범위가 MAX_CELLS 칸을 넘으면 세지 않고 skipped:true(엑셀 부담 방지).
'==============================================================
Option Explicit

Private Const MAX_CELLS As Double = 2000000

Public Function Query_Composition(ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim ws As Worksheet, u As Range, total As Double, f As Double, nn As Double, tt As Double, skipped As Boolean
    If ActiveSheet Is Nothing Or TypeName(ActiveSheet) <> "Worksheet" Then
        Query_Composition = CoHunsuJson_Fail("range_bad", "활성 시트가 워크시트가 아님")
        Exit Function
    End If
    Set ws = ActiveSheet
    Set u = ws.UsedRange
    total = u.CountLarge
    If total > MAX_CELLS Then
        skipped = True
    Else
        f = CountKind(u, xlCellTypeFormulas, 23)
        nn = CountKind(u, xlCellTypeConstants, xlNumbers)
        tt = CountKind(u, xlCellTypeConstants, xlTextValues)
    End If
    Query_Composition = CoHunsuJson_Ok("{" & CoHunsuJson_Pair("sheet", CoHunsuJson_Str(CStr(ws.Name))) & "," & _
        CoHunsuJson_Pair("cells", CoHunsuJson_Num(total)) & "," & _
        CoHunsuJson_Pair("formulas", CoHunsuJson_Num(f)) & "," & _
        CoHunsuJson_Pair("numbers", CoHunsuJson_Num(nn)) & "," & _
        CoHunsuJson_Pair("texts", CoHunsuJson_Num(tt)) & "," & _
        CoHunsuJson_Pair("others", CoHunsuJson_Num(IIf(skipped, 0, total - f - nn - tt))) & "," & _
        CoHunsuJson_Pair("skipped", IIf(skipped, "true", "false")) & "}")
    Exit Function
Fail:
    Query_Composition = CoHunsuJson_Fail("query_failed", "composition 오류: " & Err.Description)
End Function

' 해당 종류가 없으면 SpecialCells가 오류를 내므로 0으로 본다.
Private Function CountKind(ByVal u As Range, ByVal kind As Long, ByVal valueKind As Long) As Double
    Dim r As Range
    On Error Resume Next
    Set r = u.SpecialCells(kind, valueKind)
    If Err.Number = 0 And Not r Is Nothing Then CountKind = r.CountLarge
    Err.Clear
End Function
