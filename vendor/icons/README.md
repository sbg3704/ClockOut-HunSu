# 아이콘 세트 (통째로 받아 둔 원본)

세트마다 폴더 하나: SVG 전체를 담은 압축 파일과 원본 `LICENSE`. 출처·판·라이선스 표는 `vendor/SOURCES.md` "아이콘 세트" 장.

- `profiles.json`: 뜻 이름(모듈 선언의 `"icon"`) → 세트별 그림 이름. 여섯 세트를 모두 채운다.
- 앱이 쓰는 그림 데이터 만들기: 저장소 맨 위에서 `python tools/icons/make_icon_data.py` → `src/ClockOut.Hunsu.Ui/Icons/IconData.g.cs`
- 압축 파일 안 그림을 고르려면 압축을 풀어 이름을 찾는다(예: Fluent는 `이름_24_regular.svg`).
