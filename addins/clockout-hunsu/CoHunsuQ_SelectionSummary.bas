Attribute VB_Name = "CoHunsuQ_SelectionSummary"
'==============================================================
' 기능: 조회 selection.summary. 지금 선택의 주소, 개수, 합, 평균, 최소, 최대, 오류 수, 종류별 수(docs/02 5장).
' 사용법: CoHunsu_Query "selection.summary", "{}"
' 의존: CoHunsuJson. 읽기 전용: 선택, 활성 시트, 엑셀 설정을 바꾸지 않는다(docs/04 3장).
'       큰 선택은 사용 범위(UsedRange)와 겹치는 곳만 배열로 한 번에 읽는다. 그래도 100만 칸이 넘으면 워크시트 함수로 세고 truncated:true.
'==============================================================
Option Explicit

Private Const CELL_LIMIT As Double = 1000000#

Private mNum As Double, mText As Double, mErr As Double, mNonEmpty As Double
Private mSum As Double, mMin As Double, mMax As Double

Public Function Query_SelectionSummary(ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim sel As Range, ws As Worksheet, used As Range, area As Range
    Dim totalCells As Double, formulas As Double, truncated As Boolean
    If TypeName(Selection) <> "Range" Then
        Query_SelectionSummary = CoHunsuJson_Fail("range_bad", "선택이 셀 범위가 아님")
        Exit Function
    End If
    Set sel = Selection
    Set ws = sel.Worksheet
    totalCells = sel.CountLarge
    mNum = 0: mText = 0: mErr = 0: mNonEmpty = 0: mSum = 0: mMin = 0: mMax = 0
    Set used = Application.Intersect(sel, ws.UsedRange)
    If Not used Is Nothing Then
        If used.CountLarge > CELL_LIMIT Then
            truncated = True
            CountByFunctions used
        Else
            For Each area In used.Areas
                CountValues area.Value2
                formulas = formulas + CountFormulas(area)
            Next area
        End If
    End If
    Query_SelectionSummary = CoHunsuJson_Ok(BuildData(sel, ws, totalCells, formulas, truncated))
    Exit Function
Fail:
    Query_SelectionSummary = CoHunsuJson_Fail("query_failed", "selection.summary 오류: " & Err.Description)
End Function

Private Sub CountValues(ByVal v As Variant)
    Dim item As Variant
    If IsArray(v) Then
        For Each item In v
            CountOne item
        Next item
    Else
        CountOne v
    End If
End Sub

Private Sub CountOne(ByVal item As Variant)
    Select Case VarType(item)
        Case vbEmpty
            Exit Sub
        Case vbDouble, vbCurrency, vbDate, vbInteger, vbLong, vbSingle
            If mNum = 0 Then
                mMin = CDbl(item): mMax = CDbl(item)
            Else
                If CDbl(item) < mMin Then mMin = CDbl(item)
                If CDbl(item) > mMax Then mMax = CDbl(item)
            End If
            mNum = mNum + 1
            mSum = mSum + CDbl(item)
        Case vbError
            mErr = mErr + 1
        Case vbString
            If Len(item) = 0 Then Exit Sub
            mText = mText + 1
        Case Else
            mText = mText + 1
    End Select
    mNonEmpty = mNonEmpty + 1
End Sub

' 한 칸짜리에 SpecialCells를 쓰면 시트 전체를 찾으므로 따로 센다.
Private Function CountFormulas(ByVal area As Range) As Double
    Dim found As Range
    If area.CountLarge = 1 Then
        If area.HasFormula Then CountFormulas = 1
        Exit Function
    End If
    On Error Resume Next
    Set found = area.SpecialCells(xlCellTypeFormulas)
    If Err.Number = 0 And Not found Is Nothing Then CountFormulas = found.CountLarge
    Err.Clear
End Function

Private Sub CountByFunctions(ByVal used As Range)
    On Error Resume Next
    With Application.WorksheetFunction
        mNum = .Count(used)
        mNonEmpty = .CountA(used)
        If mNum > 0 Then
            mSum = .Sum(used)
            mMin = .Min(used)
            mMax = .Max(used)
        End If
    End With
    mText = mNonEmpty - mNum
End Sub

Private Function BuildData(ByVal sel As Range, ByVal ws As Worksheet, ByVal totalCells As Double, ByVal formulas As Double, ByVal truncated As Boolean) As String
    Dim nums As String
    If mNum > 0 Then
        nums = CoHunsuJson_Pair("sum", CoHunsuJson_Num(mSum)) & "," & CoHunsuJson_Pair("avg", CoHunsuJson_Num(mSum / mNum)) & "," & _
            CoHunsuJson_Pair("min", CoHunsuJson_Num(mMin)) & "," & CoHunsuJson_Pair("max", CoHunsuJson_Num(mMax))
    Else
        nums = """sum"":null,""avg"":null,""min"":null,""max"":null"
    End If
    BuildData = "{" & CoHunsuJson_Pair("addr", CoHunsuJson_Str(sel.Address)) & "," & _
        CoHunsuJson_Pair("sheet", CoHunsuJson_Str(ws.Name)) & "," & _
        CoHunsuJson_Pair("book", CoHunsuJson_Str(ws.Parent.Name)) & "," & _
        CoHunsuJson_Pair("count", CoHunsuJson_Num(totalCells)) & "," & _
        CoHunsuJson_Pair("nonEmpty", CoHunsuJson_Num(mNonEmpty)) & "," & nums & "," & _
        CoHunsuJson_Pair("errors", CoHunsuJson_Num(mErr)) & "," & _
        CoHunsuJson_Pair("types", "{" & CoHunsuJson_Pair("num", CoHunsuJson_Num(mNum)) & "," & _
            CoHunsuJson_Pair("text", CoHunsuJson_Num(mText)) & "," & CoHunsuJson_Pair("formula", CoHunsuJson_Num(formulas)) & "}") & _
        IIf(truncated, ",""truncated"":true", "") & "}"
End Function
