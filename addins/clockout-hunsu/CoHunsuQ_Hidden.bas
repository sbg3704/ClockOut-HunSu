Attribute VB_Name = "CoHunsuQ_Hidden"
'==============================================================
' 기능: 조회 hidden. 지금 시트의 숨은 행과 숨은 열. 사용 범위 안에서만 훑고 행은 앞 ROW_SCAN줄까지만 본다(큰 시트에서 느려지지 않게).
' 사용법: CoHunsu_Query "hidden", "{}" -> {"sheet":"매출","hiddenRows":3,"hiddenCols":1,"limited":false,"items":[{"label":"5행","addr":"A5"},{"label":"C열","addr":"C1"}],"truncated":false}
' 의존: CoHunsuJson. 읽기 전용. limited:true 이면 앞 ROW_SCAN줄만 본 결과다.
'==============================================================
Option Explicit

Private Const ROW_SCAN As Long = 3000
Private Const ITEM_LIMIT As Long = 20

Public Function Query_Hidden(ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim ws As Worksheet, u As Range, r1 As Long, rN As Long, c1 As Long, cN As Long, i As Long
    Dim hr As Long, hc As Long, n As Long, buf As String, limited As Boolean, colLetter As String
    If ActiveSheet Is Nothing Or TypeName(ActiveSheet) <> "Worksheet" Then
        Query_Hidden = CoHunsuJson_Fail("range_bad", "활성 시트가 워크시트가 아님")
        Exit Function
    End If
    Set ws = ActiveSheet
    Set u = ws.UsedRange
    r1 = u.Row: c1 = u.Column
    rN = r1 + u.Rows.Count - 1
    cN = c1 + u.Columns.Count - 1
    If rN - r1 + 1 > ROW_SCAN Then
        rN = r1 + ROW_SCAN - 1
        limited = True
    End If
    If cN - c1 + 1 > 256 Then cN = c1 + 255
    For i = r1 To rN
        If ws.Rows(i).Hidden Then
            hr = hr + 1
            If n < ITEM_LIMIT Then n = AddItem(buf, n, CStr(i) & "행", "A" & CStr(i))
        End If
    Next i
    For i = c1 To cN
        If ws.Columns(i).Hidden Then
            hc = hc + 1
            colLetter = Split(ws.Cells(1, i).Address(True, False), "$")(0)
            If n < ITEM_LIMIT Then n = AddItem(buf, n, colLetter & "열", colLetter & "1")
        End If
    Next i
    Query_Hidden = CoHunsuJson_Ok("{" & CoHunsuJson_Pair("sheet", CoHunsuJson_Str(CStr(ws.Name))) & "," & _
        CoHunsuJson_Pair("hiddenRows", CStr(hr)) & "," & CoHunsuJson_Pair("hiddenCols", CStr(hc)) & "," & _
        CoHunsuJson_Pair("limited", IIf(limited, "true", "false")) & "," & _
        CoHunsuJson_Pair("items", "[" & buf & "]") & "," & _
        CoHunsuJson_Pair("truncated", IIf(hr + hc > n, "true", "false")) & "}")
    Exit Function
Fail:
    Query_Hidden = CoHunsuJson_Fail("query_failed", "hidden 오류: " & Err.Description)
End Function

Private Function AddItem(ByRef buf As String, ByVal n As Long, ByVal label As String, ByVal addr As String) As Long
    If n > 0 Then buf = buf & ","
    buf = buf & "{" & CoHunsuJson_Pair("label", CoHunsuJson_Str(label)) & "," & CoHunsuJson_Pair("addr", CoHunsuJson_Str(addr)) & "}"
    AddItem = n + 1
End Function
