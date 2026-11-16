Attribute VB_Name = "CoHunsuDo_WriteBook"
'==============================================================
' 기능: 책·시트 단위 쓰기 명령(실험 기능). 칼퇴 추가기능 range-tools·sheet-tools·book-tools 일부를 옮긴 것(2026-11-16, 이관 6단계).
'       write.book.sheets_from_list(고른 한 열의 글자로 시트 만들기) / split_by_group(고른 표를 한 열의 값별로 새 시트에 나누기, 값만)
'       / index(목차 시트) / delete_sheets(방금 만든 시트 지우기, 되돌리기용) / export_each(보이는 시트마다 파일 하나) / pdf(고른 시트 PDF) / backup(책 사본 저장).
' 사용법: CoHunsu_Do "write.book.backup", "{}" -> 계획 / {"confirm":"1"} 더하면 실행. 입력이 필요하면 {"phase":"need",...}(split_by_group은 keycol).
'         파일을 만드는 명령(export_each, pdf, backup)은 저장된 책에서만 되고, 책 옆에 새 폴더·파일을 만들며 기존 파일을 덮어쓰지 않는다. 앱에는 폴더 이름만 보인다.
' 의존: CoHunsuJson, CoHunsuDo_Write(Write_Plan, Write_Applied). 시트 만들기 계열은 delete_sheets로 되돌린다(비어 있거나 만든 그대로일 때만 지움).
'==============================================================
Option Explicit

Private Const MAX_SHEETS As Long = 100
Private Const MAX_ROWS As Double = 20000
Private Const INDEX_NAME As String = "목차"

