Attribute VB_Name = "CoHunsuQ_Comments"
'==============================================================
' 기능: 조회 comments. 지금 시트의 메모(노트)와 스레드 댓글 목록. 칸 주소와 글 앞부분만 돌려준다.
' 사용법: CoHunsu_Query "comments", "{}" -> {"sheet":"매출","notes":2,"threads":1,"items":[{"addr":"C5","kind":"note","text":"확인 필요"}],"truncated":false}
'         kind: note(메모), thread(스레드 댓글, 엑셀 2016 이후). 최대 ITEM_LIMIT개, 글은 TEXT_LIMIT글자까지.
' 의존: CoHunsuJson. 읽기 전용.
'==============================================================
Option Explicit

Private Const ITEM_LIMIT As Long = 40
Private Const TEXT_LIMIT As Long = 60

Public Function Query_Comments(ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim ws As Worksheet, cm As Object, buf As String, n As Long, notes As Long, threads As Long, t As String
    If ActiveSheet Is Nothing Or TypeName(ActiveSheet) <> "Worksheet" Then
        Query_Comments = CoHunsuJson_Ok("{""sheet"":"""",""notes"":0,""threads"":0,""items"":[],""truncated"":false}")
        Exit Function
    End If
    Set ws = ActiveSheet
    For Each cm In ws.Comments
        notes = notes + 1
        If n < ITEM_LIMIT Then
            t = ""
            On Error Resume Next
            t = CStr(cm.Text)
            On Error GoTo Fail
            n = AddItem(buf, n, cm.Parent.Address(False, False), "note", t)
        End If
    Next cm
    Dim th As Object
    On Error Resume Next
    Set th = CallByName(ws, "CommentsThreaded", VbGet)
    Err.Clear
    On Error GoTo Fail
    If Not th Is Nothing Then
        For Each cm In th
            threads = threads + 1
            If n < ITEM_LIMIT Then
                t = ""
                On Error Resume Next
                t = CStr(cm.Text)
                On Error GoTo Fail
                n = AddItem(buf, n, cm.Parent.Address(False, False), "thread", t)
            End If
        Next cm
    End If
    Query_Comments = CoHunsuJson_Ok("{" & CoHunsuJson_Pair("sheet", CoHunsuJson_Str(CStr(ws.Name))) & "," & _
        CoHunsuJson_Pair("notes", CStr(notes)) & "," & CoHunsuJson_Pair("threads", CStr(threads)) & "," & _
        CoHunsuJson_Pair("items", "[" & buf & "]") & "," & _
        CoHunsuJson_Pair("truncated", IIf(notes + threads > n, "true", "false")) & "}")
    Exit Function
Fail:
    Query_Comments = CoHunsuJson_Fail("query_failed", "comments 오류: " & Err.Description)
End Function

Private Function AddItem(ByRef buf As String, ByVal n As Long, ByVal addr As String, ByVal kind As String, ByVal text As String) As Long
    text = Replace(Replace(text, vbCr, " "), vbLf, " ")
    If Len(text) > TEXT_LIMIT Then text = Left$(text, TEXT_LIMIT) & "…"
    If n > 0 Then buf = buf & ","
    buf = buf & "{" & CoHunsuJson_Pair("addr", CoHunsuJson_Str(addr)) & "," & _
        CoHunsuJson_Pair("kind", CoHunsuJson_Str(kind)) & "," & _
        CoHunsuJson_Pair("text", CoHunsuJson_Str(text)) & "}"
    AddItem = n + 1
End Function
