Attribute VB_Name = "CoHunsuQ_Validation"
'==============================================================
' 기능: 조회 validation. 지금 시트에서 데이터 유효성(드롭다운 등)이 걸린 영역 목록. 규칙 내용(목록 원본 등)은 돌려주지 않는다.
' 사용법: CoHunsu_Query "validation", "{}" -> {"sheet":"매출","areas":2,"cells":40,"items":[{"addr":"B2:B21","kind":"목록","cells":20}],"truncated":false}
' 의존: CoHunsuJson. 읽기 전용. SpecialCells(AllValidation)는 해당 칸이 없으면 오류라 그것을 0으로 본다.
'==============================================================
Option Explicit

Private Const AREA_LIMIT As Long = 20

Public Function Query_Validation(ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim ws As Worksheet, r As Range, a As Range, buf As String, n As Long, total As Long, cells As Double
    If ActiveSheet Is Nothing Or TypeName(ActiveSheet) <> "Worksheet" Then
        Query_Validation = CoHunsuJson_Fail("range_bad", "활성 시트가 워크시트가 아님")
        Exit Function
    End If
    Set ws = ActiveSheet
    On Error Resume Next
    Set r = ws.Cells.SpecialCells(xlCellTypeAllValidation)
    Err.Clear
    On Error GoTo Fail
    If Not r Is Nothing Then
        For Each a In r.Areas
            total = total + 1
            cells = cells + a.CountLarge
            If n < AREA_LIMIT Then
                If n > 0 Then buf = buf & ","
                buf = buf & "{" & CoHunsuJson_Pair("addr", CoHunsuJson_Str(a.Address(False, False))) & "," & _
                    CoHunsuJson_Pair("kind", CoHunsuJson_Str(KindName(a))) & "," & _
                    CoHunsuJson_Pair("cells", CoHunsuJson_Num(a.CountLarge)) & "}"
                n = n + 1
            End If
        Next a
    End If
    Query_Validation = CoHunsuJson_Ok("{" & CoHunsuJson_Pair("sheet", CoHunsuJson_Str(CStr(ws.Name))) & "," & _
        CoHunsuJson_Pair("areas", CStr(total)) & "," & CoHunsuJson_Pair("cells", CoHunsuJson_Num(cells)) & "," & _
        CoHunsuJson_Pair("items", "[" & buf & "]") & "," & _
        CoHunsuJson_Pair("truncated", IIf(total > n, "true", "false")) & "}")
    Exit Function
Fail:
    Query_Validation = CoHunsuJson_Fail("query_failed", "validation 오류: " & Err.Description)
End Function

Private Function KindName(ByVal a As Range) As String
    Dim t As Long
    t = -1
    On Error Resume Next
    t = a.Cells(1, 1).Validation.Type
    On Error GoTo 0
    Select Case t
        Case 1: KindName = "정수"
        Case 2: KindName = "소수"
        Case 3: KindName = "목록"
        Case 4: KindName = "날짜"
        Case 5: KindName = "시간"
        Case 6: KindName = "글자 수"
        Case 7: KindName = "사용자 수식"
        Case Else: KindName = "기타"
    End Select
End Function
