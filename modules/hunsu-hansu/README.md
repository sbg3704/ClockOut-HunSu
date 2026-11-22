# hunsu-hansu (한 수)

수식을 **만드는** 함수도우미(docs/23). 풀이는 수식 흐름·색연필 흐름이 하고, 한 수는 짜는 일만 한다.

- 패널은 선언만(넣을 칸 안내 + [한 수 두기] 단추). 단추는 앱 명령 `app.hansu.open`으로 **한 수 창**(`Shell/Hansu/HansuWindow.cs`)을 연다. 독·사이드바 패널은 키보드를 받지 않는 창이라 글을 치는 찾기 칸은 따로 뜨는 창에 둔다.
- 함수 사전: 이 폴더의 `functions.ko.json`. 함수를 더하려면 덩어리 하나를 더한다(형식 docs/23 2장, 읽는 코드 `Core/Formula/FunctionCatalog`). `hint: "sameSize"`인 범위 빈칸끼리는 크기가 같아야 한다(SUMIFS 등).
- 찾기·짜기·검사는 Core `Formula/`(FunctionSearch, FormulaDraft, FormulaRef, Hangul)에 있고 화면 없이 시험한다.
- 넣기: 쓰기 명령 `write.formula.set {sheet, addr, formula}` (다리 0.0.021). 계획 단계에서 엑셀이 값을 미리 계산해 확인 창에 보이고, 넣은 뒤 `write.formula.undo`로 옛 내용을 되돌린다.
- 365·2019 전용 함수(XLOOKUP, FILTER, IFS 등)는 후보 줄에 `365`·`2019` 표시만 붙인다(엑셀 판 번호로는 2016·2019·365를 가를 수 없어 걸러 내지 않는다).
