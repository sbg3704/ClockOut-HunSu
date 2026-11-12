Attribute VB_Name = "CoHunsuEntry"
'==============================================================
' 기능: 다리 시작과 끝. Application 이벤트 연결(CoHunsuEvents)을 켜고 끈다. 틀 창이 없어도 정상 동작한다.
' 사용법: ThisWorkbook의 Workbook_Open이 OnTime으로 CoHunsu_Start를 부른다. 틀도 hello 때 부른다(두 번 불러도 한 번만 켜짐).
' 의존: CoHunsuEvents
'==============================================================
Option Explicit

Private mEvents As CoHunsuEvents

Public Sub CoHunsu_Start()
    On Error Resume Next
    If mEvents Is Nothing Then
        Set mEvents = New CoHunsuEvents
        Set mEvents.App = Application
    End If
End Sub

Public Sub CoHunsu_Stop()
    On Error Resume Next
    If Not mEvents Is Nothing Then
        Set mEvents.App = Nothing
        Set mEvents = Nothing
    End If
End Sub
