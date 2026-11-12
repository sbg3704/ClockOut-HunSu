Attribute VB_Name = "CoHunsuQ_Sheets"
'==============================================================
' 기능: 조회 sheets(docs/02 5장)와 명령 goto, sheet.activate. 지금 통합 문서의 보이는 시트 목록을 돌려주고, 사용자가 누른 시트나 칸으로 이동한다.
' 사용법: CoHunsu_Query "sheets", "{}" -> {"book":"가.xlsx","active":"매출","count":3,"items":[{"index":1,"name":"매출","visible":true,"active":true}]}
'         지금은 보이는 시트만 담는다(visible은 늘 true). 탭 색(tab)은 다음 단계에서 더한다.
'         CoHunsu_Do "sheet.activate", "{""name"":""매출""}"      CoHunsu_Do "goto", "{""sheet"":""매출"",""addr"":""C5""}"
' 의존: CoHunsuJson(CoHunsuJson_GetStr). 목록 조회는 읽기 전용. 명령은 사용자가 훈수꾼 화면을 눌렀을 때만 오고 활성 시트·선택만 바꾼다(셀 값은 바꾸지 않음, docs/04 3장).
'==============================================================
Option Explicit

Private Const SHEET_LIMIT As Long = 100

Public Function Query_Sheets(ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim wb As Workbook, sh As Object, buf As String, n As Long, activeName As String, idx As Long
    Set wb = ActiveWorkbook
    If wb Is Nothing Then
        Query_Sheets = CoHunsuJson_Fail("range_bad", "열린 통합 문서가 없음")
        Exit Function
    End If
    On Error Resume Next
    activeName = CStr(wb.ActiveSheet.Name)
    On Error GoTo Fail
    For Each sh In wb.Sheets
        idx = idx + 1
        If sh.Visible = xlSheetVisible Then
            If n >= SHEET_LIMIT Then Exit For
            If n > 0 Then buf = buf & ","
            buf = buf & "{" & CoHunsuJson_Pair("name", CoHunsuJson_Str(CStr(sh.Name))) & "," & _
                CoHunsuJson_Pair("index", CStr(idx)) & "," & _
                CoHunsuJson_Pair("visible", "true") & "," & _
                CoHunsuJson_Pair("active", IIf(CStr(sh.Name) = activeName, "true", "false")) & "}"
            n = n + 1
        End If
    Next sh
    Query_Sheets = CoHunsuJson_Ok("{" & CoHunsuJson_Pair("book", CoHunsuJson_Str(wb.Name)) & "," & _
        CoHunsuJson_Pair("active", CoHunsuJson_Str(activeName)) & "," & CoHunsuJson_Pair("count", CStr(n)) & "," & _
        CoHunsuJson_Pair("items", "[" & buf & "]") & "}")
    Exit Function
Fail:
    Query_Sheets = CoHunsuJson_Fail("query_failed", "sheets 오류: " & Err.Description)
End Function

Public Function Do_SheetActivate(ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim nm As String
    nm = CoHunsuJson_GetStr(argsJson, "name")
    If Len(nm) = 0 Or Len(nm) > 31 Then
        Do_SheetActivate = CoHunsuJson_Fail("command_failed", "시트 이름이 비었거나 너무 김")
        Exit Function
    End If
    ActiveWorkbook.Sheets(nm).Activate
    Do_SheetActivate = CoHunsuJson_Ok("{""name"":" & CoHunsuJson_Str(nm) & "}")
    Exit Function
Fail:
    Do_SheetActivate = CoHunsuJson_Fail("command_failed", "시트 이동 실패: " & Err.Description)
End Function

Public Function Do_Goto(ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim sheetName As String, addr As String, ws As Worksheet
    sheetName = CoHunsuJson_GetStr(argsJson, "sheet")
    addr = CoHunsuJson_GetStr(argsJson, "addr")
    If Len(addr) = 0 Or Len(addr) > 255 Then
        Do_Goto = CoHunsuJson_Fail("command_failed", "주소가 비었거나 너무 김")
        Exit Function
    End If
    If Len(sheetName) > 0 Then
        ActiveWorkbook.Sheets(sheetName).Activate
    End If
    Set ws = ActiveSheet
    Application.Goto ws.Range(addr), True
    Do_Goto = CoHunsuJson_Ok("{""addr"":" & CoHunsuJson_Str(addr) & "}")
    Exit Function
Fail:
    Do_Goto = CoHunsuJson_Fail("command_failed", "이동 실패: " & Err.Description)
End Function
