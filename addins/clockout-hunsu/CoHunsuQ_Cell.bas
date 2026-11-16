Attribute VB_Name = "CoHunsuQ_Cell"
'==============================================================
' 기능: 조회 cell.info. 지금 고른 칸(활성 셀) 하나의 수식·값 종류·서식·병합·메모 여부. 값 자체는 돌려주지 않고 종류만 돌려준다.
' 사용법: CoHunsu_Query "cell.info", "{}" -> {"sheet":"매출","addr":"C5","hasFormula":true,"formula":"=A5/B5","kind":"number","format":"General","merged":false,"hasNote":false,"locked":true}
'         kind: empty, text, number, date, bool, error. formula는 앞 FORMULA_LIMIT글자만.
' 의존: CoHunsuJson. 읽기 전용, 칸 하나만 읽으므로 가볍다.
'==============================================================
Option Explicit

Private Const FORMULA_LIMIT As Long = 200

Public Function Query_CellInfo(ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim c As Range, kind As String, f As String, v As Variant, merged As Boolean, hasNote As Boolean, locked As Boolean
    Dim fmt As String, hasF As Boolean
    If ActiveCell Is Nothing Then
        Query_CellInfo = CoHunsuJson_Fail("range_bad", "활성 셀이 없음")
        Exit Function
    End If
    Set c = ActiveCell
    hasF = c.HasFormula
    If hasF Then f = CStr(c.Formula)
    If Len(f) > FORMULA_LIMIT Then f = Left$(f, FORMULA_LIMIT) & "…"
    v = c.Value2
    If IsError(v) Then
        kind = "error"
    ElseIf IsEmpty(v) Then
        kind = "empty"
    ElseIf VarType(v) = vbBoolean Then
        kind = "bool"
    ElseIf VarType(v) = vbString Then
        kind = "text"
    Else
        kind = "number"
        On Error Resume Next
        If c.NumberFormat Like "*[ymd]*" And Not c.NumberFormat Like "*[#0]*" Then kind = "date"
        On Error GoTo Fail
    End If
    fmt = CStr(c.NumberFormat)
    merged = CBool(c.MergeCells)
    On Error Resume Next
    hasNote = Not (c.Comment Is Nothing)
    locked = CBool(c.Locked)
    On Error GoTo Fail
    Query_CellInfo = CoHunsuJson_Ok("{" & CoHunsuJson_Pair("sheet", CoHunsuJson_Str(CStr(c.Worksheet.Name))) & "," & _
        CoHunsuJson_Pair("addr", CoHunsuJson_Str(c.Address(False, False))) & "," & _
        CoHunsuJson_Pair("hasFormula", IIf(hasF, "true", "false")) & "," & _
        CoHunsuJson_Pair("formula", CoHunsuJson_Str(f)) & "," & _
        CoHunsuJson_Pair("kind", CoHunsuJson_Str(kind)) & "," & _
        CoHunsuJson_Pair("format", CoHunsuJson_Str(fmt)) & "," & _
        CoHunsuJson_Pair("merged", IIf(merged, "true", "false")) & "," & _
        CoHunsuJson_Pair("hasNote", IIf(hasNote, "true", "false")) & "," & _
        CoHunsuJson_Pair("locked", IIf(locked, "true", "false")) & "}")
    Exit Function
Fail:
    Query_CellInfo = CoHunsuJson_Fail("query_failed", "cell.info 오류: " & Err.Description)
End Function
