Attribute VB_Name = "CoHunsuQ_Mark"
'==============================================================
' 기능: 조회 mark. 셀 위 표시층(앱이 엑셀 칸 위에 테두리를 그림)에 쓸 칸 자리를 화면 픽셀로 돌려준다. 지금 보이는 곳(창의 VisibleRange)만 본다.
'       pre2/dep2(2026-11-16): pre·dep 덩어리가 다시 쓰는 칸·쓰이는 칸(같은 시트, to = 이어지는 pre/dep 덩어리 주소).
'       종류: errors(오류 칸), pre(활성 셀이 바로 쓰는 칸, 같은 시트), dep(활성 셀을 바로 쓰는 칸, 같은 시트), active(활성 셀),
'             rowband(고른 칸의 행 띠: 격자(pane)마다 가로로 한 장), colband(고른 칸의 열 띠, 세로로 한 장). 십자 음영 모듈(hunsu-crosshair)이 쓴다(2026-11-16).
' 사용법: CoHunsu_Query "mark", "{}"
'         -> {"sheet":"매출","zoom":100,"panes":[{"x":10,"y":200,"w":900,"h":600}],
'             "errors":[{"addr":"C5","x":110,"y":260,"w":64,"h":20}],"pre":[...],"dep":[...],"pre2":[{"addr":"A1",...,"to":"B6"}],"dep2":[...],"active":[...],"truncated":false}
'         x,y,w,h는 화면 픽셀(모니터 배율 그대로). 칸이 틀 고정(나눈 창)의 어느 칸에 보이는지 찾아 그 칸(Pane)으로 바꾼다.
' 의존: CoHunsuJson. 읽기 전용(선택·스크롤을 바꾸지 않음). 스크롤에는 엑셀 이벤트가 없어 앱이 엑셀이 앞에 있을 때만 느리게 다시 묻는다(settings.markPollMs).
'       자리 계산은 Pane.PointsToScreenPixelsX/Y(시트 좌표 포인트 → 화면 픽셀). 확대·배율이 다른 PC에서 어긋나는지는 PC에서 눈으로 확인해야 한다(docs/08).
'==============================================================
Option Explicit

Private Const MARK_LIMIT As Long = 200
Private Const LEVEL2_PER_PARENT As Long = 6

Private mN As Long

