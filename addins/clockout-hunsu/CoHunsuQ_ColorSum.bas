Attribute VB_Name = "CoHunsuQ_ColorSum"
'==============================================================
' 기능: 조회 color.sum. 지금 선택(사용 범위와 겹치는 곳)의 칸을 채우기 색별, 글자 색별로 묶어 개수와 숫자 합계를 돌려준다.
'       칼퇴 추가기능 range-tools의 "색깔별 합계"(RangeColor_Sum)를 훈수꾼 패널로 옮긴 조회다(2026-11-16). 새 시트를 만들지 않는다.
' 사용법: CoHunsu_Query "color.sum", "{}"
'         -> {"sheet":"매출","scanned":120,"truncated":false,"fillKinds":3,"fontKinds":2,
'             "fill":[{"label":"#FFC000","count":40,"nums":38,"sum":1234.5},{"label":"색 없음",...}],"font":[...]}
' 의존: CoHunsuJson. 읽기 전용(선택·값·서식을 바꾸지 않는다). 칸마다 Interior.Color를 읽어야 해서 SCAN_LIMIT 칸까지만 보고 넘으면 truncated:true.
'       조건부 서식이 칠한 색은 보지 않는다(칸에 직접 칠한 색만). 글자 색은 값이 있는 칸만 센다. 색 종류는 KIND_LIMIT까지, 나머지는 otherCount.
'==============================================================
Option Explicit

Private Const SCAN_LIMIT As Long = 20000
Private Const KIND_LIMIT As Long = 64
Private Const SHOW_LIMIT As Long = 12
Private Const NO_FILL As Long = -1

Private mFillKey(1 To KIND_LIMIT) As Long, mFillCnt(1 To KIND_LIMIT) As Double, mFillNum(1 To KIND_LIMIT) As Double, mFillSum(1 To KIND_LIMIT) As Double
Private mFontKey(1 To KIND_LIMIT) As Long, mFontCnt(1 To KIND_LIMIT) As Double, mFontNum(1 To KIND_LIMIT) As Double, mFontSum(1 To KIND_LIMIT) As Double
Private mFillN As Long, mFontN As Long, mOther As Double

