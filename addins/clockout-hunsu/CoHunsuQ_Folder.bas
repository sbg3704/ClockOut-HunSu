Attribute VB_Name = "CoHunsuQ_Folder"
'==============================================================
' 기능: 조회 folder.files와 명령 book.open. 지금 책이 들어 있는 폴더의 파일 목록(이름, 종류, 크기, 고친 날)과, 목록의 엑셀 파일 열기.
'       칼퇴 추가기능 sheet-tools의 "폴더 파일 목록"(FolderList_Make)을 새 시트 없이 패널로 옮긴 것이다(2026-11-16).
' 사용법: CoHunsu_Query "folder.files", "{}"
'         -> {"ready":true,"folder":"보고서","count":12,"bookCount":5,"truncated":false,
'             "books":[{"name":"a.xlsx","ext":"xlsx","sizeKb":35,"modified":"2026-11-16 09:30"}],"others":[{"name":"메모.txt",...}]}
'         books는 엑셀 파일(book.open으로 열 수 있음), others는 그 밖의 파일(보기만).
'         저장하지 않은 책이면 {"ready":false,"hint":"..."}.
'         CoHunsu_Do "book.open", "{""name"":""a.xlsx""}" -> 같은 폴더의 그 엑셀 파일을 연다(이미 열려 있으면 그 창으로 간다).
' 의존: CoHunsuJson. 폴더 경로는 돌려주지 않는다(폴더 이름만). 이름은 정렬해 FILE_LIMIT개까지, ~$로 시작하는 임시 파일은 뺀다.
'       book.open은 셀 값을 바꾸지 않는 이동·열기다. 이름에 경로 문자가 있거나 그 폴더에 없거나 엑셀 파일(xls, xlsx, xlsm, xlsb, xlam, csv)이 아니면 거절한다.
'==============================================================
Option Explicit

Private Const FILE_LIMIT As Long = 200

