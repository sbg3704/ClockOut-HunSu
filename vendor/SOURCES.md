# 외부 공개 라이브러리 출처 (훈수꾼 vendor)

훈수꾼이 받아 쓸 후보(`docs/01` 3.5장 표)를 **실제로 확인한 표**다. 일#201, 확인일 2026-11-12(계정2). **이 표는 후보 확인만 한 것이고 아무것도 받아 오지 않았다.** 실제 패키지 복원과 판 고정은 `src/`가 생긴 뒤에 한다.

## 어떻게 확인했나
- **확인함(NuGet)**: nuget.org의 패키지 정보(최신 안정판, 게시일, 라이선스 표기)와, 그 `.nupkg`를 받아 열어 본 `lib/` 폴더의 지원 프레임워크, 압축 크기.
- **확인 못 함**: GitHub 저장소의 마지막 커밋일과 이슈 상황(이 클라우드 세션에서 해당 저장소 접근이 열려 있지 않았다), 글꼴의 최신 릴리스. 아래 표에서 "미확인"으로 적었다. 마지막 릴리스(게시) 날짜를 관리 상태의 대신 지표로 썼다.
- **Windows 10 호환**은 패키지 안에서 확인할 수 없다. 패키지가 `net8.0-windows` 계열을 지원하는지만 확인했고, Windows 10에서 실제로 도는지는 0단계에서 PC로 본다.
- 크기는 압축된 `.nupkg` 안 파일 크기의 합이다(설치된 크기가 아니다). SkiaSharp는 네이티브 DLL이 커서 배포물에 실제로 들어가는 크기는 0단계에서 잰다.

## 사용자 결정 (2026-11-12)
1. **틀은 최신 장기지원(LTS)판 .NET으로 만든다.** 2026-11-12 현재 최신 LTS는 .NET 10이다(2025-11 출시, 이 세션에서 공식 문서로 다시 확인하지는 않았다). 칼퇴 본체는 .NET 8로 남고, 훈수꾼 틀이 `ClockOut.Core`(net8.0)를 패키지로 받는 것은 net10에서 된다(앞선 버전을 참조하는 것은 허용).
2. **용량은 신경쓰지 않는다.** 이 표의 "크기" 칸은 참고용이고 고르는 기준이 아니다.
3. **라이선스가 중요하다.** 받는 라이브러리의 라이선스는 아래 "라이선스 고지"에 모은다. 칼퇴·훈수꾼 자체의 라이선스는 사용자가 정하는 중이다(HANDOFF 참고).

## 결론 먼저
1. **.NET 10으로 가면 SkiaSharp는 최신 안정판 4.156.0을 쓴다.** `SkiaSharp`, `SkiaSharp.Views.WPF`, `SkiaSharp.Skottie` 세 패키지 모두 `net10.0-windows10.0.19041` 빌드가 있다. (.NET 8을 유지했다면 `net8.0-windows` 빌드가 있는 마지막 판 3.119.2로 고정해야 했다. 결정 1로 이 문제는 없어졌다.)
2. `WPF-UI` 4.3.0과 `MaterialDesignThemes` 5.4.0도 `net10.0-windows7.0` 빌드가 있다. 나머지(`HandyControl`, `LottieSharp`, `FluentIcons.Wpf`)는 net8 이하 빌드만 있어 net10에서 호환으로 동작하는데, 이 세션에서 실제로 돌려 보지는 않았다(0단계, PC).
3. 허용형 라이선스 조건은 확인한 후보가 모두 통과했다(MIT 또는 OFL). 확인하지 못한 것은 HandyControl 하나다.

