Attribute VB_Name = "CoHunsuQ_Errors"
'==============================================================
' 기능: 조회 errors(docs/02 5장). 지금 활성 시트의 오류 셀(수식 결과 오류 + 입력된 오류 값)을 주소와 오류 종류로 돌려준다. 최대 ERR_LIMIT개까지만 담고 전체 수는 count에 둔다.
' 사용법: CoHunsu_Query "errors", "{}"  ->  {"sheet":"매출","count":12,"shown":12,"items":[{"addr":"C5","sheet":"매출","kind":"div0","label":"#DIV/0!","formula":"=A5/B5"}],"truncated":false}
'         kind는 docs/02의 코드(ref, div0, value, na, name, null, num, spill, calc), label은 엑셀에 보이는 이름. reason과 root는 다음 단계에서 더한다.
'         요청의 scope(book)와 limit은 아직 받지 않는다(활성 시트만).
' 의존: CoHunsuJson. 읽기 전용: 선택, 활성 시트, 엑셀 설정을 바꾸지 않는다(docs/04 3장). 시트 전체를 도는 대신 SpecialCells로 오류 셀만 찾는다.
'==============================================================
Option Explicit

Private Const ERR_LIMIT As Long = 100
Private Const FORMULA_MAX As Long = 80

Public Function Query_Errors(ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim ws As Worksheet, found As Range, part As Range, area As Range, cell As Range
    Dim buf As String, shown As Long, total As Double, used As Range
    If TypeName(ActiveSheet) <> "Worksheet" Then
        Query_Errors = CoHunsuJson_Ok(EmptyData(""))
        Exit Function
    End If
    Set ws = ActiveSheet
    Set used = ws.UsedRange
    On Error Resume Next
    Set found = used.SpecialCells(xlCellTypeFormulas, xlErrors)
    Err.Clear
    Set part = used.SpecialCells(xlCellTypeConstants, xlErrors)
    Err.Clear
    On Error GoTo Fail
    If found Is Nothing Then
        Set found = part
    ElseIf Not part Is Nothing Then
        Set found = Application.Union(found, part)
    End If
    If found Is Nothing Then
        Query_Errors = CoHunsuJson_Ok(EmptyData(ws.Name))
        Exit Function
    End If
    total = found.CountLarge
    For Each area In found.Areas
        For Each cell In area.Cells
            If shown >= ERR_LIMIT Then Exit For
            If shown > 0 Then buf = buf & ","
            buf = buf & "{" & CoHunsuJson_Pair("addr", CoHunsuJson_Str(cell.Address(False, False))) & "," & _
                CoHunsuJson_Pair("sheet", CoHunsuJson_Str(ws.Name)) & "," & _
                CoHunsuJson_Pair("kind", CoHunsuJson_Str(ErrorKind(cell.Value2))) & "," & _
                CoHunsuJson_Pair("label", CoHunsuJson_Str(ErrorLabel(cell.Value2))) & "," & _
                CoHunsuJson_Pair("formula", CoHunsuJson_Str(FormulaText(cell))) & "}"
            shown = shown + 1
        Next cell
        If shown >= ERR_LIMIT Then Exit For
    Next area
    Query_Errors = CoHunsuJson_Ok("{" & CoHunsuJson_Pair("sheet", CoHunsuJson_Str(ws.Name)) & "," & _
        CoHunsuJson_Pair("count", CoHunsuJson_Num(total)) & "," & CoHunsuJson_Pair("shown", CStr(shown)) & "," & _
        CoHunsuJson_Pair("items", "[" & buf & "]") & "," & CoHunsuJson_Pair("truncated", IIf(total > shown, "true", "false")) & "}")
    Exit Function
Fail:
    Query_Errors = CoHunsuJson_Fail("query_failed", "errors 오류: " & Err.Description)
End Function

Private Function EmptyData(ByVal sheetName As String) As String
    EmptyData = "{" & CoHunsuJson_Pair("sheet", CoHunsuJson_Str(sheetName)) & ",""count"":0,""shown"":0,""items"":[],""truncated"":false}"
End Function

Private Function FormulaText(ByVal cell As Range) As String
    On Error Resume Next
    If cell.HasFormula Then FormulaText = Left$(CStr(cell.Formula), FORMULA_MAX)
End Function

' docs/02의 오류 종류 코드. 알 수 없으면 other.
Private Function ErrorKind(ByVal v As Variant) As String
    ErrorKind = "other"
    If Not IsError(v) Then Exit Function
    If v = CVErr(xlErrDiv0) Then
        ErrorKind = "div0"
    ElseIf v = CVErr(xlErrNA) Then
        ErrorKind = "na"
    ElseIf v = CVErr(xlErrName) Then
        ErrorKind = "name"
    ElseIf v = CVErr(xlErrNull) Then
        ErrorKind = "null"
    ElseIf v = CVErr(xlErrNum) Then
        ErrorKind = "num"
    ElseIf v = CVErr(xlErrRef) Then
        ErrorKind = "ref"
    ElseIf v = CVErr(xlErrValue) Then
        ErrorKind = "value"
    ElseIf v = CVErr(2045) Then
        ErrorKind = "spill"
    ElseIf v = CVErr(2046) Then
        ErrorKind = "calc"
    End If
End Function

' 엑셀에 보이는 오류 이름.
Private Function ErrorLabel(ByVal v As Variant) As String
    Select Case ErrorKind(v)
        Case "div0": ErrorLabel = "#DIV/0!"
        Case "na": ErrorLabel = "#N/A"
        Case "name": ErrorLabel = "#NAME?"
        Case "null": ErrorLabel = "#NULL!"
        Case "num": ErrorLabel = "#NUM!"
        Case "ref": ErrorLabel = "#REF!"
        Case "value": ErrorLabel = "#VALUE!"
        Case "spill": ErrorLabel = "#SPILL!"
        Case "calc": ErrorLabel = "#CALC!"
        Case Else: ErrorLabel = "오류"
    End Select
End Function
