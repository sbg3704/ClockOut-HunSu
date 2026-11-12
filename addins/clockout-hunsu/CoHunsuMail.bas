Attribute VB_Name = "CoHunsuMail"
'==============================================================
' 기능: 신호 보내기. 틀의 숨은 창(클래스 ClockOutHunsuMsg)에 등록 메시지 ClockOut.Hunsu.Signal을 PostMessage로 보낸다(docs/02 4장 1번).
'       기다리지 않으므로 엑셀이 느려지지 않는다. 틀이 없으면 아무 일도 하지 않는다.
' 사용법: CoHunsuMail_Send 1   ' 1 = sel
'         wParam = 이 엑셀 주 창 핸들, lParam = 신호 번호 + 순번(8비트) * 256
' 의존: user32, kernel32(시간 재기). 처리 시간을 재어 bridge.stats 조회로 알려 준다(docs/04 1장 "다리 시간 5ms").
'==============================================================
Option Explicit

#If VBA7 Then
Private Declare PtrSafe Function FindWindowW Lib "user32" (ByVal lpClassName As LongPtr, ByVal lpWindowName As LongPtr) As LongPtr
Private Declare PtrSafe Function IsWindow Lib "user32" (ByVal hWnd As LongPtr) As Long
Private Declare PtrSafe Function PostMessageW Lib "user32" (ByVal hWnd As LongPtr, ByVal wMsg As Long, ByVal wParam As LongPtr, ByVal lParam As LongPtr) As Long
Private Declare PtrSafe Function RegisterWindowMessageW Lib "user32" (ByVal lpString As LongPtr) As Long
Private Declare PtrSafe Function QueryPerformanceCounter Lib "kernel32" (ByRef lpCount As Currency) As Long
Private Declare PtrSafe Function QueryPerformanceFrequency Lib "kernel32" (ByRef lpFreq As Currency) As Long
Private mTarget As LongPtr
#Else
Private Declare Function FindWindowW Lib "user32" (ByVal lpClassName As Long, ByVal lpWindowName As Long) As Long
Private Declare Function IsWindow Lib "user32" (ByVal hWnd As Long) As Long
Private Declare Function PostMessageW Lib "user32" (ByVal hWnd As Long, ByVal wMsg As Long, ByVal wParam As Long, ByVal lParam As Long) As Long
Private Declare Function RegisterWindowMessageW Lib "user32" (ByVal lpString As Long) As Long
Private Declare Function QueryPerformanceCounter Lib "kernel32" (ByRef lpCount As Currency) As Long
Private Declare Function QueryPerformanceFrequency Lib "kernel32" (ByRef lpFreq As Currency) As Long
Private mTarget As Long
#End If

Private Const SAMPLE_MAX As Long = 256
Private mMsg As Long
Private mSeq As Long
Private mSent As Long
Private mEvents As Long
Private mSamples(0 To SAMPLE_MAX - 1) As Double
Private mFreq As Currency

Public Sub CoHunsuMail_Send(ByVal code As Long)
    Dim t0 As Currency, t1 As Currency
    On Error GoTo Done
    QueryPerformanceCounter t0
    If mMsg = 0 Then mMsg = RegisterWindowMessageW(StrPtr("ClockOut.Hunsu.Signal"))
    If mTarget = 0 Then
        mTarget = FindWindowW(StrPtr("ClockOutHunsuMsg"), 0)
    ElseIf IsWindow(mTarget) = 0 Then
        mTarget = FindWindowW(StrPtr("ClockOutHunsuMsg"), 0)
    End If
    If mTarget <> 0 Then
        mSeq = (mSeq + 1) And &HFF&
        If PostMessageW(mTarget, mMsg, Application.Hwnd, (code And &HFF&) Or (mSeq * 256&)) <> 0 Then mSent = mSent + 1
    End If
Done:
    On Error Resume Next
    QueryPerformanceCounter t1
    If mFreq = 0 Then QueryPerformanceFrequency mFreq
    If mFreq <> 0 Then mSamples(mEvents Mod SAMPLE_MAX) = (t1 - t0) * 1000000# / mFreq
    mEvents = mEvents + 1
End Sub

' 처리 시간 기록(마이크로초). 0단계 측정용 진단 조회 bridge.stats가 쓴다.
Public Function CoHunsuMail_StatsJson() As String
    Dim n As Long, i As Long, buf As String
    n = mEvents
    If n > SAMPLE_MAX Then n = SAMPLE_MAX
    buf = ""
    For i = 0 To n - 1
        If i > 0 Then buf = buf & ","
        buf = buf & CoHunsuJson_Num(Round(mSamples(i), 1))
    Next i
    CoHunsuMail_StatsJson = "{" & CoHunsuJson_Pair("events", CStr(mEvents)) & "," & _
        CoHunsuJson_Pair("sent", CStr(mSent)) & "," & _
        CoHunsuJson_Pair("target", IIf(mTarget <> 0, "true", "false")) & "," & _
        CoHunsuJson_Pair("samplesUs", "[" & buf & "]") & "}"
End Function

Public Sub CoHunsuMail_ResetStats()
    mEvents = 0
    mSent = 0
End Sub
