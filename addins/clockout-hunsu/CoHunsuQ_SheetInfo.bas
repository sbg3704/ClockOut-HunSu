Attribute VB_Name = "CoHunsuQ_SheetInfo"
'==============================================================
' 기능: 조회 sheet.info. 지금 시트의 사용 범위, 보호, 자동 필터와 필터 적용 여부, 틀 고정, 조건부 서식 수. 칸 값은 읽지 않는다.
' 사용법: CoHunsu_Query "sheet.info", "{}" -> {"name":"매출","used":"A1:F200","rows":200,"cols":6,"cells":1200,"protected":false,"autoFilter":true,"filtered":false,"frozen":true,"condFormats":2}
' 의존: CoHunsuJson. 읽기 전용. cells는 큰 시트에서 Long을 넘을 수 있어 Double로 계산한다.
'==============================================================
Option Explicit

Public Function Query_SheetInfo(ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim ws As Worksheet, u As Range, usedAddr As String, r As Double, c As Double
    Dim prot As Boolean, af As Boolean, flt As Boolean, frz As Boolean, cf As Long
    If ActiveSheet Is Nothing Or TypeName(ActiveSheet) <> "Worksheet" Then
        Query_SheetInfo = CoHunsuJson_Fail("range_bad", "활성 시트가 워크시트가 아님")
        Exit Function
    End If
    Set ws = ActiveSheet
    On Error Resume Next
    Set u = ws.UsedRange
    If Not u Is Nothing Then
        usedAddr = u.Address(False, False)
        r = u.Rows.Count
        c = u.Columns.Count
    End If
    prot = ws.ProtectContents
    af = ws.AutoFilterMode
    flt = ws.FilterMode
    frz = ActiveWindow.FreezePanes
    cf = ws.Cells.FormatConditions.Count
    Err.Clear
    On Error GoTo Fail
    Query_SheetInfo = CoHunsuJson_Ok("{" & CoHunsuJson_Pair("name", CoHunsuJson_Str(CStr(ws.Name))) & "," & _
        CoHunsuJson_Pair("used", CoHunsuJson_Str(usedAddr)) & "," & _
        CoHunsuJson_Pair("rows", CoHunsuJson_Num(r)) & "," & _
        CoHunsuJson_Pair("cols", CoHunsuJson_Num(c)) & "," & _
        CoHunsuJson_Pair("cells", CoHunsuJson_Num(r * c)) & "," & _
        CoHunsuJson_Pair("protected", IIf(prot, "true", "false")) & "," & _
        CoHunsuJson_Pair("autoFilter", IIf(af, "true", "false")) & "," & _
        CoHunsuJson_Pair("filtered", IIf(flt, "true", "false")) & "," & _
        CoHunsuJson_Pair("frozen", IIf(frz, "true", "false")) & "," & _
        CoHunsuJson_Pair("condFormats", CStr(cf)) & "}")
    Exit Function
Fail:
    Query_SheetInfo = CoHunsuJson_Fail("query_failed", "sheet.info 오류: " & Err.Description)
End Function
