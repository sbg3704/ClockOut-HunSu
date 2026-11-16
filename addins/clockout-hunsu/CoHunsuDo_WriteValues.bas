Attribute VB_Name = "CoHunsuDo_WriteValues"
'==============================================================
' 기능: 값 정리 쓰기 명령(실험 기능) 한 묶음. 선택한 칸의 상수(수식 칸은 건드리지 않음)를 정리한다. 칼퇴 추가기능 value-tools·quick-tools를 옮긴 것(2026-11-16).
'       write.values.clean(공백·숨은 글자 정리) / tonum(텍스트 숫자를 숫자로) / round(올림·절사·반올림, 단위 필요) / flip(부호 뒤집기) / affix(앞뒤 글자 붙이기, 글자 필요)
'       / tovalue(수식을 값으로) / iferror(IFERROR로 감싸기) / filldown(빈 칸을 위 값으로) / todate(텍스트 날짜를 날짜로) / unmerge(병합 해제 + 값 채우기) / mergecenter(병합 해제 → 선택 영역 가운데) / undo(마지막 값 되돌리기).
' 사용법: CoHunsu_Do "write.values.round", "{}"  -> 입력이 필요하면 {"phase":"need","needs":[{"key":"unit","label":"...","default":"10"}]}, 앱이 물어 인자에 더해 다시 보낸다
'         CoHunsu_Do "write.values.clean", "{}"  -> 계획(엑셀 안 바뀜) {"phase":"plan","count":N,"summary":"..."}  /  {"confirm":"1"} 더하면 실행.
'         실행 때 바뀐 칸의 옛 내용(수식, 값, 표시 형식)을 메모리에 담아 두어 write.values.undo로 되돌린다(5000칸까지, 구조를 바꾸는 unmerge·mergecenter는 되돌리기 없음).
' 의존: CoHunsuJson, CoHunsuDo_Write(Write_Plan, Write_Applied). 규칙은 추가기능과 같게: 수식 칸은 건드리지 않고, 0으로 시작하는 코드("00123")와 15자리 넘는 숫자는 그대로 둔다.
'       숨은 줄도 선택에 들어 있으면 바뀐다. 보호된 시트는 거절. 선택이 사용 범위와 겹치는 곳이 CELL_LIMIT칸을 넘으면 거절.
'==============================================================
Option Explicit

Private Const CELL_LIMIT As Double = 100000
Private Const UNDO_LIMIT As Long = 5000

Private mUndo As Collection