Public Function Query_ColorSum(ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim sel As Range, used As Range, area As Range, c As Range, scanned As Double, cut As Boolean
    If TypeName(Selection) <> "Range" Then
        Query_ColorSum = CoHunsuJson_Fail("range_bad", "선택이 셀 범위가 아님")
        Exit Function
    End If
    mFillN = 0: mFontN = 0: mOther = 0
    Set sel = Selection
    Set used = Application.Intersect(sel, sel.Worksheet.UsedRange)
    If Not used Is Nothing Then
        For Each area In used.Areas
            For Each c In area.Cells
                If scanned >= SCAN_LIMIT Then
                    cut = True
                    Exit For
                End If
                ScanCell c
                scanned = scanned + 1
            Next c
            If cut Then Exit For
        Next area
    End If
    Query_ColorSum = CoHunsuJson_Ok("{" & CoHunsuJson_Pair("sheet", CoHunsuJson_Str(sel.Worksheet.Name)) & "," & _
        CoHunsuJson_Pair("scanned", CoHunsuJson_Num(scanned)) & "," & _
        CoHunsuJson_Pair("truncated", IIf(cut, "true", "false")) & "," & _
        CoHunsuJson_Pair("fillKinds", CStr(mFillN)) & "," & CoHunsuJson_Pair("fontKinds", CStr(mFontN)) & "," & _
        CoHunsuJson_Pair("otherCount", CoHunsuJson_Num(mOther)) & "," & _
        CoHunsuJson_Pair("fill", GroupJson(mFillKey, mFillCnt, mFillNum, mFillSum, mFillN, "색 없음")) & "," & _
        CoHunsuJson_Pair("font", GroupJson(mFontKey, mFontCnt, mFontNum, mFontSum, mFontN, "")) & "}")
    Exit Function
Fail:
    Query_ColorSum = CoHunsuJson_Fail("query_failed", "color.sum 오류: " & Err.Description)
End Function

' 칸 하나를 두 묶음(채우기 색, 글자 색)에 더한다. 숫자 상수·수식 결과만 합계에 넣는다(오류·글자는 개수만).
Private Sub ScanCell(ByVal c As Range)
    Dim v As Variant, isNum As Boolean, d As Double, fillKey As Long
    v = c.Value2
    If VarType(v) = vbDouble Or VarType(v) = vbCurrency Or VarType(v) = vbDate Or VarType(v) = vbInteger Or VarType(v) = vbLong Then
        isNum = True
        d = CDbl(v)
    End If
    If c.Interior.ColorIndex = xlColorIndexNone Then
        fillKey = NO_FILL
    Else
        fillKey = CLng(c.Interior.Color)
    End If
    Bump mFillKey, mFillCnt, mFillNum, mFillSum, mFillN, fillKey, isNum, d
    If Not IsEmpty(v) Then
        If VarType(v) <> vbString Then
            Bump mFontKey, mFontCnt, mFontNum, mFontSum, mFontN, CLng(c.Font.Color), isNum, d
        ElseIf Len(v) > 0 Then
            Bump mFontKey, mFontCnt, mFontNum, mFontSum, mFontN, CLng(c.Font.Color), isNum, d
        End If
    End If
End Sub

Private Sub Bump(ByRef keys() As Long, ByRef cnt() As Double, ByRef nums() As Double, ByRef sm() As Double, ByRef n As Long, _
        ByVal key As Long, ByVal isNum As Boolean, ByVal d As Double)
    Dim i As Long
    For i = 1 To n
        If keys(i) = key Then Exit For
    Next i
    If i > n Then
        If n >= KIND_LIMIT Then
            mOther = mOther + 1
            Exit Sub
        End If
        n = n + 1
        i = n
        keys(i) = key
        cnt(i) = 0: nums(i) = 0: sm(i) = 0
    End If
    cnt(i) = cnt(i) + 1
    If isNum Then
        nums(i) = nums(i) + 1
        sm(i) = sm(i) + d
    End If
End Sub

' 개수가 많은 순으로 SHOW_LIMIT개. noFillLabel은 key=NO_FILL의 이름(글자 색 묶음에는 쓰지 않음).
Private Function GroupJson(ByRef keys() As Long, ByRef cnt() As Double, ByRef nums() As Double, ByRef sm() As Double, ByVal n As Long, ByVal noFillLabel As String) As String
    Dim taken() As Boolean, shown As Long, i As Long, best As Long, buf As String
    If n = 0 Then
        GroupJson = "[]"
        Exit Function
    End If
    ReDim taken(1 To n)
    Do While shown < SHOW_LIMIT And shown < n
        best = 0
        For i = 1 To n
            If Not taken(i) Then
                If best = 0 Then
                    best = i
                ElseIf cnt(i) > cnt(best) Then
                    best = i
                End If
            End If
        Next i
        taken(best) = True
        If shown > 0 Then buf = buf & ","
        buf = buf & "{" & CoHunsuJson_Pair("label", CoHunsuJson_Str(ColorLabel(keys(best), noFillLabel))) & "," & _
            CoHunsuJson_Pair("count", CoHunsuJson_Num(cnt(best))) & "," & _
            CoHunsuJson_Pair("nums", CoHunsuJson_Num(nums(best))) & "," & _
            CoHunsuJson_Pair("sum", CoHunsuJson_Num(sm(best))) & "}"
        shown = shown + 1
    Loop
    GroupJson = "[" & buf & "]"
End Function

' VBA 색(BGR 정수)을 #RRGGBB 글자로.
Private Function ColorLabel(ByVal key As Long, ByVal noFillLabel As String) As String
    Dim r As Long, g As Long, b As Long
    If key = NO_FILL Then
        ColorLabel = noFillLabel
        Exit Function
    End If
    r = key And &HFF&
    g = (key \ &H100&) And &HFF&
    b = (key \ &H10000) And &HFF&
    ColorLabel = "#" & Right$("0" & Hex$(r), 2) & Right$("0" & Hex$(g), 2) & Right$("0" & Hex$(b), 2)
End Function
