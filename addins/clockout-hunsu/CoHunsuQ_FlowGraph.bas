Attribute VB_Name = "CoHunsuQ_FlowGraph"
'==============================================================
' 기능: 조회 flow.graph. 활성 셀의 수식 흐름을 시트를 넘어 몇 단계까지 따라간다.
'       앞쪽(이 칸이 쓰는 칸): 같은 시트는 DirectPrecedents, 다른 시트는 수식 글을 읽어 "시트!주소" 참조를 찾는다.
'       뒤쪽(이 칸을 쓰는 칸): 같은 시트는 DirectDependents, 다른 시트는 다른 시트의 수식 중 이 시트 이름이 든 것을 한 번 훑어(캐시) 겹치는지 본다.
' 사용법: CoHunsu_Query "flow.graph", "{""depth"":""2""}"
'         -> {"sheet":"요약","addr":"B3","formula":"=매출!C9*2","hasFormula":true,"depth":2,
'             "pre":{"count":3,"items":[{"sheet":"매출","addr":"C9","label":"매출!C9","value":"10","level":1,"cross":true,"indent":"","formula":"=SUM(C2:C8)"}],"truncated":false},
'             "dep":{...같은 모양...},"external":0,"scanned":1200,"scanTruncated":false,"cached":true}
'         depth는 1~3(기본 2). 단계마다 칸 하나짜리만 더 따라가고 여러 칸 영역은 주소만 보인다.
' 의존: CoHunsuJson. 읽기 전용(선택·활성 시트를 바꾸지 않음, 화살표를 그리지 않음).
'       한계: 다른 통합 문서 참조([책]시트!A1)는 세기만 하고(external) 따라가지 않는다. 3D 참조(시트1:시트3!A1), 이름(정의된 이름) 안의 참조, INDIRECT·OFFSET는 못 따라간다.
'       뒤쪽 훑기는 SCAN_LIMIT칸까지(넘으면 scanTruncated:true). 훑은 결과는 값이 바뀌거나 시트가 생길 때(CoHunsuFlowGraph_Dirty), 또는 CACHE_SEC초 뒤에 버린다.
'==============================================================
Option Explicit

Private Const NODE_LIMIT As Long = 40
Private Const SCAN_LIMIT As Double = 20000#
Private Const CAND_LIMIT As Long = 3000
Private Const CACHE_SEC As Double = 30#
Private Const FORMULA_MAX As Long = 120

' 뒤쪽 훑기 캐시: 다른 시트에서 "!"가 든 수식 칸과 그 칸이 가리키는 "시트<Tab>주소" 목록
Private mCandCells As Collection
Private mCandRefs As Collection
Private mCacheBook As String
Private mCacheAt As Double
Private mScanned As Double
Private mScanTruncated As Boolean
Private mExternal As Long

Private mSeen As Object ' Scripting.Dictionary: 이미 담은 칸(시트!주소)

' 값이 바뀌거나 시트가 생기면 이벤트가 부른다(CoHunsuEvents). 다음 조회 때 다시 훑는다.
Public Sub CoHunsuFlowGraph_Dirty()
    Set mCandCells = Nothing
    Set mCandRefs = Nothing
End Sub

