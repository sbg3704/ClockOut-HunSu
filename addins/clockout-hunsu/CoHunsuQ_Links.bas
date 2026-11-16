Attribute VB_Name = "CoHunsuQ_Links"
'==============================================================
' 기능: 조회 links. 이 통합 문서가 다른 엑셀 파일을 가리키는 외부 링크 목록. 파일 이름만 돌려주고 경로는 돌려주지 않는다(docs/04 7장).
' 사용법: CoHunsu_Query "links", "{}" -> {"count":2,"items":[{"file":"원본.xlsx"}],"truncated":false}
' 의존: CoHunsuJson. 읽기 전용, 링크를 갱신하거나 열지 않는다.
'==============================================================
Option Explicit

Private Const LIST_LIMIT As Long = 30

Public Function Query_Links(ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim wb As Workbook, v As Variant, i As Long, total As Long, n As Long, buf As String, p As String, k As Long
    Set wb = ActiveWorkbook
    If wb Is Nothing Then
        Query_Links = CoHunsuJson_Fail("range_bad", "열린 통합 문서가 없음")
        Exit Function
    End If
    On Error Resume Next
    v = wb.LinkSources(xlExcelLinks)
    If Err.Number <> 0 Then v = Empty
    Err.Clear
    On Error GoTo Fail
    If IsArray(v) Then
        total = UBound(v) - LBound(v) + 1
        For i = LBound(v) To UBound(v)
            If n >= LIST_LIMIT Then Exit For
            p = CStr(v(i))
            k = InStrRev(p, "\")
            If InStrRev(p, "/") > k Then k = InStrRev(p, "/")
            If k > 0 Then p = Mid$(p, k + 1)
            If n > 0 Then buf = buf & ","
            buf = buf & "{" & CoHunsuJson_Pair("file", CoHunsuJson_Str(p)) & "}"
            n = n + 1
        Next i
    End If
    Query_Links = CoHunsuJson_Ok("{" & CoHunsuJson_Pair("count", CStr(total)) & "," & _
        CoHunsuJson_Pair("items", "[" & buf & "]") & "," & _
        CoHunsuJson_Pair("truncated", IIf(total > n, "true", "false")) & "}")
    Exit Function
Fail:
    Query_Links = CoHunsuJson_Fail("query_failed", "links 오류: " & Err.Description)
End Function
