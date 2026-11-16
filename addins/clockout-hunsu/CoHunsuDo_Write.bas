Attribute VB_Name = "CoHunsuDo_Write"
'==============================================================
' 기능: 쓰기 명령(실험 기능). 앱이 공지와 확인 창을 거친 뒤에만 보낸다(docs/11 2장, docs/01 9장 8번 개정 2026-11-16).
'       write.sheets.unhide(숨은 시트 모두 보이기), write.sheets.rehide(되돌리기), write.links.break(외부 링크 끊기),
'       write.names.delete_broken(#REF! 이름 삭제), write.names.restore(되돌리기), write.lastcell.fix(마지막 칸 바로잡기, 활성 시트).
' 사용법: CoHunsu_Do "write.sheets.unhide", "{}"                 -> 계획만: {"phase":"plan","count":3,"summary":"..."}
'         CoHunsu_Do "write.sheets.unhide", "{""confirm"":""1""}" -> 실행: {"phase":"apply","changed":3,"message":"...","undoCmd":"write.sheets.rehide","undoArgs":"{...}"}
'         confirm이 "1"이 아니면 어떤 경우에도 엑셀을 바꾸지 않는다(앱이 확인을 받았다는 표시). undoCmd, undoArgs는 되돌릴 수 있을 때만 있다.
' 의존: CoHunsuJson. 아주 숨김(xlSheetVeryHidden) 시트는 건드리지 않는다. 한 가지라도 못 하면 오류 코드 command_failed와 이유를 돌려준다.
'==============================================================
Option Explicit

