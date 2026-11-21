Attribute VB_Name = "CoHunsuDo_WriteFormula"
'==============================================================
' 기능: 한 수(hunsu-hansu, docs/22)의 수식 넣기 쓰기 명령(실험 기능). write.formula.set(칸 하나에 수식 넣기), write.formula.undo(넣기 전 내용으로 되돌리기).
' 사용법: CoHunsu_Do "write.formula.set", "{""sheet"":""요약"",""addr"":""D5"",""formula"":""=SUM(A1:A9)""}"  -> 계획: 엑셀이 값을 미리 계산해 summary에 담는다(엑셀은 안 바뀜)
'         confirm "1"을 더하면 실행: Formula2(없는 판이면 Formula)로 넣고 옛 내용을 메모리에 담아 write.formula.undo로 되돌린다(마지막 한 번만).
' 의존: CoHunsuJson, CoHunsuDo_Write(Write_Plan, Write_Applied). 지금 통합 문서(ActiveWorkbook)의 시트만. 보호된 시트·배열 수식 칸은 거절.
'       미리 계산은 Worksheet.Evaluate라 255글자를 넘는 수식은 계산하지 않고 넣기만 한다.
'==============================================================
Option Explicit

Private Const EVAL_LIMIT As Long = 255
Private Const FORMULA_LIMIT As Long = 8192

Private mHas As Boolean
Private mBook As String, mSheet As String, mAddr As String
Private mWasFormula As Boolean, mOld As Variant, mOldFormat As String