Public Function Query_Mark(ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim w As Window, ws As Worksheet, vis As Range, c As Range, found As Range, part As Range, d As Range, dPre As Range, dDep As Range
    Dim panes As String, i As Long, cut As Boolean
    Set w = ActiveWindow
    If w Is Nothing Or TypeName(ActiveSheet) <> "Worksheet" Then
        Query_Mark = CoHunsuJson_Ok("{""panes"":[],""errors"":[],""pre"":[],""dep"":[],""pre2"":[],""dep2"":[],""active"":[],""rowband"":[],""colband"":[],""truncated"":false}")
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
    Set dPre = d
    Set d = Nothing
    If Not c Is Nothing Then
        On Error Resume Next
        Set d = c.DirectDependents
        Err.Clear
        On Error GoTo Fail
    End If
    Query_Mark = Query_Mark & "," & CoHunsuJson_Pair("dep", CellsJson(w, d, vis, cut))
    Set dDep = d
    ' 두 번째 단계: 쓰는 칸이 쓰는 칸(pre2)과 쓰이는 칸을 쓰는 칸(dep2). 각 줄의 to는 이어지는 첫 단계 칸 주소(색연필 수식 흐름이 화살표를 잇는 데 쓴다)
    Query_Mark = Query_Mark & "," & CoHunsuJson_Pair("pre2", Level2Json(w, dPre, vis, cut, True))
    Query_Mark = Query_Mark & "," & CoHunsuJson_Pair("dep2", Level2Json(w, dDep, vis, cut, False))
    Query_Mark = Query_Mark & "," & CoHunsuJson_Pair("active", CellsJson(w, c, vis, cut))
    Query_Mark = Query_Mark & "," & CoHunsuJson_Pair("rowband", BandJson(w, c, True)) & "," & CoHunsuJson_Pair("colband", BandJson(w, c, False))
    Query_Mark = CoHunsuJson_Ok(Query_Mark & "," & CoHunsuJson_Pair("truncated", IIf(cut, "true", "false")) & "}")
    Exit Function
Fail:
    Query_Mark = CoHunsuJson_Fail("query_failed", "mark 오류: " & Err.Description)
End Function

' 두 번째 단계 자리 배열. parent의 보이는 덩어리마다 그 덩어리의 DirectPrecedents(byPrecedent) 또는 DirectDependents를 찾아
' {addr, x, y, w, h, to: 부모 덩어리 주소}로 낸다. 같은 시트의 보이는 곳만, 부모 덩어리마다 LEVEL2_PER_PARENT개까지.
Private Function Level2Json(ByVal w As Window, ByVal parent As Range, ByVal vis As Range, ByRef cut As Boolean, ByVal byPrecedent As Boolean) As String
    Dim buf As String, area As Range, pPart As Range, src As Range, sArea As Range, sPart As Range, p As Pane
    Dim n As Long, k As Long, key As String, rect As String
    If parent Is Nothing Then
        Level2Json = "[]"
        Exit Function
    End If
    If parent.Worksheet.Name <> vis.Worksheet.Name Then
        Level2Json = "[]"
        Exit Function
    End If
    For Each area In parent.Areas
        Set pPart = Application.Intersect(area, vis)
        If Not pPart Is Nothing Then
            key = pPart.Address(False, False)
            Set src = Nothing
            On Error Resume Next
            If byPrecedent Then
                Set src = pPart.DirectPrecedents
            Else
                Set src = pPart.DirectDependents
            End If
            Err.Clear
            On Error GoTo 0
            If Not src Is Nothing Then
                If src.Worksheet.Name = vis.Worksheet.Name Then
                    k = 0
                    For Each sArea In src.Areas
                        Set sPart = Application.Intersect(sArea, vis)
                        If Not sPart Is Nothing Then
                            If mN >= MARK_LIMIT Or k >= LEVEL2_PER_PARENT Then
                                cut = True
                                Exit For
                            End If
                            Set p = PaneFor(w, sPart)
                            If Not p Is Nothing Then
                                rect = RectJson(p, sPart, sPart.Address(False, False))
                                rect = Left$(rect, Len(rect) - 1) & "," & CoHunsuJson_Pair("to", CoHunsuJson_Str(key)) & "}"
                                If n > 0 Then buf = buf & ","
                                buf = buf & rect
                                n = n + 1
                                k = k + 1
                                mN = mN + 1
                            End If
                        End If
                    Next sArea
                End If
            End If
        End If
    Next area
    Level2Json = "[" & buf & "]"
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

' 고른 칸(영역이 하나면 그 영역, 여럿이면 활성 칸)의 행 띠 또는 열 띠. 격자마다 보이는 부분만 한 장씩.
' 통째로 고른 행(열)이면 그 방향 띠는 만들지 않는다(시트 전체가 칠해지는 것을 막음).
Private Function BandJson(ByVal w As Window, ByVal active As Range, ByVal byRow As Boolean) As String
    Dim base As Range, part As Range, i As Long, buf As String, n As Long
    On Error GoTo Fail
    If active Is Nothing Then GoTo Done
    Set base = active
    If TypeName(Selection) = "Range" Then
        If Selection.Areas.Count = 1 Then Set base = Selection
    End If
    If byRow Then
        If base.Rows.Count >= base.Worksheet.Rows.Count Then GoTo Done
    Else
        If base.Columns.Count >= base.Worksheet.Columns.Count Then GoTo Done
    End If
    For i = 1 To w.Panes.Count
        If byRow Then
            Set part = Application.Intersect(base.EntireRow, w.Panes(i).VisibleRange)
        Else
            Set part = Application.Intersect(base.EntireColumn, w.Panes(i).VisibleRange)
        End If
        If Not part Is Nothing Then
            If n > 0 Then buf = buf & ","
            buf = buf & RectJson(w.Panes(i), part, "")
            n = n + 1
        End If
    Next i
Done:
    BandJson = "[" & buf & "]"
    Exit Function
Fail:
    BandJson = "[]"
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
