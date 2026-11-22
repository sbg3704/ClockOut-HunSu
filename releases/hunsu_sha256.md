# 훈수꾼 묶음 SHA-256 장부

칼퇴 레일로 내보낸 `hunsu.zip`의 판마다 SHA-256 하나(2026-11-22 사용자 요청, 칼퇴 docs/10 2장). 서버 비공개 장부와 공개 카탈로그(`ClockOut-Catalog` `catalog.json` apps)가 **이 값과 같아야** 칼퇴가 "인증된 판"으로 판별한다.

규칙
- **한 판 = 한 SHA.** 같은 판 번호로 zip을 다시 만들면 SHA가 바뀐다. Release에 올린 파일의 SHA만 적고, 다시 만들어야 하면 판을 올린다. `tools/pack_app.ps1`은 이 장부에 같은 판이 다른 SHA로 있으면 실패한다.
- 한 줄은 `out\pack\release-record.json`의 `ledgerRow`를 그대로 옮긴다. Release를 올린 뒤, 카탈로그를 고치는 PR과 같은 때 더한다(docs/21 4장).
- 적기 전에 Release 파일을 받아 다시 계산해 맞춰 본다(`Get-FileHash hunsu.zip -Algorithm SHA256`, 또는 클라우드에서 `sha256sum`).
- 최신이 맨 위.

| 판 | 날짜 | SHA-256 | 크기(바이트) | Release 태그 |
|---|---|---|---|---|
| 0.0.202 | 2026-11-17 | cac873448ea3be172256c688494a75caca0cfdaefb21ce46611eb19375735bbe | 78688939 | app-hunsu-0.0.202 |
| 0.0.201 | 2026-11-17 | cb81f8d5dd17eda2a07bf1b61faf9178749502e819aa411cc03d7c26105248e2 | 78687802 | app-hunsu-0.0.201 |
| 0.0.045 | 2026-11-16 | 3f35ec752e0dc592cf18c7cb946efcdbf7bcb57f891378ac82c32efb9b444cc0 | 77052037 | app-hunsu-0.0.045 |

지난 세 줄은 카탈로그 기록(`catalog.json` 커밋 9a2c4dd, 2326f25, 6729f51)에서 옮겼다. Release 파일을 다시 받아 맞춰 보지는 않았다(2026-11-22).
