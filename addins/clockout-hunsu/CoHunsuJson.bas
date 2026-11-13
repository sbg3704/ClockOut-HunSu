Attribute VB_Name = "CoHunsuJson"
'==============================================================
' 기능: 다리 응답 JSON 조각 만들기(성공/실패 틀, 글자 이스케이프, 숫자).
' 사용법: CoHunsuJson_Ok("{...}"), CoHunsuJson_Fail("code", "문장"), CoHunsuJson_Str(s), CoHunsuJson_Num(d)
' 의존: 없음.
' 참고: docs/02 2장은 clockout-vendor-data를 부르라고 했으나 0단계 엑셀에는 그 추가기능이 없어 최소한만 둔다.
'       1단계에서 공용 라이브러리 쪽 JSON으로 바꾼다(HANDOFF 남은 일).
'==============================================================
Option Explicit

Public Function CoHunsuJson_Ok(ByVal dataJson As String) As String
    CoHunsuJson_Ok = "{""ok"":true,""proto"":" & CStr(HUNSU_PROTO) & ",""data"":" & dataJson & "}"
End Function

Public Function CoHunsuJson_Fail(ByVal code As String, ByVal message As String) As String
    CoHunsuJson_Fail = "{""ok"":false,""proto"":" & CStr(HUNSU_PROTO) & ",""code"":" & CoHunsuJson_Str(code) & _
        ",""error"":" & CoHunsuJson_Str(message) & "}"
End Function

Public Function CoHunsuJson_Pair(ByVal key As String, ByVal valueJson As String) As String
    CoHunsuJson_Pair = """" & key & """:" & valueJson
End Function

Public Function CoHunsuJson_Str(ByVal s As String) As String
    Dim i As Long, ch As String, code As Long, buf As String
    buf = ""
    For i = 1 To Len(s)
        ch = Mid$(s, i, 1)
        code = AscW(ch) And &HFFFF&
        Select Case code
            Case 34: buf = buf & "\"""
            Case 92: buf = buf & "\\"
            Case 8: buf = buf & "\b"
            Case 9: buf = buf & "\t"
            Case 10: buf = buf & "\n"
            Case 12: buf = buf & "\f"
            Case 13: buf = buf & "\r"
            Case Is < 32: buf = buf & "\u" & Right$("000" & Hex$(code), 4)
            Case Else: buf = buf & ch
        End Select
    Next i
    CoHunsuJson_Str = """" & buf & """"
End Function

' 지역 설정과 관계없이 소수점은 "."(Str은 늘 점을 쓴다). 앞자리 0을 붙인다.
Public Function CoHunsuJson_Num(ByVal d As Double) As String
    Dim t As String
    t = Trim$(Str$(d))
    If Left$(t, 1) = "." Then t = "0" & t
    If Left$(t, 2) = "-." Then t = "-0" & Mid$(t, 2)
    CoHunsuJson_Num = t
End Function

' 인자 JSON({"name":"값"})에서 글자 값 하나를 꺼낸다. 없으면 빈 글자. \", \\, \n, \t, \uXXXX만 풀고 중첩 객체는 다루지 않는다(명령 인자는 평평한 글자 객체).
Public Function CoHunsuJson_GetStr(ByVal json As String, ByVal key As String) As String
    Dim p As Long, i As Long, ch As String, buf As String
    p = InStr(1, json, """" & key & """", vbBinaryCompare)
    If p = 0 Then Exit Function
    p = InStr(p + Len(key) + 2, json, ":", vbBinaryCompare)
    If p = 0 Then Exit Function
    p = InStr(p, json, """", vbBinaryCompare)
    If p = 0 Then Exit Function
    i = p + 1
    Do While i <= Len(json)
        ch = Mid$(json, i, 1)
        If ch = "\" Then
            i = i + 1
            If i > Len(json) Then Exit Do
            ch = Mid$(json, i, 1)
            Select Case ch
                Case "n"
                    buf = buf & vbLf
                Case "t"
                    buf = buf & vbTab
                Case "u"
                    If i + 4 <= Len(json) Then
                        buf = buf & ChrW$(CLng("&H" & Mid$(json, i + 1, 4)))
                        i = i + 4
                    End If
                Case Else
                    buf = buf & ch
            End Select
        ElseIf ch = """" Then
            Exit Do
        Else
            buf = buf & ch
        End If
        i = i + 1
    Loop
    CoHunsuJson_GetStr = buf
End Function
