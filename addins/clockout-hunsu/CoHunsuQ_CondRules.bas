Attribute VB_Name = "CoHunsuQ_CondRules"
'==============================================================
' 기능: 조회 condrules. 지금 시트의 조건부 서식 규칙 목록(적용 범위와 종류). 규칙의 수식·색은 돌려주지 않는다.
' 사용법: CoHunsu_Query "condrules", "{}" -> {"sheet":"매출","count":3,"items":[{"addr":"C2:C50","kind":"셀 값"}],"truncated":false}
' 의존: CoHunsuJson. 읽기 전용. 최대 RULE_LIMIT개.
'==============================================================
Option Explicit

Private Const RULE_LIMIT As Long = 30

Public Function Query_CondRules(ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim ws As Worksheet, fcs As Object, fc As Object, i As Long, total As Long, n As Long, buf As String
    Dim addr As String, t As Long
    If ActiveSheet Is Nothing Or TypeName(ActiveSheet) <> "Worksheet" Then
        Query_CondRules = CoHunsuJson_Fail("range_bad", "활성 시트가 워크시트가 아님")
        Exit Function
    End If
    Set ws = ActiveSheet
    Set fcs = ws.Cells.FormatConditions
    total = fcs.Count
    For i = 1 To total
        If n >= RULE_LIMIT Then Exit For
        addr = "": t = -1
        On Error Resume Next
        Set fc = fcs.Item(i)
        addr = CStr(fc.AppliesTo.Address(False, False))
        t = CLng(fc.Type)
        On Error GoTo Fail
        If n > 0 Then buf = buf & ","
        buf = buf & "{" & CoHunsuJson_Pair("addr", CoHunsuJson_Str(addr)) & "," & _
            CoHunsuJson_Pair("kind", CoHunsuJson_Str(KindName(t))) & "}"
        n = n + 1
    Next i
    Query_CondRules = CoHunsuJson_Ok("{" & CoHunsuJson_Pair("sheet", CoHunsuJson_Str(CStr(ws.Name))) & "," & _
        CoHunsuJson_Pair("count", CStr(total)) & "," & CoHunsuJson_Pair("items", "[" & buf & "]") & "," & _
        CoHunsuJson_Pair("truncated", IIf(total > n, "true", "false")) & "}")
    Exit Function
Fail:
    Query_CondRules = CoHunsuJson_Fail("query_failed", "condrules 오류: " & Err.Description)
End Function

Private Function KindName(ByVal t As Long) As String
    Select Case t
        Case 1: KindName = "셀 값"
        Case 2: KindName = "수식"
        Case 3: KindName = "색조"
        Case 4: KindName = "데이터 막대"
        Case 5: KindName = "상위·하위"
        Case 6: KindName = "아이콘"
        Case 8: KindName = "중복·고유"
        Case 9: KindName = "텍스트"
        Case 10: KindName = "빈 칸"
        Case 11: KindName = "날짜"
        Case 12: KindName = "평균"
        Case 13: KindName = "빈 칸 아님"
        Case 16: KindName = "오류"
        Case 17: KindName = "오류 아님"
        Case Else: KindName = "기타"
    End Select
End Function
