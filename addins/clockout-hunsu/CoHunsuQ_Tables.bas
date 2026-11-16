Attribute VB_Name = "CoHunsuQ_Tables"
'==============================================================
' 기능: 조회 tables. 통합 문서 안의 표(ListObject) 목록. 시트, 표 이름, 범위 주소, 데이터 줄 수. 누르면 이동할 수 있게 sheet와 addr를 준다.
' 사용법: CoHunsu_Query "tables", "{}" -> {"count":2,"items":[{"name":"표1","sheet":"매출","addr":"A1:D20","rows":19}],"truncated":false}
' 의존: CoHunsuJson. 읽기 전용, 칸 값을 읽지 않는다. 최대 LIST_LIMIT개.
'==============================================================
Option Explicit

Private Const LIST_LIMIT As Long = 50

Public Function Query_Tables(ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim wb As Workbook, sh As Object, lo As ListObject, buf As String, n As Long, total As Long, rowsN As Long
    Set wb = ActiveWorkbook
    If wb Is Nothing Then
        Query_Tables = CoHunsuJson_Fail("range_bad", "열린 통합 문서가 없음")
        Exit Function
    End If
    For Each sh In wb.Sheets
        If TypeName(sh) = "Worksheet" Then
            For Each lo In sh.ListObjects
                total = total + 1
                If n < LIST_LIMIT Then
                    rowsN = 0
                    On Error Resume Next
                    rowsN = lo.DataBodyRange.Rows.Count
                    Err.Clear
                    On Error GoTo Fail
                    If n > 0 Then buf = buf & ","
                    buf = buf & "{" & CoHunsuJson_Pair("name", CoHunsuJson_Str(CStr(lo.Name))) & "," & _
                        CoHunsuJson_Pair("sheet", CoHunsuJson_Str(CStr(sh.Name))) & "," & _
                        CoHunsuJson_Pair("addr", CoHunsuJson_Str(lo.Range.Address(False, False))) & "," & _
                        CoHunsuJson_Pair("rows", CStr(rowsN)) & "}"
                    n = n + 1
                End If
            Next lo
        End If
    Next sh
    Query_Tables = CoHunsuJson_Ok("{" & CoHunsuJson_Pair("count", CStr(total)) & "," & _
        CoHunsuJson_Pair("items", "[" & buf & "]") & "," & _
        CoHunsuJson_Pair("truncated", IIf(total > n, "true", "false")) & "}")
    Exit Function
Fail:
    Query_Tables = CoHunsuJson_Fail("query_failed", "tables 오류: " & Err.Description)
End Function
