Attribute VB_Name = "CoHunsuQ_ColDiff"
'==============================================================
' 기능: 조회 coldiff. 지금 선택이 나란한 두 열이면 줄마다 두 칸을 견주어 다른 줄을 돌려준다(목록 + goto로 이동).
'       칼퇴 추가기능 value-tools의 "두 열 다른 칸 선택"(ValueCompare_SelectDiff)을 선택을 바꾸지 않는 목록으로 옮긴 조회다(2026-11-16).
' 사용법: CoHunsu_Query "coldiff", "{}"
'         -> {"ready":true,"sheet":"매출","rows":200,"diffCount":3,"truncated":false,"items":[{"addr":"A5","label":"5행","a":"가","b":"나"}]}
'         선택이 한 영역의 두 열이 아니면 {"ready":false,"hint":"..."}(오류가 아님).
' 의존: CoHunsuJson. 읽기 전용. 값 비교는 글자는 대소문자·공백까지 같아야 같은 것으로 보고, 숫자는 값이 같아야 같다. 빈 칸끼리는 같다.
'       줄 수는 ROW_LIMIT까지만 보고 넘으면 truncated:true. 돌려주는 줄은 ITEM_LIMIT개까지이고 칸 값은 TEXT_LIMIT 글자로 자른다.
'==============================================================
Option Explicit

Private Const ROW_LIMIT As Long = 50000
Private Const ITEM_LIMIT As Long = 30
Private Const TEXT_LIMIT As Long = 24

Public Function Query_ColDiff(ByVal argsJson As String) As String
    On Error GoTo Fail
    Dim sel As Range, a As Variant, nRows As Long, i As Long, diffs As Double, n As Long, buf As String, cut As Boolean
    Dim r0 As Long, c0 As Long
    If TypeName(Selection) <> "Range" Then
        Query_ColDiff = NotReady("셀 범위를 고르세요")
        Exit Function
    End If
    Set sel = Selection
    If sel.Areas.Count <> 1 Then
        Query_ColDiff = NotReady("영역 하나로 두 열을 고르세요")
        Exit Function
    End If
    If sel.Columns.Count <> 2 Then
        Query_ColDiff = NotReady("나란한 두 열을 고르세요")
        Exit Function
    End If
    nRows = sel.Rows.Count
    If nRows > ROW_LIMIT Then
        nRows = ROW_LIMIT
        cut = True
    End If
    r0 = sel.Row: c0 = sel.Column
    a = sel.Worksheet.Range(sel.Worksheet.Cells(r0, c0), sel.Worksheet.Cells(r0 + nRows - 1, c0 + 1)).Value2
    For i = 1 To nRows
        If Not Same(a(i, 1), a(i, 2)) Then
            diffs = diffs + 1
            If n < ITEM_LIMIT Then
                If n > 0 Then buf = buf & ","
                buf = buf & "{" & CoHunsuJson_Pair("addr", CoHunsuJson_Str(sel.Worksheet.Cells(r0 + i - 1, c0).Address(False, False))) & "," & _
                    CoHunsuJson_Pair("label", CoHunsuJson_Str(CStr(r0 + i - 1) & "행")) & "," & _
                    CoHunsuJson_Pair("a", CoHunsuJson_Str(Show(a(i, 1)))) & "," & CoHunsuJson_Pair("b", CoHunsuJson_Str(Show(a(i, 2)))) & "}"
                n = n + 1
            End If
        End If
    Next i
    Query_ColDiff = CoHunsuJson_Ok("{" & CoHunsuJson_Pair("ready", "true") & "," & _
        CoHunsuJson_Pair("sheet", CoHunsuJson_Str(sel.Worksheet.Name)) & "," & _
        CoHunsuJson_Pair("rows", CStr(nRows)) & "," & CoHunsuJson_Pair("diffCount", CoHunsuJson_Num(diffs)) & "," & _
        CoHunsuJson_Pair("truncated", IIf(cut, "true", "false")) & "," & _
        CoHunsuJson_Pair("items", "[" & buf & "]") & "}")
    Exit Function
Fail:
    Query_ColDiff = CoHunsuJson_Fail("query_failed", "coldiff 오류: " & Err.Description)
End Function

Private Function NotReady(ByVal hint As String) As String
    NotReady = CoHunsuJson_Ok("{" & CoHunsuJson_Pair("ready", "false") & "," & CoHunsuJson_Pair("hint", CoHunsuJson_Str(hint)) & "}")
End Function

' 두 칸 값이 같은가. 오류 값은 같은 오류끼리만 같다. 숫자와 글자는 다르다.
Private Function Same(ByVal x As Variant, ByVal y As Variant) As Boolean
    If IsError(x) Or IsError(y) Then
        If IsError(x) And IsError(y) Then Same = (CStr(x) = CStr(y))
        Exit Function
    End If
    If IsBlank(x) And IsBlank(y) Then
        Same = True
    ElseIf VarType(x) = vbString And VarType(y) = vbString Then
        Same = (StrComp(x, y, vbBinaryCompare) = 0)
    ElseIf VarType(x) = vbString Or VarType(y) = vbString Then
        Same = False
    ElseIf IsBlank(x) Or IsBlank(y) Then
        Same = False
    Else
        Same = (CDbl(x) = CDbl(y))
    End If
End Function

' 빈 칸 또는 길이 0인 글자.
Private Function IsBlank(ByVal v As Variant) As Boolean
    If IsEmpty(v) Then
        IsBlank = True
    ElseIf VarType(v) = vbString Then
        IsBlank = (Len(v) = 0)
    End If
End Function

Private Function Show(ByVal v As Variant) As String
    Dim t As String
    If IsError(v) Then
        t = "#오류"
    ElseIf IsEmpty(v) Then
        t = "(빈 칸)"
    Else
        t = CStr(v)
    End If
    If Len(t) > TEXT_LIMIT Then t = Left$(t, TEXT_LIMIT) & "…"
    Show = t
End Function
