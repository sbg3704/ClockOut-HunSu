# hunsu-history (발자취)

선언만 있는 모듈(코드 없음). 방금 다녀간 칸·시트 목록과 뒤로·앞으로 단추. 기록은 앱이 가진다(`app.history` 주제, `Core/Nav/NavHistory`, `Shell/ShellController.History.cs`). 다리에는 기존 `selection.summary`와 `goto`만 쓴다. 처음에는 꺼져 있다(모듈 관리자에서 켜거나 `settings.json`의 `enabledByDefault`).

- 1초 안에 잇달아 바뀐 선택(끌기, 빠른 화살표)은 한 줄로 합친다.
- 뒤로 가서 생긴 선택은 다시 쌓지 않는다. 뒤로 간 뒤 새로 고르면 앞쪽 기록은 버린다(브라우저와 같음).
- 다른 통합 문서의 기록은 숨긴다(이동 명령이 지금 통합 문서 안에서만 간다).