Public Function Query_FlowGraph(ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim c As Range, ws As Worksheet, depth As Long, f As String, cached As Boolean
    Dim preBuf As String, depBuf As String, preN As Long, depN As Long, preCut As Boolean, depCut As Boolean
    On Error Resume Next
    Set c = ActiveCell
    On Error GoTo Fail
    If c Is Nothing Then
        Query_FlowGraph = CoHunsuJson_Fail("range_bad", "활성 셀이 없음")
        Exit Function
    End If
    depth = Val(CoHunsuJson_GetStr(argsJson, "depth"))
    If depth < 1 Or depth > 3 Then depth = 2
    Set ws = c.Worksheet
    If c.HasFormula Then f = Left$(CStr(c.Formula), FORMULA_MAX)
    mExternal = 0
    Set mSeen = CreateObject("Scripting.Dictionary")
    mSeen.Add Key(c), True
    Walk c, 1, depth, True, preBuf, preN, preCut
    cached = EnsureScan(ws.Parent)
    Walk c, 1, depth, False, depBuf, depN, depCut
    Query_FlowGraph = CoHunsuJson_Ok("{" & CoHunsuJson_Pair("sheet", CoHunsuJson_Str(ws.Name)) & "," & _
        CoHunsuJson_Pair("addr", CoHunsuJson_Str(c.Address(False, False))) & "," & _
        CoHunsuJson_Pair("formula", CoHunsuJson_Str(f)) & "," & _
        CoHunsuJson_Pair("hasFormula", IIf(c.HasFormula, "true", "false")) & "," & _
        CoHunsuJson_Pair("depth", CStr(depth)) & "," & _
        CoHunsuJson_Pair("pre", ListJson(preBuf, preN, preCut)) & "," & _
        CoHunsuJson_Pair("dep", ListJson(depBuf, depN, depCut)) & "," & _
        CoHunsuJson_Pair("external", CStr(mExternal)) & "," & _
        CoHunsuJson_Pair("scanned", CoHunsuJson_Num(mScanned)) & "," & _
        CoHunsuJson_Pair("scanTruncated", IIf(mScanTruncated, "true", "false")) & "," & _
        CoHunsuJson_Pair("cached", IIf(cached, "true", "false")) & "}")
    Set mSeen = Nothing
    Exit Function
Fail:
    Set mSeen = Nothing
    Query_FlowGraph = CoHunsuJson_Fail("query_failed", "flow.graph 오류: " & Err.Description)
End Function

Private Function ListJson(ByVal buf As String, ByVal n As Long, ByVal cut As Boolean) As String
    ListJson = "{" & CoHunsuJson_Pair("count", CStr(n)) & "," & CoHunsuJson_Pair("items", "[" & buf & "]") & "," & _
        CoHunsuJson_Pair("truncated", IIf(cut, "true", "false")) & "}"
End Function

' 한 단계씩 깊이 우선으로 따라가며 줄을 쌓는다(화면에서 들여쓰기로 나무처럼 보임).
Private Sub Walk(ByVal c As Range, ByVal level As Long, ByVal depth As Long, ByVal pre As Boolean, ByRef buf As String, ByRef n As Long, ByRef cut As Boolean)
    Dim nexts As Collection, r As Variant
    If pre Then Set nexts = PrecedentsOf(c) Else Set nexts = DependentsOf(c)
    For Each r In nexts
        If n >= NODE_LIMIT Then
            cut = True
            Exit Sub
        End If
        If Not mSeen.Exists(Key(r)) Then
            mSeen.Add Key(r), True
            If n > 0 Then buf = buf & ","
            buf = buf & NodeJson(r, c, level)
            n = n + 1
            If level < depth And r.CountLarge = 1 Then
                If r.HasFormula Or Not pre Then Walk r, level + 1, depth, pre, buf, n, cut
            End If
        End If
    Next r
End Sub

Private Function NodeJson(ByVal r As Range, ByVal from As Range, ByVal level As Long) As String
    Dim sh As String, a As String, v As String, f As String, cross As Boolean
    sh = r.Worksheet.Name
    a = r.Address(False, False)
    cross = (sh <> from.Worksheet.Name)
    On Error Resume Next
    If r.CountLarge = 1 Then
        v = Left$(CStr(r.Text), 24)
        If r.HasFormula Then f = Left$(CStr(r.Formula), 60)
    End If
    On Error GoTo 0
    NodeJson = "{" & CoHunsuJson_Pair("sheet", CoHunsuJson_Str(sh)) & "," & CoHunsuJson_Pair("addr", CoHunsuJson_Str(a)) & "," & _
        CoHunsuJson_Pair("label", CoHunsuJson_Str(IIf(sh = ActiveSheet.Name, a, sh & "!" & a))) & "," & _
        CoHunsuJson_Pair("value", CoHunsuJson_Str(v)) & "," & CoHunsuJson_Pair("formula", CoHunsuJson_Str(f)) & "," & _
        CoHunsuJson_Pair("level", CStr(level)) & "," & CoHunsuJson_Pair("cross", IIf(cross, "true", "false")) & "," & _
        CoHunsuJson_Pair("indent", CoHunsuJson_Str(String$((level - 1) * 3, " ") & IIf(level > 1, "└ ", ""))) & "}"
End Function

Private Function Key(ByVal r As Range) As String
    Key = r.Worksheet.Name & "!" & r.Address(False, False)
End Function

' 앞쪽: 같은 시트(DirectPrecedents 영역) + 수식 글에 적힌 다른 시트 참조.
Private Function PrecedentsOf(ByVal c As Range) As Collection
    Dim res As New Collection, d As Range, area As Range, refs As Collection, item As Variant, r As Range
    Set PrecedentsOf = res
    If c.CountLarge <> 1 Then Exit Function
    If Not c.HasFormula Then Exit Function
    On Error Resume Next
    Set d = c.DirectPrecedents
    Err.Clear
    On Error GoTo 0
    If Not d Is Nothing Then
        For Each area In d.Areas
            res.Add area
        Next area
    End If
    Set refs = QualifiedRefs(CStr(c.Formula))
    For Each item In refs
        Set r = Resolve(c.Worksheet.Parent, CStr(item))
        If Not r Is Nothing Then
            If r.Worksheet.Name <> c.Worksheet.Name Then res.Add r
        End If
    Next item
End Function

' 뒤쪽: 같은 시트(DirectDependents 영역) + 다른 시트 수식 중 이 칸과 겹치는 참조가 있는 칸.
Private Function DependentsOf(ByVal c As Range) As Collection
    Dim res As New Collection, d As Range, area As Range, i As Long, refs As Collection, item As Variant, r As Range, sh As String
    Set DependentsOf = res
    On Error Resume Next
    Set d = c.DirectDependents
    Err.Clear
    On Error GoTo 0
    If Not d Is Nothing Then
        For Each area In d.Areas
            res.Add area
        Next area
    End If
    If mCandCells Is Nothing Then Exit Function
    sh = c.Worksheet.Name
    For i = 1 To mCandCells.Count
        Set refs = mCandRefs(i)
        For Each item In refs
            If Left$(CStr(item), Len(sh) + 1) = sh & vbTab Then
                Set r = Resolve(c.Worksheet.Parent, CStr(item))
                If Not r Is Nothing Then
                    If Not Application.Intersect(r, c) Is Nothing Then
                        res.Add mCandCells(i)
                        Exit For
                    End If
                End If
            End If
        Next item
    Next i
End Function

' "시트<Tab>주소"를 칸으로. 못 찾으면 Nothing.
Private Function Resolve(ByVal wb As Workbook, ByVal item As String) As Range
    Dim p As Long
    p = InStr(1, item, vbTab, vbBinaryCompare)
    If p = 0 Then Exit Function
    On Error Resume Next
    Set Resolve = wb.Worksheets(Left$(item, p - 1)).Range(Mid$(item, p + 1))
    Err.Clear
End Function

' 다른 시트 수식 훑기. 캐시가 살아 있으면 다시 훑지 않는다(true = 캐시 씀).
Private Function EnsureScan(ByVal wb As Workbook) As Boolean
    Dim ws As Worksheet, found As Range, area As Range, arr As Variant, i As Long, j As Long, keepExternal As Long
    If Not mCandCells Is Nothing And mCacheBook = wb.FullName And Abs(Timer - mCacheAt) < CACHE_SEC Then
        EnsureScan = True
        Exit Function
    End If
    Set mCandCells = New Collection
    Set mCandRefs = New Collection
    mCacheBook = wb.FullName
    mCacheAt = Timer
    mScanned = 0
    mScanTruncated = False
    keepExternal = mExternal ' 훑는 동안 만난 다른 책 참조는 이 칸의 수가 아니다
    For Each ws In wb.Worksheets
        Set found = Nothing
        On Error Resume Next
        Set found = ws.UsedRange.SpecialCells(xlCellTypeFormulas)
        Err.Clear
        On Error GoTo 0
        If Not found Is Nothing Then
            For Each area In found.Areas
                If mScanned + area.CountLarge > SCAN_LIMIT Or mCandCells.Count >= CAND_LIMIT Then
                    mScanTruncated = True
                    mExternal = keepExternal
                    Exit Function
                End If
                mScanned = mScanned + area.CountLarge
                If area.CountLarge = 1 Then
                    Consider area, CStr(area.Formula)
                Else
                    arr = area.Formula
                    For i = 1 To UBound(arr, 1)
                        For j = 1 To UBound(arr, 2)
                            If InStr(1, CStr(arr(i, j)), "!", vbBinaryCompare) > 0 Then Consider area.Cells(i, j), CStr(arr(i, j))
                        Next j
                    Next i
                End If
            Next area
        End If
    Next ws
    mExternal = keepExternal
End Function

Private Sub Consider(ByVal cell As Range, ByVal f As String)
    Dim refs As Collection
    If InStr(1, f, "!", vbBinaryCompare) = 0 Then Exit Sub
    Set refs = QualifiedRefs(f)
    If refs.Count = 0 Then Exit Sub
    mCandCells.Add cell
    mCandRefs.Add refs
End Sub

' 수식 글에서 시트 이름이 붙은 참조("시트<Tab>주소")를 모은다. 글자 상수("...")는 건너뛴다. 다른 책·3D 참조는 mExternal만 센다.
Private Function QualifiedRefs(ByVal f As String) As Collection
    Dim res As New Collection, i As Long, j As Long, n As Long, ch As String, sh As String, nextI As Long
    n = Len(f)
    i = 1
    Do While i <= n
        ch = Mid$(f, i, 1)
        If ch = """" Then
            i = i + 1
            Do While i <= n
                If Mid$(f, i, 1) = """" Then
                    If i < n And Mid$(f, i + 1, 1) = """" Then
                        i = i + 2
                    Else
                        Exit Do
                    End If
                Else
                    i = i + 1
                End If
            Loop
            i = i + 1
        ElseIf ch = "'" Then
            sh = ""
            j = i + 1
            Do While j <= n
                If Mid$(f, j, 1) = "'" Then
                    If j < n And Mid$(f, j + 1, 1) = "'" Then
                        sh = sh & "'"
                        j = j + 2
                    Else
                        Exit Do
                    End If
                Else
                    sh = sh & Mid$(f, j, 1)
                    j = j + 1
                End If
            Loop
            If j < n And Mid$(f, j + 1, 1) = "!" Then
                AddRef res, sh, ReadRef(f, j + 2, nextI)
                i = nextI
            Else
                i = j + 1
            End If
        ElseIf ch = "!" Then
            j = i - 1
            Do While j >= 1
                If IsNameChar(Mid$(f, j, 1)) Then j = j - 1 Else Exit Do
            Loop
            sh = Mid$(f, j + 1, i - j - 1)
            If j >= 1 Then
                If Mid$(f, j, 1) = ":" Then sh = "[3d]" ' 시트1:시트3!A1
            End If
            AddRef res, sh, ReadRef(f, i + 1, nextI)
            i = nextI
        Else
            i = i + 1
        End If
    Loop
    Set QualifiedRefs = res
End Function

Private Sub AddRef(ByVal res As Collection, ByVal sh As String, ByVal ref As String)
    If Len(sh) = 0 Or Len(ref) = 0 Then Exit Sub
    If InStr(1, sh, "]", vbBinaryCompare) > 0 Then
        mExternal = mExternal + 1
        Exit Sub
    End If
    res.Add sh & vbTab & ref
End Sub

' 시트 이름(따옴표 없는 것)에 올 수 있는 글자: 영문·숫자·밑줄·점과 한글 등 ASCII 밖 글자. 다른 책 표시 [ ]도 함께 읽어 거른다.
Private Function IsNameChar(ByVal ch As String) As Boolean
    Dim code As Long
    code = AscW(ch) And &HFFFF&
    IsNameChar = (ch Like "[A-Za-z0-9_.]") Or ch = "[" Or ch = "]" Or code > 127
End Function

' "!" 뒤 주소(예: $A$1, B2:C9, A:A, 3:3)를 읽는다. nextI에 다음 글자 자리를 둔다.
Private Function ReadRef(ByVal f As String, ByVal start As Long, ByRef nextI As Long) As String
    Dim k As Long, ch As String
    k = start
    Do While k <= Len(f)
        ch = Mid$(f, k, 1)
        If ch Like "[A-Za-z0-9$:]" Then k = k + 1 Else Exit Do
    Loop
    ReadRef = Mid$(f, start, k - start)
    nextI = k
    If nextI <= start Then nextI = start + 1
End Function