Public Function Do_Write(ByVal cmdName As String, ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim apply As Boolean
    apply = (CoHunsuJson_GetStr(argsJson, "confirm") = "1")
    If ActiveWorkbook Is Nothing Then
        Do_Write = CoHunsuJson_Fail("command_failed", "열린 책이 없음")
        Exit Function
    End If
    Select Case cmdName
        Case "write.sheets.unhide"
            Do_Write = SheetsUnhide(apply)
        Case "write.sheets.rehide"
            Do_Write = SheetsRehide(CoHunsuJson_GetStr(argsJson, "names"), apply)
        Case "write.links.break"
            Do_Write = LinksBreak(apply)
        Case "write.names.delete_broken"
            Do_Write = NamesDeleteBroken(apply)
        Case "write.names.restore"
            Do_Write = NamesRestore(CoHunsuJson_GetStr(argsJson, "records"), apply)
        Case "write.lastcell.fix"
            Do_Write = LastCellFix(apply)
        Case Else
            Do_Write = CoHunsuJson_Fail("query_unknown", "다리에 없는 쓰기 명령: " & cmdName)
    End Select
    Exit Function
Fail:
    Do_Write = CoHunsuJson_Fail("command_failed", "쓰기 실패: " & Err.Description)
End Function

Public Function Write_Plan(ByVal count As Double, ByVal summary As String) As String
    Write_Plan = CoHunsuJson_Ok("{" & CoHunsuJson_Pair("phase", """plan""") & "," & CoHunsuJson_Pair("count", CoHunsuJson_Num(count)) & "," & _
        CoHunsuJson_Pair("summary", CoHunsuJson_Str(summary)) & "}")
End Function

Public Function Write_Applied(ByVal changed As Double, ByVal message As String, ByVal undoCmd As String, ByVal undoArgsJson As String) As String
    Dim u As String
    If Len(undoCmd) > 0 Then
        u = "," & CoHunsuJson_Pair("undoCmd", CoHunsuJson_Str(undoCmd)) & "," & CoHunsuJson_Pair("undoArgs", CoHunsuJson_Str(undoArgsJson))
    End If
    Write_Applied = CoHunsuJson_Ok("{" & CoHunsuJson_Pair("phase", """apply""") & "," & CoHunsuJson_Pair("changed", CoHunsuJson_Num(changed)) & "," & _
        CoHunsuJson_Pair("message", CoHunsuJson_Str(message)) & u & "}")
End Function

' ---- 시트 ----

Private Function SheetsUnhide(ByVal apply As Boolean) As String
    Dim ws As Object, n As Double, names As String
    If ActiveWorkbook.ProtectStructure Then
        SheetsUnhide = CoHunsuJson_Fail("command_failed", "통합 문서 구조가 보호되어 있어 시트를 보이게 할 수 없음")
        Exit Function
    End If
    For Each ws In ActiveWorkbook.Sheets
        If ws.Visible = xlSheetHidden Then
            n = n + 1
            If Len(names) > 0 Then names = names & vbTab
            names = names & ws.Name
        End If
    Next ws
    If Not apply Then
        SheetsUnhide = Write_Plan(n, "숨은 시트 " & CStr(n) & "개를 보이게 합니다. (아주 숨김 시트는 그대로 둡니다.)")
        Exit Function
    End If
    SheetsUnhide = Write_Applied(SetVisible(names, xlSheetVisible), "숨은 시트를 보이게 했습니다.", "write.sheets.rehide", "{" & CoHunsuJson_Pair("names", CoHunsuJson_Str(names)) & "}")
End Function

Private Function SheetsRehide(ByVal names As String, ByVal apply As Boolean) As String
    Dim n As Double
    n = UBound(Split(names, vbTab)) + 1
    If Len(names) = 0 Then n = 0
    If Not apply Then
        SheetsRehide = Write_Plan(n, "시트 " & CStr(n) & "개를 다시 숨깁니다.")
        Exit Function
    End If
    SheetsRehide = Write_Applied(SetVisible(names, xlSheetHidden), "시트를 다시 숨겼습니다.", "", "")
End Function

' 이름 목록(탭으로 구분)의 시트를 정해진 보임 상태로. 못 바꾼 시트는 건너뛰고 바뀐 수를 돌려준다.
Private Function SetVisible(ByVal names As String, ByVal state As Long) As Double
    Dim parts() As String, i As Long, done As Double
    If Len(names) = 0 Then Exit Function
    parts = Split(names, vbTab)
    For i = LBound(parts) To UBound(parts)
        On Error Resume Next
        ActiveWorkbook.Sheets(parts(i)).Visible = state
        If Err.Number = 0 Then done = done + 1
        Err.Clear
        On Error GoTo 0
    Next i
    SetVisible = done
End Function

' ---- 외부 링크 ----

Private Function LinksBreak(ByVal apply As Boolean) As String
    Dim v As Variant, i As Long, n As Double, done As Double
    v = ActiveWorkbook.LinkSources(xlExcelLinks)
    If Not IsEmpty(v) Then n = UBound(v) - LBound(v) + 1
    If Not apply Then
        LinksBreak = Write_Plan(n, "다른 파일을 가리키는 링크 " & CStr(n) & "개를 끊고, 그 링크를 쓰던 수식을 지금 값으로 바꿉니다. 되돌릴 수 없습니다.")
        Exit Function
    End If
    If n > 0 Then
        For i = LBound(v) To UBound(v)
            On Error Resume Next
            ActiveWorkbook.BreakLink Name:=v(i), Type:=xlExcelLinks
            If Err.Number = 0 Then done = done + 1
            Err.Clear
            On Error GoTo 0
        Next i
    End If
    LinksBreak = Write_Applied(done, "외부 링크 " & CStr(done) & "개를 끊었습니다.", "", "")
End Function

' ---- 이름 ----

Private Function NamesDeleteBroken(ByVal apply As Boolean) As String
    Dim i As Long, nm As Object, n As Double, records As String, done As Double, list As Collection, one As Variant
    Set list = New Collection
    For i = 1 To ActiveWorkbook.Names.Count
        Set nm = ActiveWorkbook.Names(i)
        If InStr(nm.RefersTo, "#REF!") > 0 And InStr(nm.Name, "_xlfn") = 0 Then
            list.Add nm.Name & vbTab & nm.RefersTo
            n = n + 1
        End If
    Next i
    If Not apply Then
        NamesDeleteBroken = Write_Plan(n, "참조가 깨진(#REF!) 이름 " & CStr(n) & "개를 지웁니다. 이름 목록은 되돌리기로 다시 만들 수 있습니다.")
        Exit Function
    End If
    For Each one In list
        On Error Resume Next
        ActiveWorkbook.Names(Split(one, vbTab)(0)).Delete
        If Err.Number = 0 Then
            done = done + 1
            If Len(records) > 0 Then records = records & vbLf
            records = records & one
        End If
        Err.Clear
        On Error GoTo 0
    Next one
    NamesDeleteBroken = Write_Applied(done, "깨진 이름 " & CStr(done) & "개를 지웠습니다.", "write.names.restore", "{" & CoHunsuJson_Pair("records", CoHunsuJson_Str(records)) & "}")
End Function

Private Function NamesRestore(ByVal records As String, ByVal apply As Boolean) As String
    Dim lines() As String, i As Long, f() As String, n As Double, done As Double
    If Len(records) > 0 Then
        lines = Split(records, vbLf)
        n = UBound(lines) + 1
    End If
    If Not apply Then
        NamesRestore = Write_Plan(n, "지운 이름 " & CStr(n) & "개를 다시 만듭니다.")
        Exit Function
    End If
    For i = 0 To n - 1
        f = Split(lines(i), vbTab)
        If UBound(f) >= 1 Then
            On Error Resume Next
            ActiveWorkbook.Names.Add Name:=f(0), RefersTo:=f(1)
            If Err.Number = 0 Then done = done + 1
            Err.Clear
            On Error GoTo 0
        End If
    Next i
    NamesRestore = Write_Applied(done, "이름 " & CStr(done) & "개를 다시 만들었습니다.", "", "")
End Function

' ---- 마지막 칸 ----

' 활성 시트의 사용 범위가 실제 값이 있는 곳보다 크면(서식만 남은 빈 행·열) 그 빈 행·열을 지운다.
' 보호된 시트, 표·도형·메모가 있는 시트, 값이 하나도 없는 시트는 건드리지 않는다.
Private Function LastCellFix(ByVal apply As Boolean) As String
    Dim ws As Worksheet, u As Range, lastR As Long, lastC As Long, usedR As Long, usedC As Long, extraR As Long, extraC As Long, f As Range
    If TypeName(ActiveSheet) <> "Worksheet" Then
        LastCellFix = CoHunsuJson_Fail("command_failed", "활성 시트가 워크시트가 아님")
        Exit Function
    End If
    Set ws = ActiveSheet
    If ws.ProtectContents Then
        LastCellFix = CoHunsuJson_Fail("command_failed", "보호된 시트는 건드리지 않음")
        Exit Function
    End If
    If ws.ListObjects.Count > 0 Or ws.Shapes.Count > 0 Or ws.Comments.Count > 0 Then
        LastCellFix = CoHunsuJson_Fail("command_failed", "표, 도형, 메모가 있는 시트는 건드리지 않음")
        Exit Function
    End If
    Set f = ws.Cells.Find(What:="*", SearchOrder:=xlByRows, SearchDirection:=xlPrevious, LookIn:=xlFormulas)
    If f Is Nothing Then
        LastCellFix = CoHunsuJson_Fail("command_failed", "값이 없는 시트는 건드리지 않음")
        Exit Function
    End If
    lastR = f.Row
    Set f = ws.Cells.Find(What:="*", SearchOrder:=xlByColumns, SearchDirection:=xlPrevious, LookIn:=xlFormulas)
    lastC = f.Column
    Set u = ws.UsedRange
    usedR = u.Row + u.Rows.Count - 1
    usedC = u.Column + u.Columns.Count - 1
    extraR = usedR - lastR
    extraC = usedC - lastC
    If extraR < 0 Then extraR = 0
    If extraC < 0 Then extraC = 0
    If Not apply Then
        LastCellFix = Write_Plan(extraR + extraC, "시트 '" & ws.Name & "'의 서식만 남은 빈 행 " & CStr(extraR) & "개와 빈 열 " & CStr(extraC) & "개를 지워 마지막 칸을 " & ws.Cells(lastR, lastC).Address(False, False) & "로 바로잡습니다. 되돌릴 수 없습니다.")
        Exit Function
    End If
    If extraR > 0 Then ws.Rows(CStr(lastR + 1) & ":" & CStr(usedR)).Delete
    If extraC > 0 Then ws.Range(ws.Columns(lastC + 1), ws.Columns(usedC)).Delete
    Set u = ws.UsedRange
    LastCellFix = Write_Applied(extraR + extraC, "마지막 칸을 바로잡았습니다.", "", "")
End Function
