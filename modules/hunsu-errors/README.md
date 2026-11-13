# 오류 셀 (hunsu-errors)

지금 시트의 오류 셀을 모아 보여 준다. 칸 이름을 누르면 그 칸으로 이동한다(명령 `goto`).

- 데이터: 조회 `errors`(다리 `CoHunsuQ_Errors.bas`). 값 변경·재계산·시트 바뀜·책 바뀜 신호에서 다시 조회한다.
- 한 번에 최대 100개까지 담고(`ERR_LIMIT`), 화면은 `view.json`의 `max`(8)줄만 그린다. 더 보려면 `max`를 키운다(전체 상한은 `settings.json`의 `listMaxRows`).
- 처음에는 꺼져 있다(`settings.json`의 `enabledByDefault`에 `hunsu-errors`를 더하거나 모듈 관리자에서 켠다).