## 후보 표
| 쓸 자리 | 패키지 (NuGet id) | 고정할 판 | 게시일 | 라이선스 | .NET 8 (WPF) | 크기 | 비고 |
|---|---|---|---|---|---|---|---|
| 현대적인 부품 모양 | `WPF-UI` | 4.3.0 (최신 안정판) | 2026-05-04 | MIT | 있음 `net8.0-windows7.0` | 16.4MB | `Wpf.Ui`라는 이름의 패키지는 옛 3.4.2.7이다. 받을 것은 `WPF-UI`. |
| 대체 부품 모음 | `MaterialDesignThemes` | 5.4.0 (최신 안정판) | 2026-10-20 | MIT | 있음 `net8.0-windows7.0` | 19.5MB | 필요할 때만. 더 최근 판은 CI 시험판뿐. |
| 대체 부품 모음 | `HandyControl` | 3.5.1 (최신 안정판) | 2024-03-05 | **미확인**(패키지 표기가 파일 이름 `LICENSE`뿐이라 종류를 이 세션에서 읽지 못했다) | 있음 `net8.0` | 7.5MB | 마지막 안정판이 2년 전이고 3.6.0은 시험판(rc3, 2025-09)이다. **라이선스 종류를 확인하기 전에는 쓰지 않는다.** |
| 창 다루기 | `Microsoft.Windows.CsWin32` | 0.3.371 (최신 안정판) | 2026-11-10 | MIT | 해당 없음(소스 생성기, 모든 프레임워크에서 동작) | 8.8MB | 안정판이 없는 `Microsoft.Windows.SDK.Win32Metadata`(시험판 71.0.45-preview)에 기대는 것은 이 도구의 정상 구성이다. |
| 벡터 그림, 그래프 | `SkiaSharp` + `SkiaSharp.Views.WPF` | 4.156.0 (최신 안정판) | 2026-11-11 | MIT | 있음 `net10.0-windows10.0.19041` | 14.5MB + 0.2MB | .NET 8용 부품은 없다(결정 1로 .NET 10을 쓰므로 문제 없음). |
| 움직이는 아이콘과 효과 | `SkiaSharp.Skottie` (Lottie 재생) | 4.156.0 (최신 안정판) | 2026-11-11 | MIT | 있음 `net10.0` | 0.6MB | 후보 표의 "Lottie(SkiaSharp 기반)"는 이것이다. |
| 움직이는 아이콘 (대안) | `LottieSharp` | 2.4.3 (최신 안정판) | 2025-06-09 | MIT | 있음 `net8.0-windows7.0` | 0.2MB | SkiaSharp 없이 WPF에서 그리는 다른 구현. 1년 반 갱신 없음. 비교용. |
| 순서도 자동 배치(계산) | `Microsoft.Msagl` | 1.1.6 (최신 안정판) | 2023-02-23 | MIT | 있음 `netstandard2.0` | 1.0MB | 계산만 하고 그리지 않는다. 마지막 릴리스가 3년 가깝다. |
| 순서도 그리기 모델 | `Microsoft.Msagl.Drawing` | 1.1.6 | 2023-02-23 | MIT | 있음 `netstandard2.0` | 0.1MB | |
| 순서도 WPF 그림판 (참고) | `Msagl.WpfGraphControl` (levnach) | 1.2.1 | 2024-12-14 | MIT | 있음 `net6.0-windows7.0`(8에서 동작) | 0.1MB | **Microsoft 공식이 아니다**(공식 이름 `Microsoft.Msagl.WpfGraphControl`은 NuGet에 없다). 훈수꾼은 SkiaSharp로 직접 그릴 계획이라 계산용 `Microsoft.Msagl`만 쓰고 이 패키지는 받지 않는 쪽을 권한다. |
| 아이콘 | `FluentIcons.Wpf` (davidxuang) | 2.1.345 (최신 안정판) | 2026-10-31 | MIT | 있음 `net6.0-windows7.0`(8에서 동작) | 9.3MB | Microsoft 아이콘 모음을 WPF용으로 감싼 **비공식** 패키지. 원본 `microsoft/fluentui-system-icons`(MIT로 알려져 있음)의 릴리스와 라이선스는 **미확인**. 아이콘 몇 개만 쓸 거면 원본 SVG를 `vendor/`에 두는 편이 가볍다. |
| 글꼴(본문) | Pretendard | 칼퇴에 번들된 v1.3.9 | 최신판 **미확인** | OFL 1.1 (칼퇴에 든 `OFL-Pretendard.txt`로 확인) | 해당 없음 | 칼퇴 앱에 약 1.5MB × 4굵기 | 칼퇴 `app/ClockOut.App/Fonts/`와 같은 파일을 쓰면 판이 어긋나지 않는다. |
| 글꼴(제목) | SUIT | 칼퇴에 번들된 판(판 번호 미기록) | 최신판 **미확인** | OFL 1.1 (칼퇴의 `OFL-SUIT.txt`) | 해당 없음 | 0.35MB × 2굵기 | |
| 글꼴(숫자·코드) | IBM Plex Sans KR | 칼퇴에 번들된 판(판 번호 미기록) | 최신판 **미확인** | OFL 1.1 (칼퇴의 `OFL-IBMPlexSansKR.txt`) | 해당 없음 | 약 1MB × 2굵기 | |
| VBA에서 JSON | VBA-JSON | `clockout-vendor-data`에 이미 있음 | — | MIT | 해당 없음 | — | 새로 받을 것 없음. |

## 라이선스 고지
배포물에는 받은 라이브러리마다 **라이선스 전문(저작권 줄 포함)을 함께 넣는다**(`docs/01` 3.5장). 아래는 조건 요약이고 법률 자문이 아니다.

| 라이선스 | 해당 | 지켜야 할 것 |
|---|---|---|
| MIT | WPF-UI, MaterialDesignThemes, CsWin32, SkiaSharp 3종, LottieSharp, Microsoft.Msagl 2종, Msagl.WpfGraphControl, FluentIcons.Wpf | 저작권 줄과 MIT 전문을 배포물에 함께 넣는다. 보증 없음. |
| OFL 1.1 | Pretendard, SUIT, IBM Plex Sans KR | `OFL-*.txt`를 함께 넣는다. 글꼴 파일만 따로 팔지 않는다. 고쳐서 낼 때는 예약된 글꼴 이름을 쓰지 않는다. 칼퇴는 원본 그대로 쓴다. |
| 미확인 | HandyControl | 종류를 읽기 전에는 쓰지 않는다. |

받아 둔 라이브러리의 라이선스 파일은 `vendor/<이름>/`에 원본 그대로 둔다. 칼퇴·훈수꾼 **자체의 라이선스**는 아직 정해지지 않았다(패키지 `ClockOut.Core`에도 표기 없음).

## 남은 확인 (PC나 접근 권한이 있을 때)
- GitHub 저장소 상태(마지막 커밋일, 열린 이슈, 보관 여부): `lepoco/wpfui`, `MaterialDesignInXAML/MaterialDesignInXamlToolkit`, `HandyOrg/HandyControl`, `microsoft/CsWin32`, `mono/SkiaSharp`, `microsoft/automatic-graph-layout`, `microsoft/fluentui-system-icons`.
- HandyControl 라이선스 종류.
- 글꼴 세 가지의 최신 릴리스와 칼퇴에 번들된 판의 일치.
- Windows 10에서 각 패키지가 도는지(0단계, PC).