Public Function Do_WriteBook(ByVal cmdName As String, ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim apply As Boolean, op As String
    apply = (CoHunsuJson_GetStr(argsJson, "confirm") = "1")
    op = Mid$(cmdName, Len("write.book.") + 1)
    If ActiveWorkbook Is Nothing Then
        Do_WriteBook = CoHunsuJson_Fail("command_failed", "열린 책이 없음")
        Exit Function
    End If
    If ActiveWorkbook.ProtectStructure And (op = "sheets_from_list" Or op = "split_by_group" Or op = "index" Or op = "delete_sheets") Then
        Do_WriteBook = CoHunsuJson_Fail("command_failed", "책 구조가 보호되어 시트를 더하거나 지울 수 없음")
        Exit Function
    End If
    Select Case op
        Case "sheets_from_list": Do_WriteBook = SheetsFromList(apply)
        Case "split_by_group": Do_WriteBook = SplitByGroup(apply, argsJson)
        Case "index": Do_WriteBook = MakeIndex(apply)
        Case "delete_sheets": Do_WriteBook = DeleteSheets(apply, CoHunsuJson_GetStr(argsJson, "names"))
        Case "export_each": Do_WriteBook = ExportEach(apply)
        Case "pdf": Do_WriteBook = ExportPdf(apply)
        Case "backup": Do_WriteBook = Backup(apply)
        Case Else: Do_WriteBook = CoHunsuJson_Fail("query_unknown", "다리에 없는 책 도구: " & op)
    End Select
    Exit Function
Fail:
    Do_WriteBook = CoHunsuJson_Fail("command_failed", "책 도구 실패: " & Err.Description)
End Function

' ---- 시트 이름 ----

Private Function SheetExists(ByVal nm As String) As Boolean
    Dim ws As Object
    For Each ws In ActiveWorkbook.Sheets
        If StrComp(ws.Name, nm, vbTextCompare) = 0 Then SheetExists = True: Exit Function
    Next ws
End Function

Private Function SafeName(ByVal s As String) As String
    Dim bad As Variant, b As Variant
    s = Trim$(s)
    For Each b In Array(":", "\", "/", "?", "*", "[", "]")
        s = Replace(s, CStr(b), "_")
    Next b
    If Left$(s, 1) = "'" Then s = "_" & Mid$(s, 2)
    If Len(s) > 31 Then s = Left$(s, 31)
    SafeName = s
End Function

Private Function NewSheet(ByVal nm As String) As Worksheet
    Dim ws As Worksheet
    Set ws = ActiveWorkbook.Worksheets.Add(After:=ActiveWorkbook.Sheets(ActiveWorkbook.Sheets.Count))
    ws.Name = nm
    Set NewSheet = ws
End Function

Private Function CreatedMsg(ByVal made As String, ByVal n As Double) As String
    If n = 0 Then
        CreatedMsg = Write_Applied(0, "만든 시트가 없습니다.", "", "")
    Else
        CreatedMsg = Write_Applied(n, "시트 " & CStr(n) & "개를 만들었습니다.", "write.book.delete_sheets", "{" & CoHunsuJson_Pair("names", CoHunsuJson_Str(made)) & "}")
    End If
End Function

' ---- 한 열의 글자로 시트 만들기 ----

Private Function SheetsFromList(ByVal apply As Boolean) As String
    Dim sel As Range, c As Range, nm As String, n As Double, skipped As Double, made As String, nmList As Collection
    If TypeName(Selection) <> "Range" Then SheetsFromList = CoHunsuJson_Fail("command_failed", "시트 이름이 든 칸을 먼저 고르세요"): Exit Function
    Set sel = Selection
    If sel.Columns.Count <> 1 Or sel.Areas.Count <> 1 Then SheetsFromList = CoHunsuJson_Fail("command_failed", "한 열만 고르세요(시트 이름 목록)"): Exit Function
    If sel.Cells.CountLarge > MAX_SHEETS Then SheetsFromList = CoHunsuJson_Fail("command_failed", "한 번에 " & CStr(MAX_SHEETS) & "개까지만 만듭니다"): Exit Function
    Set nmList = New Collection
    For Each c In sel.Cells
        nm = SafeName(CStr(c.Text))
        If Len(nm) = 0 Or SheetExists(nm) Or InCol(nmList, nm) Then
            skipped = skipped + 1
        Else
            nmList.Add nm, LCase$(nm)
        End If
    Next c
    If Not apply Then
        SheetsFromList = Write_Plan(nmList.Count, "시트 " & CStr(nmList.Count) & "개를 새로 만듭니다(빈 이름·이미 있는 이름 " & CStr(skipped) & "개는 건너뜀). 파일 맨 뒤에 붙습니다.")
        Exit Function
    End If
    Dim v As Variant
    For Each v In nmList
        NewSheet CStr(v)
        n = n + 1
        made = made & IIf(Len(made) > 0, "|", "") & CStr(v)
    Next v
    SheetsFromList = CreatedMsg(made, n)
End Function

Private Function InCol(ByVal col As Collection, ByVal key As String) As Boolean
    Dim x As Variant
    On Error Resume Next
    x = col(LCase$(key))
    InCol = (Err.Number = 0)
    Err.Clear
End Function

' ---- 한 열의 값별로 나누기(값만) ----

Private Function SplitByGroup(ByVal apply As Boolean, ByVal argsJson As String) As String
    Dim sel As Range, keyCol As Long, r As Long, i As Long, data As Variant, groups As Collection, g As Variant, nm As String
    Dim ws As Worksheet, cnt As Long, rowsOut As Long, made As String, n As Double, skipped As Double, cols As Long
    If TypeName(Selection) <> "Range" Then SplitByGroup = CoHunsuJson_Fail("command_failed", "머리글 줄이 든 표를 먼저 고르세요"): Exit Function
    Set sel = Selection
    If sel.Areas.Count <> 1 Or sel.Rows.Count < 2 Then SplitByGroup = CoHunsuJson_Fail("command_failed", "머리글 줄과 자료 줄이 있는 하나의 표를 고르세요"): Exit Function
    If sel.Rows.Count > MAX_ROWS Then SplitByGroup = CoHunsuJson_Fail("command_failed", "표가 너무 큽니다(" & CStr(MAX_ROWS) & "줄 이하)"): Exit Function
    If InStr(1, argsJson, """keycol""", vbBinaryCompare) = 0 Then
        SplitByGroup = CoHunsuJson_Ok("{""phase"":""need"",""needs"":[{""key"":""keycol"",""label"":""나눌 기준 열 번호(고른 표의 왼쪽부터 1, 2, 3…)"",""default"":""1""}]}")
        Exit Function
    End If
    keyCol = CLng(Val(CoHunsuJson_GetStr(argsJson, "keycol")))
    cols = sel.Columns.Count
    If keyCol < 1 Or keyCol > cols Then SplitByGroup = CoHunsuJson_Fail("command_failed", "기준 열 번호가 표 밖입니다(1~" & CStr(cols) & ")"): Exit Function
    data = sel.Value2
    Set groups = New Collection
    For r = 2 To UBound(data, 1)
        nm = SafeName(CStr(data(r, keyCol)))
        If Len(nm) = 0 Or SheetExists(nm) Then
            skipped = skipped + 1
        ElseIf Not InCol(groups, nm) Then
            If groups.Count >= MAX_SHEETS Then SplitByGroup = CoHunsuJson_Fail("command_failed", "나눌 묶음이 " & CStr(MAX_SHEETS) & "개를 넘습니다"): Exit Function
            groups.Add nm, LCase$(nm)
        End If
    Next r
    If Not apply Then
        SplitByGroup = Write_Plan(groups.Count, "표를 기준 열의 값별로 시트 " & CStr(groups.Count) & "개에 나눠 담습니다(값만, 서식·수식 없음). 이름이 비었거나 이미 있는 시트와 같은 줄 " & CStr(skipped) & "줄은 건너뜁니다.")
        Exit Function
    End If
    Dim outv() As Variant, k As Long, key As String
    For Each g In groups
        key = CStr(g)
        cnt = 0
        For r = 2 To UBound(data, 1)
            If StrComp(SafeName(CStr(data(r, keyCol))), key, vbTextCompare) = 0 Then cnt = cnt + 1
        Next r
        ReDim outv(1 To cnt + 1, 1 To cols)
        For i = 1 To cols: outv(1, i) = data(1, i): Next i
        k = 1
        For r = 2 To UBound(data, 1)
            If StrComp(SafeName(CStr(data(r, keyCol))), key, vbTextCompare) = 0 Then
                k = k + 1
                For i = 1 To cols: outv(k, i) = data(r, i): Next i
            End If
        Next r
        Set ws = NewSheet(key)
        ws.Range(ws.Cells(1, 1), ws.Cells(cnt + 1, cols)).Value2 = outv
        n = n + 1
        made = made & IIf(Len(made) > 0, "|", "") & key
    Next g
    SplitByGroup = CreatedMsg(made, n)
End Function

' ---- 목차 시트 ----

Private Function MakeIndex(ByVal apply As Boolean) As String
    Dim ws As Object, n As Double, r As Long, idx As Worksheet
    If SheetExists(INDEX_NAME) Then MakeIndex = CoHunsuJson_Fail("command_failed", "'" & INDEX_NAME & "' 시트가 이미 있습니다"): Exit Function
    For Each ws In ActiveWorkbook.Worksheets
        If ws.Visible = xlSheetVisible Then n = n + 1
    Next ws
    If Not apply Then MakeIndex = Write_Plan(1, "'" & INDEX_NAME & "' 시트를 맨 앞에 만들고 보이는 시트 " & CStr(n) & "개로 가는 링크를 적습니다."): Exit Function
    Set idx = ActiveWorkbook.Worksheets.Add(Before:=ActiveWorkbook.Sheets(1))
    idx.Name = INDEX_NAME
    idx.Cells(1, 1).Value2 = "시트"
    r = 1
    For Each ws In ActiveWorkbook.Worksheets
        If ws.Visible = xlSheetVisible And ws.Name <> INDEX_NAME Then
            r = r + 1
            idx.Hyperlinks.Add Anchor:=idx.Cells(r, 1), Address:="", SubAddress:="'" & ws.Name & "'!A1", TextToDisplay:=ws.Name
        End If
    Next ws
    MakeIndex = Write_Applied(1, "'" & INDEX_NAME & "' 시트를 만들었습니다.", "write.book.delete_sheets", "{" & CoHunsuJson_Pair("names", CoHunsuJson_Str(INDEX_NAME)) & "}")
End Function

' ---- 만든 시트 지우기(되돌리기) ----

Private Function DeleteSheets(ByVal apply As Boolean, ByVal names As String) As String
    Dim p As Variant, ws As Object, n As Double, left As Double, oldAlerts As Boolean
    If Len(names) = 0 Then DeleteSheets = Write_Plan(0, "지울 시트가 없습니다."): Exit Function
    For Each p In Split(names, "|")
        If SheetExists(CStr(p)) Then n = n + 1
    Next p
    If Not apply Then DeleteSheets = Write_Plan(n, "방금 만든 시트 " & CStr(n) & "개를 지웁니다."): Exit Function
    oldAlerts = Application.DisplayAlerts
    Application.DisplayAlerts = False
    For Each p In Split(names, "|")
        If SheetExists(CStr(p)) Then
            If ActiveWorkbook.Sheets.Count > 1 Then ActiveWorkbook.Sheets(CStr(p)).Delete: left = left + 1
        End If
    Next p
    Application.DisplayAlerts = oldAlerts
    DeleteSheets = Write_Applied(left, "시트 " & CStr(left) & "개를 지웠습니다.", "", "")
End Function

' ---- 파일 만들기: 저장된 책 옆에만 ----

Private Function SavedPath() As String
    If Len(ActiveWorkbook.Path) = 0 Then Err.Raise vbObjectError + 1, , "저장한 파일에서만 쓸 수 있습니다(먼저 저장하세요)"
    SavedPath = ActiveWorkbook.Path
End Function

Private Function BaseName() As String
    Dim nm As String, p As Long
    nm = ActiveWorkbook.Name
    p = InStrRev(nm, ".")
    If p > 1 Then nm = Left$(nm, p - 1)
    BaseName = nm
End Function

Private Function Stamp() As String
    Stamp = Format$(Now, "yyyymmdd_hhnnss")
End Function

Private Function Backup(ByVal apply As Boolean) As String
    Dim ext As String, p As Long, target As String
    p = InStrRev(ActiveWorkbook.Name, ".")
    ext = IIf(p > 0, Mid$(ActiveWorkbook.Name, p), ".xlsx")
    target = SavedPath() & "\" & BaseName() & "_백업_" & Stamp() & ext
    If Not apply Then Backup = Write_Plan(1, "지금 상태를 같은 폴더에 사본 파일 1개로 저장합니다(원래 파일은 그대로, 이름: " & BaseName() & "_백업_날짜" & ext & ")."): Exit Function
    ActiveWorkbook.SaveCopyAs target
    Backup = Write_Applied(1, "백업 사본을 저장했습니다: " & Mid$(target, InStrRev(target, "\") + 1), "", "")
End Function

Private Function ExportPdf(ByVal apply As Boolean) As String
    Dim target As String, n As Double
    n = ActiveWindow.SelectedSheets.Count
    target = SavedPath() & "\" & BaseName() & "_" & Stamp() & ".pdf"
    If Not apply Then ExportPdf = Write_Plan(n, "고른 시트 " & CStr(n) & "개를 PDF 파일 1개로 같은 폴더에 저장합니다."): Exit Function
    ActiveSheet.ExportAsFixedFormat Type:=0, Filename:=target, Quality:=0, IncludeDocProperties:=False, OpenAfterPublish:=False
    ExportPdf = Write_Applied(n, "PDF를 저장했습니다: " & Mid$(target, InStrRev(target, "\") + 1), "", "")
End Function

Private Function ExportEach(ByVal apply As Boolean) As String
    Dim ws As Object, n As Double, dir As String, folder As String, nb As Workbook, done As Double, oldAlerts As Boolean, used As Range
    For Each ws In ActiveWorkbook.Worksheets
        If ws.Visible = xlSheetVisible Then n = n + 1
    Next ws
    If n > 50 Then ExportEach = CoHunsuJson_Fail("command_failed", "보이는 시트가 50개를 넘습니다"): Exit Function
    folder = BaseName() & "_시트별_" & Stamp()
    dir = SavedPath() & "\" & folder
    If Not apply Then ExportEach = Write_Plan(n, "보이는 시트 " & CStr(n) & "개를 각각 엑셀 파일로 새 폴더 '" & folder & "'에 저장합니다(값만, 수식은 값으로)."): Exit Function
    MkDir dir
    oldAlerts = Application.DisplayAlerts
    Application.DisplayAlerts = False
    For Each ws In ActiveWorkbook.Worksheets
        If ws.Visible = xlSheetVisible Then
            ws.Copy
            Set nb = ActiveWorkbook
            Set used = nb.Worksheets(1).UsedRange
            used.Value2 = used.Value2
            nb.SaveAs dir & "\" & SafeName(ws.Name) & ".xlsx", 51
            nb.Close False
            done = done + 1
        End If
    Next ws
    Application.DisplayAlerts = oldAlerts
    ExportEach = Write_Applied(done, "파일 " & CStr(done) & "개를 폴더 '" & folder & "'에 저장했습니다.", "", "")
End Function