Public Function Do_WriteFormula(ByVal cmdName As String, ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim apply As Boolean, sheetName As String, addr As String, f As String, ws As Worksheet, c As Range, msg As String
    apply = (CoHunsuJson_GetStr(argsJson, "confirm") = "1")
    If cmdName = "write.formula.undo" Then
        Do_WriteFormula = UndoFormula(apply)
        Exit Function
    End If
    sheetName = CoHunsuJson_GetStr(argsJson, "sheet")
    addr = CoHunsuJson_GetStr(argsJson, "addr")
    f = CoHunsuJson_GetStr(argsJson, "formula")
    If Len(f) < 2 Or Left$(f, 1) <> "=" Then
        Do_WriteFormula = CoHunsuJson_Fail("command_failed", "수식이 비었거나 =로 시작하지 않습니다")
        Exit Function
    End If
    If Len(f) > FORMULA_LIMIT Then
        Do_WriteFormula = CoHunsuJson_Fail("command_failed", "수식이 너무 깁니다(8192글자 이하)")
        Exit Function
    End If
    Set ws = FindSheet(sheetName)
    If ws Is Nothing Then
        Do_WriteFormula = CoHunsuJson_Fail("command_failed", "넣을 시트를 찾지 못했습니다: " & sheetName)
        Exit Function
    End If
    Set c = FindCell(ws, addr)
    If c Is Nothing Then
        Do_WriteFormula = CoHunsuJson_Fail("command_failed", "넣을 칸 주소가 올바르지 않습니다: " & addr)
        Exit Function
    End If
    If ws.ProtectContents And c.Locked Then
        Do_WriteFormula = CoHunsuJson_Fail("command_failed", "보호된 시트의 잠긴 칸이라 넣을 수 없습니다")
        Exit Function
    End If
    If c.HasArray Then
        Do_WriteFormula = CoHunsuJson_Fail("command_failed", "옛 배열 수식(Ctrl+Shift+Enter) 칸이라 바꿀 수 없습니다")
        Exit Function
    End If
    If Not apply Then
        msg = Where(ws, c) & "에 넣습니다." & vbLf & "수식: " & f & vbLf & "계산 값: " & Preview(ws, f)
        If Not IsEmpty(c.Value2) Or c.HasFormula Then msg = msg & vbLf & vbLf & "지금 있는 " & IIf(c.HasFormula, "수식", "값") & "을 덮어씁니다(바로 되돌릴 수 있음)."
        Do_WriteFormula = Write_Plan(1, msg)
        Exit Function
    End If
    Remember ws, c
    msg = PutFormula(c, f)
    If Len(msg) > 0 Then
        mHas = False
        Do_WriteFormula = CoHunsuJson_Fail("command_failed", msg)
        Exit Function
    End If
    Do_WriteFormula = Write_Applied(1, Where(ws, c) & "에 넣었습니다." & vbLf & "지금 값: " & ValueText(c.Value2), "write.formula.undo", "{}")
    Exit Function
Fail:
    Do_WriteFormula = CoHunsuJson_Fail("command_failed", "수식 넣기 실패: " & Err.Description)
End Function

Private Function UndoFormula(ByVal apply As Boolean) As String
    Dim c As Range, s As String
    If Not mHas Then
        UndoFormula = CoHunsuJson_Fail("command_failed", "되돌릴 내용이 없습니다(앱이나 엑셀을 다시 켜면 사라집니다)")
        Exit Function
    End If
    If Not apply Then
        UndoFormula = Write_Plan(1, mSheet & "!" & mAddr & "를 넣기 전 내용으로 되돌립니다.")
        Exit Function
    End If
    On Error Resume Next
    Set c = Workbooks(mBook).Worksheets(mSheet).Range(mAddr)
    On Error GoTo 0
    If c Is Nothing Then
        UndoFormula = CoHunsuJson_Fail("command_failed", "되돌릴 칸을 찾지 못했습니다(책이나 시트가 바뀜)")
        Exit Function
    End If
    c.NumberFormat = mOldFormat
    If mWasFormula Then
        s = PutFormula(c, CStr(mOld))
    ElseIf IsEmpty(mOld) Then
        c.ClearContents
    ElseIf VarType(mOld) = vbString Then
        If IsNumeric(mOld) Or Left$(CStr(mOld), 1) = "=" Then c.Value2 = "'" & CStr(mOld) Else c.Value2 = mOld
    Else
        c.Value2 = mOld
    End If
    mHas = False
    If Len(s) > 0 Then
        UndoFormula = CoHunsuJson_Fail("command_failed", s)
        Exit Function
    End If
    UndoFormula = Write_Applied(1, "되돌렸습니다.", "", "")
End Function

' ---- 부품 ----

Private Function FindSheet(ByVal name As String) As Worksheet
    On Error Resume Next
    If Len(name) = 0 Then Set FindSheet = ActiveSheet Else Set FindSheet = ActiveWorkbook.Worksheets(name)
    On Error GoTo 0
End Function

Private Function FindCell(ByVal ws As Worksheet, ByVal addr As String) As Range
    On Error Resume Next
    Set FindCell = ws.Range(addr).Cells(1, 1)
    On Error GoTo 0
End Function

Private Function Where(ByVal ws As Worksheet, ByVal c As Range) As String
    Where = ws.Name & "!" & c.Address(False, False)
End Function

' 옛 내용을 기억한다(되돌리기는 마지막 한 번만).
Private Sub Remember(ByVal ws As Worksheet, ByVal c As Range)
    mBook = ws.Parent.Name
    mSheet = ws.Name
    mAddr = c.Address(False, False)
    mOldFormat = CStr(c.NumberFormat)
    mWasFormula = c.HasFormula
    If mWasFormula Then
        mOld = FormulaOf(c)
    Else
        mOld = c.Value2
    End If
    mHas = True
End Sub

' 새 판은 Formula2(동적 배열), 없는 판(438)은 Formula. 엑셀이 받지 않으면 이유 글, 되면 빈 글.
Private Function PutFormula(ByVal c As Range, ByVal f As String) As String
    On Error Resume Next
    c.Formula2 = f
    If Err.Number = 438 Then
        Err.Clear
        c.Formula = f
    End If
    If Err.Number <> 0 Then PutFormula = "엑셀이 이 수식을 받지 않았습니다(괄호·쉼표·이름을 확인해 주세요): " & Err.Description
    Err.Clear
    On Error GoTo 0
End Function

Private Function FormulaOf(ByVal c As Range) As String
    On Error Resume Next
    FormulaOf = c.Formula2
    If Err.Number <> 0 Then
        Err.Clear
        FormulaOf = c.Formula
    End If
    On Error GoTo 0
End Function

Private Function Preview(ByVal ws As Worksheet, ByVal f As String) As String
    Dim v As Variant
    If Len(f) > EVAL_LIMIT Then
        Preview = "(수식이 길어 미리 계산하지 않았습니다)"
        Exit Function
    End If
    On Error Resume Next
    v = ws.Evaluate(f)
    If Err.Number <> 0 Then
        Preview = "(미리 계산하지 못했습니다)"
        Err.Clear
        Exit Function
    End If
    On Error GoTo 0
    If IsArray(v) Then
        Preview = "여러 칸으로 펼쳐집니다(" & CStr(UBound(v, 1) - LBound(v, 1) + 1) & "줄), 첫 값 " & ValueText(FirstOf(v))
    Else
        Preview = ValueText(v)
    End If
End Function

Private Function FirstOf(ByVal v As Variant) As Variant
    On Error Resume Next
    FirstOf = v(LBound(v, 1), LBound(v, 2))
    If Err.Number <> 0 Then
        Err.Clear
        FirstOf = v(LBound(v, 1))
    End If
    On Error GoTo 0
End Function

Private Function ValueText(ByVal v As Variant) As String
    If IsError(v) Then
        If v = CVErr(2007) Then
            ValueText = "#DIV/0!"
        ElseIf v = CVErr(2042) Then
            ValueText = "#N/A"
        ElseIf v = CVErr(2029) Then
            ValueText = "#NAME?"
        ElseIf v = CVErr(2036) Then
            ValueText = "#NUM!"
        ElseIf v = CVErr(2023) Then
            ValueText = "#REF!"
        ElseIf v = CVErr(2015) Then
            ValueText = "#VALUE!"
        Else
            ValueText = "#오류"
        End If
    ElseIf IsEmpty(v) Then
        ValueText = "(빈 값)"
    ElseIf VarType(v) = vbString Then
        ValueText = """" & Left$(CStr(v), 80) & """"
    Else
        ValueText = CStr(v)
    End If
End Function
