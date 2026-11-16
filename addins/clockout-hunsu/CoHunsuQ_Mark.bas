Attribute VB_Name = "CoHunsuQ_Mark"
'==============================================================
' 기능: 조회 mark. 셀 위 표시층(앱이 엑셀 칸 위에 테두리를 그림)에 쓸 칸 자리를 화면 픽셀로 돌려준다. 지금 보이는 곳(창의 VisibleRange)만 본다.
'       종류: errors(오류 칸), pre(활성 셀이 바로 쓰는 칸, 같은 시트), dep(활성 셀을 바로 쓰는 칸, 같은 시트), active(활성 셀).
' 사용법: CoHunsu_Query "mark", "{}"
'         -> {"sheet":"매출","zoom":100,"panes":[{"x":10,"y":200,"w":900,"h":600}],
'             "errors":[{"addr":"C5","x":110,"y":260,"w":64,"h":20}],"pre":[...],"dep":[...],"active":[...],"truncated":false}
'         x,y,w,h는 화면 픽셀(모니터 배율 그대로). 칸이 틀 고정(나눈 창)의 어느 칸에 보이는지 찾아 그 칸(Pane)으로 바꾼다.
' 의존: CoHunsuJson. 읽기 전용(선택·스크롤을 바꾸지 않음). 스크롤에는 엑셀 이벤트가 없어 앱이 엑셀이 앞에 있을 때만 느리게 다시 묻는다(settings.markPollMs).
'       자리 계산은 Pane.PointsToScreenPixelsX/Y(시트 좌표 포인트 → 화면 픽셀). 확대·배율이 다른 PC에서 어긋나는지는 PC에서 눈으로 확인해야 한다(docs/08).
'==============================================================
Option Explicit

Private Const MARK_LIMIT As Long = 200

Private mN As Long

Public Function Query_Mark(ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim w As Window, ws As Worksheet, vis As Range, c As Range, found As Range, part As Range, d As Range
    Dim panes As String, i As Long, cut As Boolean
    Set w = ActiveWindow
    If w Is Nothing Or TypeName(ActiveSheet) <> "Worksheet" Then
        Query_Mark = CoHunsuJson_Ok("{""panes"":[],""errors"":[],""pre"":[],""dep"":[],""active"":[],""truncated"":false}")
        Exit Function
    End If
    Set ws = ActiveSheet
    Set vis = w.VisibleRange
    For i = 1 To w.Panes.Count
        If i > 1 Then panes = panes & ","
        panes = panes & RectJson(w.Panes(i), w.Panes(i).VisibleRange, "")
    Next i
    mN = 0
    ' 오류 칸: 보이는 곳 안의 수식 오류와 입력된 오류 값
    On Error Resume Next
    Set found = vis.SpecialCells(xlCellTypeFormulas, xlErrors)
    Err.Clear
    Set part = vis.SpecialCells(xlCellTypeConstants, xlErrors)
    Err.Clear
    Set c = ActiveCell
    Err.Clear
    On Error GoTo Fail
    If found Is Nothing Then
        Set found = part
    ElseIf Not part Is Nothing Then
        Set found = Application.Union(found, part)
    End If
    Query_Mark = "{" & CoHunsuJson_Pair("sheet", CoHunsuJson_Str(ws.Name)) & "," & _
        CoHunsuJson_Pair("zoom", CoHunsuJson_Num(w.Zoom)) & "," & _
        CoHunsuJson_Pair("panes", "[" & panes & "]") & "," & _
        CoHunsuJson_Pair("errors", CellsJson(w, found, vis, cut))
    Set d = Nothing
    If Not c Is Nothing Then
        If c.HasFormula Then
            On Error Resume Next
            Set d = c.DirectPrecedents
            Err.Clear
            On Error GoTo Fail
        End If
    End If
    Query_Mark = Query_Mark & "," & CoHunsuJson_Pair("pre", CellsJson(w, d, vis, cut))
    Set d = Nothing
    If Not c Is Nothing Then
        On Error Resume Next
        Set d = c.DirectDependents
        Err.Clear
        On Error GoTo Fail
    End If
    Query_Mark = Query_Mark & "," & CoHunsuJson_Pair("dep", CellsJson(w, d, vis, cut))
    Query_Mark = Query_Mark & "," & CoHunsuJson_Pair("active", CellsJson(w, c, vis, cut))
    Query_Mark = CoHunsuJson_Ok(Query_Mark & "," & CoHunsuJson_Pair("truncated", IIf(cut, "true", "false")) & "}")
    Exit Function
Fail:
    Query_Mark = CoHunsuJson_Fail("query_failed", "mark 오류: " & Err.Description)
End Function

' 영역 목록을 자리 배열로. 보이는 곳과 겹치는 부분만, 영역 하나는 사각형 하나(칸마다가 아님).
Private Function CellsJson(ByVal w As Window, ByVal r As Range, ByVal vis As Range, ByRef cut As Boolean) As String
    Dim buf As String, area As Range, part As Range, n As Long, p As Pane
    If r Is Nothing Then
        CellsJson = "[]"
        Exit Function
    End If
    If r.Worksheet.Name <> vis.Worksheet.Name Then
        CellsJson = "[]"
        Exit Function
    End If
    For Each area In r.Areas
        Set part = Application.Intersect(area, vis)
        If Not part Is Nothing Then
            If mN >= MARK_LIMIT Then
                cut = True
                Exit For
            End If
            Set p = PaneFor(w, part)
            If Not p Is Nothing Then
                If n > 0 Then buf = buf & ","
                buf = buf & RectJson(p, part, part.Address(False, False))
                n = n + 1
                mN = mN + 1
            End If
        End If
    Next area
    CellsJson = "[" & buf & "]"
End Function

' 틀 고정·창 나누기에서 그 영역의 왼쪽 위 칸이 보이는 칸(Pane).
Private Function PaneFor(ByVal w As Window, ByVal r As Range) As Pane
    Dim i As Long
    For i = 1 To w.Panes.Count
        If Not Application.Intersect(w.Panes(i).VisibleRange, r.Cells(1, 1)) Is Nothing Then
            Set PaneFor = w.Panes(i)
            Exit Function
        End If
    Next i
End Function

Private Function RectJson(ByVal p As Pane, ByVal r As Range, ByVal addr As String) As String
    Dim x1 As Long, y1 As Long, x2 As Long, y2 As Long
    x1 = p.PointsToScreenPixelsX(r.Left)
    y1 = p.PointsToScreenPixelsY(r.Top)
    x2 = p.PointsToScreenPixelsX(r.Left + r.Width)
    y2 = p.PointsToScreenPixelsY(r.Top + r.Height)
    RectJson = "{" & IIf(Len(addr) > 0, CoHunsuJson_Pair("addr", CoHunsuJson_Str(addr)) & ",", "") & _
        CoHunsuJson_Pair("x", CStr(x1)) & "," & CoHunsuJson_Pair("y", CStr(y1)) & "," & _
        CoHunsuJson_Pair("w", CStr(x2 - x1)) & "," & CoHunsuJson_Pair("h", CStr(y2 - y1)) & "}"
End Function