Public Function Do_WriteValues(ByVal cmdName As String, ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim op As String, apply As Boolean, sel As Range, ws As Worksheet, used As Range, area As Range
    Dim unit As Double, mode As String, pre As String, suf As String, n As Double, txt As String
    op = Mid$(cmdName, Len("write.values.") + 1)
    apply = (CoHunsuJson_GetStr(argsJson, "confirm") = "1")
    If op = "undo" Then
        Do_WriteValues = UndoLast(apply)
        Exit Function
    End If
    If TypeName(Selection) <> "Range" Then
        Do_WriteValues = CoHunsuJson_Fail("command_failed", "셀 범위를 먼저 고르세요")
        Exit Function
    End If
    Set sel = Selection
    Set ws = sel.Worksheet
    If ws.ProtectContents Then
        Do_WriteValues = CoHunsuJson_Fail("command_failed", "보호된 시트는 바꿀 수 없음")
        Exit Function
    End If
    Set used = Application.Intersect(sel, ws.UsedRange)
    If used Is Nothing Then
        Do_WriteValues = Write_Plan(0, "선택한 곳에 값이 있는 칸이 없습니다.")
        Exit Function
    End If
    If used.CountLarge > CELL_LIMIT Then
        Do_WriteValues = CoHunsuJson_Fail("command_failed", "선택이 너무 큽니다(" & CStr(CELL_LIMIT) & "칸 이하로 줄여 주세요)")
        Exit Function
    End If
    Select Case op
        Case "round"
            If Not HasKey(argsJson, "unit") Or Not HasKey(argsJson, "mode") Then
                Do_WriteValues = NeedJson("{""key"":""unit"",""label"":""단위 (예: 1, 10, 100, 0.1)"",""default"":""10""},{""key"":""mode"",""label"":""방식 (반올림, 올림, 절사 중 하나)"",""default"":""반올림""}")
                Exit Function
            End If
            unit = Val(CoHunsuJson_GetStr(argsJson, "unit"))
            mode = CoHunsuJson_GetStr(argsJson, "mode")
            If unit <= 0 Then
                Do_WriteValues = CoHunsuJson_Fail("command_failed", "단위는 0보다 큰 숫자여야 합니다")
                Exit Function
            End If
            If mode <> "반올림" And mode <> "올림" And mode <> "절사" Then
                Do_WriteValues = CoHunsuJson_Fail("command_failed", "방식은 반올림, 올림, 절사 중 하나여야 합니다")
                Exit Function
            End If
        Case "affix"
            If Not HasKey(argsJson, "pre") Or Not HasKey(argsJson, "suf") Then
                Do_WriteValues = NeedJson("{""key"":""pre"",""label"":""앞에 붙일 글자 (없으면 비워 두세요)"",""default"":""""},{""key"":""suf"",""label"":""뒤에 붙일 글자 (없으면 비워 두세요)"",""default"":""""}")
                Exit Function
            End If
            pre = CoHunsuJson_GetStr(argsJson, "pre")
            suf = CoHunsuJson_GetStr(argsJson, "suf")
            If Len(pre) = 0 And Len(suf) = 0 Then
                Do_WriteValues = CoHunsuJson_Fail("command_failed", "붙일 글자가 없습니다")
                Exit Function
            End If
        Case "clean", "tonum", "flip", "tovalue", "iferror", "filldown", "todate", "unmerge", "mergecenter", "won", "clearinput", "natsort"
        Case Else
            Do_WriteValues = CoHunsuJson_Fail("query_unknown", "다리에 없는 값 정리 도구: " & op)
            Exit Function
    End Select
    If apply Then Set mUndo = New Collection
    If op = "unmerge" Or op = "mergecenter" Then
        n = DoMerges(op, used, apply)
    ElseIf op = "natsort" Then
        For Each area In used.Areas
            n = n + DoNatSort(area, apply)
        Next area
    Else
        For Each area In used.Areas
            n = n + DoArea(op, area, apply, unit, mode, pre, suf)
        Next area
    End If
    If Not apply Then
        Do_WriteValues = Write_Plan(n, Describe(op, n, unit, mode, pre, suf))
        Exit Function
    End If
    If op = "unmerge" Or op = "mergecenter" Or mUndo.Count = 0 Or mUndo.Count > UNDO_LIMIT Then
        Set mUndo = Nothing
        Do_WriteValues = Write_Applied(n, CStr(n) & "건을 바꿨습니다.", "", "")
    Else
        Do_WriteValues = Write_Applied(n, CStr(n) & "칸을 바꿨습니다.", "write.values.undo", "{}")
    End If
    Exit Function
Fail:
    Do_WriteValues = CoHunsuJson_Fail("command_failed", "값 정리 실패: " & Err.Description)
End Function

Private Function HasKey(ByVal json As String, ByVal key As String) As Boolean
    HasKey = InStr(1, json, """" & key & """", vbBinaryCompare) > 0
End Function

Private Function NeedJson(ByVal itemsJson As String) As String
    NeedJson = CoHunsuJson_Ok("{""phase"":""need"",""needs"":[" & itemsJson & "]}")
End Function

Private Function Describe(ByVal op As String, ByVal n As Double, ByVal unit As Double, ByVal mode As String, ByVal pre As String, ByVal suf As String) As String
    Dim c As String
    c = CStr(n)
    Select Case op
        Case "clean": Describe = c & "칸의 공백·숨은 글자를 정리합니다."
        Case "tonum": Describe = c & "칸의 글자를 숫자로 바꿉니다. (0으로 시작하는 코드와 15자리 넘는 숫자는 그대로 둡니다.)"
        Case "round": Describe = c & "칸의 숫자를 " & CStr(unit) & " 단위로 " & mode & "합니다. 서식이 아니라 실제 값이 바뀝니다."
        Case "flip": Describe = c & "칸의 숫자 부호를 뒤집습니다."
        Case "affix": Describe = c & "칸의 값을 글자로 바꿔 앞에 '" & pre & "', 뒤에 '" & suf & "'를 붙입니다. 숫자는 글자가 됩니다."
        Case "tovalue": Describe = c & "칸의 수식을 지금 값으로 바꿉니다."
        Case "iferror": Describe = c & "칸의 수식을 IFERROR(수식,0)으로 감쌉니다. (이미 IFERROR이거나 배열 수식이면 건너뜁니다.)"
        Case "filldown": Describe = c & "개의 빈 칸을 같은 열의 바로 위 값으로 채웁니다."
        Case "todate": Describe = c & "칸의 날짜 모양 글자(2026.11.08, 20261108 등)를 진짜 날짜로 바꿉니다."
        Case "won": Describe = c & "칸의 0 이상 정수를 한글 금액 글자(예: 일천이백삼십사원)로 바꿉니다. 숫자가 글자가 됩니다."
        Case "clearinput": Describe = c & "칸의 입력값(수식이 아닌 상수)을 비웁니다. 수식과 서식은 그대로입니다."
        Case "natsort": Describe = c & "칸의 자리를 바꿔 고른 열의 상수를 자연 순서(품목2 < 품목10)로 정렬합니다. 열 하나씩만 정렬합니다."
        Case "unmerge": Describe = c & "개의 병합을 풀고 병합됐던 모든 칸에 원래 값을 채웁니다. 되돌리기 없음."
        Case "mergecenter": Describe = c & "개의 병합을 풀고 같은 범위를 '선택 영역의 가운데'로 바꿉니다. 되돌리기 없음."
    End Select
End Function

' 영역 하나를 읽어(배열) 바꿀 칸을 찾고, apply이면 쓴다. 바뀐(또는 바뀔) 칸 수.
Private Function DoArea(ByVal op As String, ByVal area As Range, ByVal apply As Boolean, ByVal unit As Double, ByVal mode As String, ByVal pre As String, ByVal suf As String) As Double
    Dim v As Variant, f As Variant, i As Long, j As Long, n As Double, x As Variant, isF As Boolean, isText As Boolean, t As String, nv As Variant, d As Double
    v = Grid(area, False)
    f = Grid(area, True)
    For i = 1 To UBound(v, 1)
        For j = 1 To UBound(v, 2)
            x = v(i, j)
            isF = (VarType(f(i, j)) = vbString)
            If isF Then isF = (Left$(CStr(f(i, j)), 1) = "=")
            isText = (VarType(x) = vbString)
            Select Case op
                Case "clean"
                    If Not isF And isText Then
                        t = CleanText(CStr(x))
                        If t <> CStr(x) Then
                            n = n + 1
                            If apply Then Snap area.Cells(i, j): SetText area.Cells(i, j), t, True
                        End If
                    End If
                Case "tonum"
                    If Not isF And isText Then
                        t = NormalizeNumber(CleanText(CStr(x)))
                        If IsSafeNumberText(t) Then
                            n = n + 1
                            If apply Then Snap area.Cells(i, j): area.Cells(i, j).NumberFormat = "General": area.Cells(i, j).Value2 = Val(t)
                        End If
                    End If
                Case "round"
                    If Not isF And IsNumeric2(x) Then
                        d = CDbl(x)
                        nv = RoundTo(d, unit, mode)
                        If nv <> d Then
                            n = n + 1
                            If apply Then Snap area.Cells(i, j): area.Cells(i, j).Value2 = nv
                        End If
                    End If
                Case "flip"
                    If Not isF And IsNumeric2(x) Then
                        If CDbl(x) <> 0 Then
                            n = n + 1
                            If apply Then Snap area.Cells(i, j): area.Cells(i, j).Value2 = -CDbl(x)
                        End If
                    End If
                Case "affix"
                    If Not isF And Not IsEmpty(x) And Not IsError(x) Then
                        If Not (isText And Len(CStr(x)) = 0) Then
                            n = n + 1
                            If apply Then Snap area.Cells(i, j): SetText area.Cells(i, j), pre & CStr(area.Cells(i, j).Text) & suf, True
                        End If
                    End If
                Case "won"
                    If Not isF And IsNumeric2(x) Then
                        d = CDbl(x)
                        If d >= 0 And d < 10000000000000000# And d = Int(d) Then
                            n = n + 1
                            If apply Then Snap area.Cells(i, j): SetText area.Cells(i, j), HangulWon(d), True
                        End If
                    End If
                Case "clearinput"
                    If Not isF And Not IsEmpty(x) Then
                        n = n + 1
                        If apply Then Snap area.Cells(i, j): area.Cells(i, j).ClearContents
                    End If
                Case "tovalue"
                    If isF And Not IsError(x) Then
                        n = n + 1
                        If apply Then Snap area.Cells(i, j): area.Cells(i, j).Value2 = x
                    End If
                Case "iferror"
                    If isF Then
                        t = CStr(f(i, j))
                        If UCase$(Left$(t, 9)) <> "=IFERROR(" And Not area.Cells(i, j).HasArray Then
                            n = n + 1
                            If apply Then Snap area.Cells(i, j): area.Cells(i, j).Formula = "=IFERROR(" & Mid$(t, 2) & ",0)"
                        End If
                    End If
                Case "todate"
                    If Not isF And isText Then
                        If ParseDate(CStr(x), d) Then
                            n = n + 1
                            If apply Then
                                Snap area.Cells(i, j)
                                area.Cells(i, j).NumberFormat = "yyyy-mm-dd"
                                area.Cells(i, j).Value2 = d
                            End If
                        End If
                    End If
                Case "filldown"
                    If i > 1 And Len(CStr(f(i, j))) = 0 And Not isF Then
                        If HasUpper(v, f, i, j) Then
                            n = n + 1
                            If apply Then
                                Snap area.Cells(i, j)
                                v(i, j) = v(i - 1, j)
                                If VarType(v(i, j)) = vbString Then SetText area.Cells(i, j), CStr(v(i, j)), False Else area.Cells(i, j).Value2 = v(i, j)
                                f(i, j) = "x"
                            End If
                        End If
                    End If
            End Select
        Next j
    Next i
    DoArea = n
End Function

' filldown용: 같은 열 위쪽에 값이 있는 칸이 있고 그 바로 위 칸이 오류가 아닌지(채울 값이 있는지).
Private Function HasUpper(ByRef v As Variant, ByRef f As Variant, ByVal i As Long, ByVal j As Long) As Boolean
    Dim k As Long
    For k = i - 1 To 1 Step -1
        If Len(CStr(f(k, j))) > 0 Then
            HasUpper = Not IsError(v(k, j))
            If k < i - 1 Then HasUpper = HasUpper
            Exit Function
        End If
    Next k
End Function

' 병합 풀기. unmerge: 풀고 모든 칸에 값 채움. mergecenter: 풀고 선택 영역의 가운데로. 병합 영역 수를 센다.
Private Function DoMerges(ByVal op As String, ByVal used As Range, ByVal apply As Boolean) As Double
    Dim area As Range, c As Range, addrs As New Collection, a As Variant, ma As Range, anchor As Variant, i As Long, n As Double
    For Each area In used.Areas
        For Each c In area.Cells
            If c.MergeCells Then
                If c.Address = c.MergeArea.Cells(1, 1).Address Then
                    On Error Resume Next
                    addrs.Add c.MergeArea.Address, c.MergeArea.Address
                    On Error GoTo 0
                End If
            End If
        Next c
    Next area
    n = addrs.Count
    If Not apply Then
        DoMerges = n
        Exit Function
    End If
    For Each a In addrs
        Set ma = used.Worksheet.Range(CStr(a))
        anchor = ma.Cells(1, 1).Value2
        ma.UnMerge
        If op = "unmerge" Then
            For i = 2 To ma.Cells.Count
                If VarType(anchor) = vbString Then
                    SetText ma.Cells(i), CStr(anchor), False
                ElseIf Not IsError(anchor) And Not IsEmpty(anchor) Then
                    ma.Cells(i).Value2 = anchor
                End If
            Next i
        Else
            ma.HorizontalAlignment = xlCenterAcrossSelection
        End If
    Next a
    DoMerges = n
End Function

'' 한글 금액: 1234 -> 일천이백삼십사원. 0은 영원. 만, 억, 조 단위로 네 자리씩 끊는다.
Private Function HangulWon(ByVal d As Double) As String
    Dim digits As Variant, units As Variant, big As Variant, s As String, grp As Long, part As Double, g As String, k As Long, dg As Long, p As Long
    If d = 0 Then HangulWon = "영원": Exit Function
    digits = Array("", "일", "이", "삼", "사", "오", "육", "칠", "팔", "구")
    units = Array("", "십", "백", "천")
    big = Array("", "만", "억", "조")
    For grp = 3 To 0 Step -1
        part = Int(d / (10000# ^ grp))
        part = part - Int(part / 10000#) * 10000#
        If part > 0 Then
            g = ""
            For k = 3 To 0 Step -1
                p = CLng(Int(part / (10 ^ k))) Mod 10
                dg = p
                If dg > 0 Then g = g & digits(dg) & units(k)
            Next k
            s = s & g & big(grp)
        End If
    Next grp
    HangulWon = s & "원"
End Function

' 자연 정렬: 한 영역의 열마다 상수(수식·빈 칸 제외)를 자연 순서로 정렬해 같은 칸들에 다시 쓴다.
Private Function DoNatSort(ByVal area As Range, ByVal apply As Boolean) As Double
    Dim col As Long, r As Long, v As Variant, f As Variant, cnt As Long, pos() As Long, vals() As Variant, i As Long, j As Long, tmp As Variant, n As Double, c As Range
    v = Grid(area, False)
    f = Grid(area, True)
    For col = 1 To UBound(v, 2)
        cnt = 0
        ReDim pos(1 To UBound(v, 1))
        ReDim vals(1 To UBound(v, 1))
        For r = 1 To UBound(v, 1)
            If Len(CStr(f(r, col))) > 0 And Left$(CStr(f(r, col)), 1) <> "=" And Not IsError(v(r, col)) Then
                cnt = cnt + 1
                pos(cnt) = r
                vals(cnt) = v(r, col)
            End If
        Next r
        If cnt > 1 And cnt <= UNDO_LIMIT Then
            For i = 2 To cnt
                tmp = vals(i)
                j = i - 1
                Do While j >= 1
                    If NatCompare(CStr(vals(j)), CStr(tmp)) <= 0 Then Exit Do
                    vals(j + 1) = vals(j)
                    j = j - 1
                Loop
                vals(j + 1) = tmp
            Next i
            For i = 1 To cnt
                If CStr(vals(i)) <> CStr(v(pos(i), col)) Or VarType(vals(i)) <> VarType(v(pos(i), col)) Then
                    n = n + 1
                    If apply Then
                        Set c = area.Cells(pos(i), col)
                        Snap c
                        If VarType(vals(i)) = vbString Then SetText c, CStr(vals(i)), False Else c.Value2 = vals(i)
                    End If
                End If
            Next i
        End If
    Next col
    DoNatSort = n
End Function

' 자연 비교: 숫자 덩어리는 크기로, 나머지는 대소문자 무시 글자로. 앞이 작으면 -1, 같으면 0, 크면 1.
Private Function NatCompare(ByVal a As String, ByVal b As String) As Long
    Dim i As Long, j As Long, ca As String, cb As String, na As String, nb As String
    i = 1: j = 1
    Do While i <= Len(a) And j <= Len(b)
        ca = Mid$(a, i, 1): cb = Mid$(b, j, 1)
        If ca >= "0" And ca <= "9" And cb >= "0" And cb <= "9" Then
            na = "": nb = ""
            Do While i <= Len(a)
                If Mid$(a, i, 1) >= "0" And Mid$(a, i, 1) <= "9" Then na = na & Mid$(a, i, 1): i = i + 1 Else Exit Do
            Loop
            Do While j <= Len(b)
                If Mid$(b, j, 1) >= "0" And Mid$(b, j, 1) <= "9" Then nb = nb & Mid$(b, j, 1): j = j + 1 Else Exit Do
            Loop
            na = StripZeros(na): nb = StripZeros(nb)
            If Len(na) <> Len(nb) Then NatCompare = IIf(Len(na) < Len(nb), -1, 1): Exit Function
            If na <> nb Then NatCompare = IIf(na < nb, -1, 1): Exit Function
        Else
            ca = LCase$(ca): cb = LCase$(cb)
            If ca <> cb Then NatCompare = IIf(StrComp(ca, cb, vbBinaryCompare) < 0, -1, 1): Exit Function
            i = i + 1: j = j + 1
        End If
    Loop
    If Len(a) - i = Len(b) - j Then NatCompare = 0 Else NatCompare = IIf(Len(a) - i < Len(b) - j, -1, 1)
End Function

Private Function StripZeros(ByVal s As String) As String
    Do While Len(s) > 1 And Left$(s, 1) = "0"
        s = Mid$(s, 2)
    Loop
    StripZeros = s
End Function

' ---- 되돌리기 ----

Private Sub Snap(ByVal c As Range)
    If mUndo Is Nothing Then Exit Sub
    If mUndo.Count > UNDO_LIMIT Then Exit Sub
    Dim isF As Boolean, content As Variant
    isF = c.HasFormula
    If isF Then content = c.Formula Else content = c.Value2
    mUndo.Add Array(c.Worksheet.Name, c.Address(False, False), isF, content, c.NumberFormat)
End Sub

Private Function UndoLast(ByVal apply As Boolean) As String
    Dim e As Variant, c As Range, n As Double, total As Double
    If mUndo Is Nothing Then
        UndoLast = CoHunsuJson_Fail("command_failed", "되돌릴 내용이 없습니다(앱을 다시 켜면 사라집니다)")
        Exit Function
    End If
    total = mUndo.Count
    If Not apply Then
        UndoLast = Write_Plan(total, CStr(total) & "칸을 바꾸기 전 값으로 되돌립니다.")
        Exit Function
    End If
    For Each e In mUndo
        On Error Resume Next
        Set c = ActiveWorkbook.Sheets(CStr(e(0))).Range(CStr(e(1)))
        If Err.Number = 0 Then
            c.NumberFormat = e(4)
            If e(2) Then
                c.Formula = e(3)
            ElseIf IsEmpty(e(3)) Then
                c.ClearContents
            ElseIf VarType(e(3)) = vbString Then
                SetText c, CStr(e(3)), False
            Else
                c.Value2 = e(3)
            End If
            If Err.Number = 0 Then n = n + 1
        End If
        Err.Clear
        On Error GoTo 0
    Next e
    Set mUndo = Nothing
    UndoLast = Write_Applied(n, CStr(n) & "칸을 되돌렸습니다.", "", "")
End Function

' ---- 부품 ----

' 영역을 항상 2차원 배열로(한 칸이면 1x1). formulas가 True면 Formula, 아니면 Value2.
Private Function Grid(ByVal r As Range, ByVal formulas As Boolean) As Variant
    Dim g() As Variant, raw As Variant
    If formulas Then raw = r.Formula Else raw = r.Value2
    If r.CountLarge = 1 Then
        ReDim g(1 To 1, 1 To 1)
        g(1, 1) = raw
        Grid = g
    Else
        Grid = raw
    End If
End Function

' 글자 값을 칸에 쓴다. 숫자로 읽힐 수 있는 글자는 글자로 남도록 맨 앞에 ' 를 붙인다(칸 표시 형식이 글자(@)이면 그대로).
Private Sub SetText(ByVal c As Range, ByVal s As String, ByVal clearIfEmpty As Boolean)
    If Len(s) = 0 Then
        If clearIfEmpty Then c.ClearContents
        Exit Sub
    End If
    If c.NumberFormat = "@" Then
        c.Value2 = s
    Else
        c.Value2 = "'" & s
    End If
End Sub

Private Function IsNumeric2(ByVal x As Variant) As Boolean
    Select Case VarType(x)
        Case vbDouble, vbCurrency, vbInteger, vbLong, vbSingle
            IsNumeric2 = True
    End Select
End Function

' 앞뒤·중복 공백과 NBSP·제로폭 글자를 정리한다. 줄바꿈은 그대로 둔다.
Private Function CleanText(ByVal s As String) As String
    Dim t As String
    t = Replace(s, ChrW(160), " ")
    t = Replace(t, ChrW(8203), "")
    t = Replace(t, ChrW(8204), "")
    t = Replace(t, ChrW(8205), "")
    t = Replace(t, ChrW(65279), "")
    t = Replace(t, vbTab, " ")
    Do While InStr(t, "  ") > 0
        t = Replace(t, "  ", " ")
    Loop
    t = Replace(t, " " & vbLf, vbLf)
    t = Replace(t, vbLf & " ", vbLf)
    CleanText = Trim$(t)
End Function

' 숫자 모양 글자("1,200", "123-", "(1,500)")를 "-1200" 꼴로. 숫자 모양이 아니면 "". (칼퇴 value-tools ValueFix_NormalizeNumber와 같은 규칙)
Private Function NormalizeNumber(ByVal s As String) As String
    Dim t As String, neg As Boolean, i As Long, c As String, dots As Long, digits As Long
    t = Replace(Replace(Replace(s, ChrW(160), ""), " ", ""), ",", "")
    If Len(t) = 0 Then Exit Function
    If Left$(t, 1) = "(" And Right$(t, 1) = ")" Then
        neg = True
        t = Mid$(t, 2, Len(t) - 2)
    End If
    If Right$(t, 1) = "-" Then
        If neg Then Exit Function
        neg = True
        t = Left$(t, Len(t) - 1)
    End If
    If Left$(t, 1) = "-" Then
        If neg Then Exit Function
        neg = True
        t = Mid$(t, 2)
    ElseIf Left$(t, 1) = "+" Then
        t = Mid$(t, 2)
    End If
    If Len(t) = 0 Then Exit Function
    For i = 1 To Len(t)
        c = Mid$(t, i, 1)
        If c >= "0" And c <= "9" Then
            digits = digits + 1
        ElseIf c = "." Then
            dots = dots + 1
        Else
            Exit Function
        End If
    Next i
    If digits = 0 Or dots > 1 Then Exit Function
    NormalizeNumber = IIf(neg, "-", "") & t
End Function

' 바꿔도 안전한 숫자 글자인가: 15자리를 넘으면 안 되고, 0으로 시작하는 코드("00123")는 그대로 둔다.
Private Function IsSafeNumberText(ByVal norm As String) As Boolean
    Dim body As String, digits As Long, i As Long
    If Len(norm) = 0 Then Exit Function
    body = IIf(Left$(norm, 1) = "-", Mid$(norm, 2), norm)
    For i = 1 To Len(body)
        If Mid$(body, i, 1) <> "." Then digits = digits + 1
    Next i
    If digits > 15 Then Exit Function
    If Len(body) > 1 Then
        If Left$(body, 1) = "0" And Mid$(body, 2, 1) <> "." Then Exit Function
    End If
    IsSafeNumberText = True
End Function

' 단위로 반올림·올림(0에서 멀어지는 쪽)·절사(0 쪽). 부동소수 찌꺼기는 10자리에서 정리한다.
Private Function RoundTo(ByVal d As Double, ByVal unit As Double, ByVal mode As String) As Double
    Dim q As Double, a As Double, r As Double
    q = d / unit
    a = Abs(q)
    Select Case mode
        Case "올림": r = -Int(-(a - 0.000000000001))
        Case "절사": r = Int(a + 0.000000000001)
        Case Else: r = Int(a + 0.5 + 0.000000000001)
    End Select
    r = r * unit
    If q < 0 Then r = -r
    RoundTo = CDbl(Format$(r, "0.##########"))
End Function

' "2026.11.08", "2026-11-8", "2026/11/08", "20261108" 같은 글자를 날짜 값으로. 맞지 않으면 False.
Private Function ParseDate(ByVal s As String, ByRef serial As Double) As Boolean
    Dim t As String, p() As String, y As Long, m As Long, d As Long, k As Long, ok As Boolean
    t = Trim$(Replace(s, " ", ""))
    If Right$(t, 1) = "." Then t = Left$(t, Len(t) - 1)
    If Len(t) = 8 And IsDigits(t) Then
        y = CLng(Left$(t, 4)): m = CLng(Mid$(t, 5, 2)): d = CLng(Right$(t, 2))
        ok = True
    Else
        t = Replace(Replace(t, "/", "."), "-", ".")
        p = Split(t, ".")
        If UBound(p) = 2 Then
            If IsDigits(p(0)) And IsDigits(p(1)) And IsDigits(p(2)) And Len(p(0)) = 4 And Len(p(1)) <= 2 And Len(p(2)) <= 2 Then
                y = CLng(p(0)): m = CLng(p(1)): d = CLng(p(2))
                ok = True
            End If
        End If
    End If
    If Not ok Then Exit Function
    If y < 1900 Or y > 2100 Or m < 1 Or m > 12 Or d < 1 Or d > 31 Then Exit Function
    serial = CDbl(DateSerial(y, m, d))
    If Month(serial) <> m Or Day(serial) <> d Then Exit Function
    ParseDate = True
End Function

Private Function IsDigits(ByVal s As String) As Boolean
    Dim i As Long
    If Len(s) = 0 Then Exit Function
    For i = 1 To Len(s)
        If Mid$(s, i, 1) < "0" Or Mid$(s, i, 1) > "9" Then Exit Function
    Next i
    IsDigits = True
End Function
