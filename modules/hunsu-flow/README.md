# 수식 흐름 (hunsu-flow)

활성 칸의 앞쪽(쓰는 칸)과 뒤쪽(쓰이는 칸)을 보여 준다. 칸 이름을 누르면 이동한다(명령 `goto`).

- 데이터: 조회 `flow.neighbors`(다리 `CoHunsuQ_Flow.bas`). 엑셀 `DirectPrecedents`/`DirectDependents`를 쓰므로 **같은 시트 안의 칸만** 나온다. 수식에 `!`가 있으면 "다른 시트도 사용" 표시만 한다(전체 순서도는 다음 단계, docs/07).
- 선택이 바뀔 때마다 조회하므로 처음에는 꺼져 있다. 켜 본 뒤 엑셀이 느려지면 `settings.json`의 `queryDragGapMs`를 키운다.
