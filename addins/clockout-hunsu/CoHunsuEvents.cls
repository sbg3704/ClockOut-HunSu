VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
END
Attribute VB_Name = "CoHunsuEvents"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = False
'==============================================================
' 기능: 엑셀 Application 이벤트를 받아 신호를 보낸다. sel(선택), change(값 변경), sheet(시트 바뀜), book(통합 문서 바뀜·열림·시트 추가), calc(재계산).
' 사용법: CoHunsu_Start가 만들어 App에 Application을 넣는다.
' 의존: CoHunsuMail. 처리는 신호 보내기뿐(5ms 이내, 셀 읽기 금지, docs/04 3장 1번).
'==============================================================
Option Explicit

Public WithEvents App As Application

Private Sub App_SheetSelectionChange(ByVal Sh As Object, ByVal Target As Range)
    CoHunsuMail_Send 1
End Sub

Private Sub App_SheetChange(ByVal Sh As Object, ByVal Target As Range)
    CoHunsuMail_Send 2
End Sub

Private Sub App_SheetActivate(ByVal Sh As Object)
    CoHunsuMail_Send 3
End Sub

Private Sub App_WorkbookActivate(ByVal Wb As Workbook)
    CoHunsuMail_Send 4
End Sub

Private Sub App_WorkbookOpen(ByVal Wb As Workbook)
    CoHunsuMail_Send 4
End Sub

Private Sub App_NewWorkbook(ByVal Wb As Workbook)
    CoHunsuMail_Send 4
End Sub

Private Sub App_WorkbookNewSheet(ByVal Wb As Workbook, ByVal Sh As Object)
    CoHunsuMail_Send 4
End Sub

Private Sub App_SheetCalculate(ByVal Sh As Object)
    CoHunsuMail_Send 5
End Sub
