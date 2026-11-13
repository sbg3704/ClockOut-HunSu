Attribute VB_Name = "CoHunsuQ_Names"
'==============================================================
' 기능: 조회 names. 지금 통합 문서의 이름(Name) 목록과, 칸을 가리키는 이름이면 그 시트와 주소를 돌려준다(눌러서 이동할 수 있게).
' 사용법: CoHunsu_Query "names", "{}" -> {"count":5,"items":[{"name":"세율","sheet":"설정","addr":"B2","where":"설정!B2"}],"truncated":false}
'         수식·상수 이름(칸을 가리키지 않음)은 sheet, addr가 빈 글자이고 where는 "수식·상수".
' 의존: CoHunsuJson. 읽기 전용. 숨은 이름과 엑셀 내부 이름(_xlfn 등)은 뺀다. 최대 NAME_LIMIT개.
'==============================================================
Option Explicit

Private Const NAME_LIMIT As Long = 100

Public Function Query_Names(ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim wb As Workbook, nm As Name, buf As String, n As Long, total As Long
    Dim r As Range, sheetName As String, addr As String, nameText As String, whereText As String
    Set wb = ActiveWorkbook
    If wb Is Nothing Then
        Query_Names = CoHunsuJson_Fail("range_bad", "열린 통합 문서가 없음")
        Exit Function
    End If
    For Each nm In wb.Names
        nameText = CStr(nm.Name)
        If nm.Visible And InStr(1, nameText, "_xl", vbTextCompare) = 0 Then
            total = total + 1
            If n < NAME_LIMIT Then
                sheetName = "": addr = "": Set r = Nothing
                On Error Resume Next
                Set r = nm.RefersToRange
                If Err.Number = 0 And Not r Is Nothing Then
                    sheetName = CStr(r.Worksheet.Name)
                    addr = r.Address(False, False)
                End If
                Err.Clear
                On Error GoTo Fail
                If Len(addr) > 0 Then whereText = sheetName & "!" & addr Else whereText = "수식·상수"
                If n > 0 Then buf = buf & ","
                buf = buf & "{" & CoHunsuJson_Pair("name", CoHunsuJson_Str(nameText)) & "," & _
                    CoHunsuJson_Pair("sheet", CoHunsuJson_Str(sheetName)) & "," & _
                    CoHunsuJson_Pair("addr", CoHunsuJson_Str(addr)) & "," & _
                    CoHunsuJson_Pair("where", CoHunsuJson_Str(whereText)) & "}"
                n = n + 1
            End If
        End If
    Next nm
    Query_Names = CoHunsuJson_Ok("{" & CoHunsuJson_Pair("count", CStr(total)) & "," & _
        CoHunsuJson_Pair("items", "[" & buf & "]") & "," & _
        CoHunsuJson_Pair("truncated", IIf(total > n, "true", "false")) & "}")
    Exit Function
Fail:
    Query_Names = CoHunsuJson_Fail("query_failed", "names 오류: " & Err.Description)
End Function
