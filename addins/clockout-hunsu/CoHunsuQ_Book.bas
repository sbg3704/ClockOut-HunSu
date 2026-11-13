Attribute VB_Name = "CoHunsuQ_Book"
'==============================================================
' 기능: 조회 book.info(책 요약)와 book.audit(점검 숫자). 읽기 전용이고 셀 값을 읽지 않는다(시트 수, 이름 수 같은 구조 정보만).
' 사용법: CoHunsu_Query "book.info", "{}"  -> {"name":"가.xlsx","saved":true,"readOnly":false,"calcMode":"auto","sheets":3,"activeSheet":"매출","structureProtected":false}
'         CoHunsu_Query "book.audit", "{}" -> {"hidden":1,"veryHidden":0,"protectedSheets":2,"links":0,"names":5,"hiddenList":[{"name":"원본","kind":"hidden"}],"truncated":false}
'         calcMode: auto, manual, semi. kind: hidden(사용자가 다시 표시 가능), veryHidden(VBA로만 표시 가능).
' 의존: CoHunsuJson. 통합 문서 경로는 돌려주지 않는다(docs/04 7장: 경로를 기록하지 않음).
'==============================================================
Option Explicit

Private Const LIST_LIMIT As Long = 30

Public Function Query_BookInfo(ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim wb As Workbook, mode As String, activeName As String
    Set wb = ActiveWorkbook
    If wb Is Nothing Then
        Query_BookInfo = CoHunsuJson_Fail("range_bad", "열린 통합 문서가 없음")
        Exit Function
    End If
    Select Case Application.Calculation
        Case xlCalculationAutomatic: mode = "auto"
        Case xlCalculationManual: mode = "manual"
        Case Else: mode = "semi"
    End Select
    On Error Resume Next
    activeName = CStr(wb.ActiveSheet.Name)
    On Error GoTo Fail
    Query_BookInfo = CoHunsuJson_Ok("{" & CoHunsuJson_Pair("name", CoHunsuJson_Str(wb.Name)) & "," & _
        CoHunsuJson_Pair("saved", IIf(wb.Saved, "true", "false")) & "," & _
        CoHunsuJson_Pair("readOnly", IIf(wb.ReadOnly, "true", "false")) & "," & _
        CoHunsuJson_Pair("calcMode", CoHunsuJson_Str(mode)) & "," & _
        CoHunsuJson_Pair("sheets", CStr(wb.Sheets.Count)) & "," & _
        CoHunsuJson_Pair("activeSheet", CoHunsuJson_Str(activeName)) & "," & _
        CoHunsuJson_Pair("structureProtected", IIf(wb.ProtectStructure, "true", "false")) & "}")
    Exit Function
Fail:
    Query_BookInfo = CoHunsuJson_Fail("query_failed", "book.info 오류: " & Err.Description)
End Function

Public Function Query_BookAudit(ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim wb As Workbook, sh As Object, buf As String, n As Long
    Dim hidden As Long, veryHidden As Long, protectedCount As Long, links As Long, truncated As Boolean
    Dim v As Variant, kind As String
    Set wb = ActiveWorkbook
    If wb Is Nothing Then
        Query_BookAudit = CoHunsuJson_Fail("range_bad", "열린 통합 문서가 없음")
        Exit Function
    End If
    For Each sh In wb.Sheets
        kind = ""
        If sh.Visible = xlSheetHidden Then
            hidden = hidden + 1
            kind = "hidden"
        ElseIf sh.Visible = xlSheetVeryHidden Then
            veryHidden = veryHidden + 1
            kind = "veryHidden"
        End If
        If Len(kind) > 0 Then
            If n < LIST_LIMIT Then
                If n > 0 Then buf = buf & ","
                buf = buf & "{" & CoHunsuJson_Pair("name", CoHunsuJson_Str(CStr(sh.Name))) & "," & CoHunsuJson_Pair("kind", CoHunsuJson_Str(kind)) & "}"
                n = n + 1
            Else
                truncated = True
            End If
        End If
        If TypeName(sh) = "Worksheet" Then
            If sh.ProtectContents Then protectedCount = protectedCount + 1
        End If
    Next sh
    On Error Resume Next
    v = wb.LinkSources(xlExcelLinks)
    If Err.Number = 0 Then
        If IsArray(v) Then links = UBound(v) - LBound(v) + 1
    End If
    Err.Clear
    On Error GoTo Fail
    Query_BookAudit = CoHunsuJson_Ok("{" & CoHunsuJson_Pair("hidden", CStr(hidden)) & "," & _
        CoHunsuJson_Pair("veryHidden", CStr(veryHidden)) & "," & _
        CoHunsuJson_Pair("protectedSheets", CStr(protectedCount)) & "," & _
        CoHunsuJson_Pair("links", CStr(links)) & "," & _
        CoHunsuJson_Pair("names", CStr(wb.Names.Count)) & "," & _
        CoHunsuJson_Pair("hiddenList", "[" & buf & "]") & "," & _
        CoHunsuJson_Pair("truncated", IIf(truncated, "true", "false")) & "}")
    Exit Function
Fail:
    Query_BookAudit = CoHunsuJson_Fail("query_failed", "book.audit 오류: " & Err.Description)
End Function