Public Function Query_FolderFiles(ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim base As String, nm As String, names() As String, n As Long, cut As Boolean
    Dim i As Long, j As Long, t As String, bufB As String, bufO As String, full As String, ext As String, one As String, nb As Long
    If ActiveWorkbook Is Nothing Then
        Query_FolderFiles = NotReady("열린 책이 없습니다")
        Exit Function
    End If
    base = ActiveWorkbook.Path
    If Len(base) = 0 Or Left$(base, 4) = "http" Then
        Query_FolderFiles = NotReady("저장한 파일에서만 볼 수 있습니다")
        Exit Function
    End If
    ReDim names(1 To FILE_LIMIT)
    nm = Dir$(base & Application.PathSeparator & "*.*", vbNormal)
    Do While Len(nm) > 0
        If Left$(nm, 2) <> "~$" Then
            If n >= FILE_LIMIT Then
                cut = True
                Exit Do
            End If
            n = n + 1
            names(n) = nm
        End If
        nm = Dir$
    Loop
    For i = 2 To n
        t = names(i)
        j = i - 1
        Do While j >= 1
            If StrComp(names(j), t, vbTextCompare) <= 0 Then Exit Do
            names(j + 1) = names(j)
            j = j - 1
        Loop
        names(j + 1) = t
    Next i
    For i = 1 To n
        full = base & Application.PathSeparator & names(i)
        ext = LCase$(Mid$(names(i), InStrRev(names(i), ".") + 1))
        If InStrRev(names(i), ".") = 0 Then ext = ""
        one = "{" & CoHunsuJson_Pair("name", CoHunsuJson_Str(names(i))) & "," & CoHunsuJson_Pair("ext", CoHunsuJson_Str(ext)) & "," & _
            CoHunsuJson_Pair("sizeKb", CoHunsuJson_Num(FileSizeKb(full))) & "," & _
            CoHunsuJson_Pair("modified", CoHunsuJson_Str(FileStamp(full))) & "}"
        If IsBookExt(ext) Then
            If nb > 0 Then bufB = bufB & ","
            bufB = bufB & one
            nb = nb + 1
        Else
            If Len(bufO) > 0 Then bufO = bufO & ","
            bufO = bufO & one
        End If
    Next i
    Query_FolderFiles = CoHunsuJson_Ok("{" & CoHunsuJson_Pair("ready", "true") & "," & _
        CoHunsuJson_Pair("folder", CoHunsuJson_Str(LeafName(base))) & "," & CoHunsuJson_Pair("count", CStr(n)) & "," & _
        CoHunsuJson_Pair("bookCount", CStr(nb)) & "," & CoHunsuJson_Pair("truncated", IIf(cut, "true", "false")) & "," & _
        CoHunsuJson_Pair("books", "[" & bufB & "]") & "," & CoHunsuJson_Pair("others", "[" & bufO & "]") & "}")
    Exit Function
Fail:
    Query_FolderFiles = CoHunsuJson_Fail("query_failed", "folder.files 오류: " & Err.Description)
End Function

Public Function Do_BookOpen(ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim nm As String, base As String, full As String, ext As String, wb As Workbook
    nm = CoHunsuJson_GetStr(argsJson, "name")
    If Len(nm) = 0 Or Len(nm) > 200 Or InStr(nm, "\") > 0 Or InStr(nm, "/") > 0 Or InStr(nm, ":") > 0 Or InStr(nm, "..") > 0 Then
        Do_BookOpen = CoHunsuJson_Fail("command_failed", "파일 이름이 올바르지 않음")
        Exit Function
    End If
    If ActiveWorkbook Is Nothing Then
        Do_BookOpen = CoHunsuJson_Fail("command_failed", "열린 책이 없음")
        Exit Function
    End If
    base = ActiveWorkbook.Path
    If Len(base) = 0 Then
        Do_BookOpen = CoHunsuJson_Fail("command_failed", "저장하지 않은 책")
        Exit Function
    End If
    ext = LCase$(Mid$(nm, InStrRev(nm, ".") + 1))
    If InStrRev(nm, ".") = 0 Or Not IsBookExt(ext) Then
        Do_BookOpen = CoHunsuJson_Fail("command_failed", "엑셀 파일이 아님")
        Exit Function
    End If
    full = base & Application.PathSeparator & nm
    If Len(Dir$(full, vbNormal)) = 0 Then
        Do_BookOpen = CoHunsuJson_Fail("command_failed", "그 폴더에 파일이 없음")
        Exit Function
    End If
    For Each wb In Application.Workbooks
        If StrComp(wb.FullName, full, vbTextCompare) = 0 Then
            wb.Activate
            Do_BookOpen = CoHunsuJson_Ok("{""name"":" & CoHunsuJson_Str(nm) & ",""already"":true}")
            Exit Function
        End If
    Next wb
    Application.Workbooks.Open Filename:=full, UpdateLinks:=0
    Do_BookOpen = CoHunsuJson_Ok("{""name"":" & CoHunsuJson_Str(nm) & ",""already"":false}")
    Exit Function
Fail:
    Do_BookOpen = CoHunsuJson_Fail("command_failed", "열기 실패: " & Err.Description)
End Function

Private Function NotReady(ByVal hint As String) As String
    NotReady = CoHunsuJson_Ok("{" & CoHunsuJson_Pair("ready", "false") & "," & CoHunsuJson_Pair("hint", CoHunsuJson_Str(hint)) & "}")
End Function

Private Function IsBookExt(ByVal ext As String) As Boolean
    Select Case ext
        Case "xls", "xlsx", "xlsm", "xlsb", "xlam", "csv"
            IsBookExt = True
    End Select
End Function

Private Function LeafName(ByVal p As String) As String
    Dim k As Long
    k = InStrRev(p, Application.PathSeparator)
    LeafName = Mid$(p, k + 1)
End Function

Private Function FileSizeKb(ByVal full As String) As Double
    On Error Resume Next
    FileSizeKb = Int((FileLen(full) + 1023) / 1024)
End Function

Private Function FileStamp(ByVal full As String) As String
    On Error Resume Next
    FileStamp = Format$(FileDateTime(full), "yyyy-mm-dd hh:nn")
End Function
