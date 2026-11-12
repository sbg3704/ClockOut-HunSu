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
' 기능: 엑셀 Application 이벤트를 받아 신호를 보낸다. 0단계 최소판은 선택 바뀜(sel)만.
' 사용법: CoHunsu_Start가 만들어 App에 Application을 넣는다.
' 의존: CoHunsuMail. 처리는 신호 보내기뿐(5ms 이내, 셀 읽기 금지, docs/04 3장 1번).
'==============================================================
Option Explicit

Public WithEvents App As Application

Private Sub App_SheetSelectionChange(ByVal Sh As Object, ByVal Target As Range)
    CoHunsuMail_Send 1
End Sub
