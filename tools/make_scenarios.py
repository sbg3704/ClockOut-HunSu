#!/usr/bin/env python3
# -*- coding: utf-8 -*-
r"""
기능: 훈수꾼 시나리오 통합 문서(tools/scenarios/훈수꾼_시나리오.xlsx)를 만든다. 모듈마다 "어디를 보면 무엇이 나와야 하는지"가 시트와 안내 표로 들어 있다.
사용법(클라우드/개발자 PC, 엑셀 불필요): python3 tools/make_scenarios.py   (XlsxWriter 필요: pip install xlsxwriter)
      모듈을 더하면 아래 SCENARIOS 표에 한 줄, 필요하면 시트 함수 하나를 더하고 다시 만들어 xlsx를 커밋한다(docs/07 4장 규칙).
      PC 빌드(co build hunsu)는 이 파일을 out\ 로 복사하고, 큰 시트(큰 표, 대용량)는 tools\make_example_xlsx.ps1이 엑셀로 더한다.
의존: xlsxwriter. 값은 전부 시험용 가짜 자료다(회사 자료 없음). 외부 링크는 xlsx 안의 XML을 직접 더해 만든다(XlsxWriter에 없음).
"""
import re
import shutil
import sys
import zipfile
from pathlib import Path

import xlsxwriter

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "scenarios" / "훈수꾼_시나리오.xlsx"

# 모듈 id, 이름, 시트, 해볼 것, 나와야 하는 것  (모든 modules/ 폴더가 여기 있어야 한다: ModuleToolsTests가 검사)
SCENARIOS = [
    ("hunsu-selection", "선택 요약", "매출", "B3:F14를 마우스로 고른다", "개수 60과 합계·평균·최소·최대가 나온다. 범위를 바꾸면 바로 바뀐다."),
    ("hunsu-selection", "선택 요약", "섞임", "A2:A12를 고른다", "숫자·글자·수식 개수가 나오고 오류 3개 배지가 보인다."),
    ("hunsu-selection", "선택 요약", "큰 표 / 대용량", "A열 머리글을 눌러 열 전체를 고른다. 큰 범위를 마우스로 끌어 본다", "104만 칸이어도 바로 나오고 패널이 떨리지 않는다(docs/06)."),
    ("hunsu-selection", "선택 요약", "숨김행열", "A2:G14를 고른다(숨은 행 3개·열 2개가 섞여 있음)", "개수 50, 합계 3,780, 평균 75.6, 최소 10, 최대 136. '숨은 줄·열의 41칸은 뺐습니다'가 보인다(숨긴 칸은 세지 않는 규칙, 칼퇴 추가기능과 같음)."),
    ("hunsu-errors", "오류 셀", "오류", "시트를 연다(아무 칸이나)", "오류 셀 7개(종류별 1개씩). 목록 줄을 누르면 그 칸으로 이동한다."),
    ("hunsu-sheets", "시트 이동", "아무 시트", "패널의 시트 이름을 누른다", "보이는 시트만 나열되고(숨김_시트, 아주숨김_시트는 없음) 누르면 그 시트로 이동, 지금 시트가 강조된다."),
    ("hunsu-flow", "수식 흐름", "흐름", "B1, D1, E1을 차례로 고른다", "B1: 쓰는 칸 A1 1개, 쓰는 칸을 쓰는 C1·D1 2개. D1: 쓰는 칸 3개. E1: '다른 시트도 사용합니다' 배지. 목록을 누르면 이동."),
    ("hunsu-book", "책 정보", "아무 시트", "아무 칸에 값을 쓴다. 수식 탭 > 계산 옵션 > 수동으로 바꿔 본다(끝나면 자동으로 되돌린다)", "저장 안 된 변경 배지, 수동 계산 경고(빨강)가 나온다. 시트 수는 20개(큰 시트를 더하면 22)."),
    ("hunsu-audit", "책 점검", "아무 시트", "패널을 본다", "숨은 시트 1(숨김_시트), 아주 숨김 1, 보호된 시트 1(보호됨), 외부 링크 1개."),
    ("hunsu-names", "이름 목록", "이름", "패널의 이름을 누른다", "보이는 이름 5개(엑셀 이름 관리자 기준. Names.Count는 숨은 이름 2개가 더해 7). 세율·합계범위는 눌러 이동, 버전표시·오늘·깨진이름은 '수식·상수'라 이동하지 않는다."),
    ("hunsu-keys", "단축키 표", "아무 시트", "패널을 본다", "다리 없이 단축키 표가 나온다."),
    ("hunsu-flowgraph", "수식 족보", "흐름", "E1을 고른 뒤 D1을 고른다", "E1: 쓰는 칸에 다른 시트 칸 매출!G15가 굵게 나오고 누르면 그 시트로 간다. D1: A1·B1·C1과 그 아래 2단계 들여쓰기, 쓰이는 칸 E1."),
    ("hunsu-marks", "칸 위 형광펜", "오류 / 흐름", "[오류]를 열고 스크롤해 본다. [흐름]에서 D1을 고른다", "오류 칸에 빨간 테두리(스크롤하면 0.4초 안에 따라옴). D1이 쓰는 A1:C1은 파란 칠, D1을 쓰는 E1은 초록 밑줄. 확대 100% 아닐 때도 칸에 맞는지 본다."),
    ("(공통) 최소화", "패널 줄이기", "흐름", "색연필 수식 흐름·형광펜·십자 음영 패널은 처음부터 줄어 있다. 다른 패널 제목줄의 '작게(아이콘으로)' 단추를 눌러 본다", "패널이 사라지고 런처에 아이콘으로 남는다(평소에도 보임, 투명도 75% 이상). 아이콘을 누르면 다시 열린다. 줄여도 프로그램이 꺼지지 않는다."),
    ("(공통) 최소화", "일하는 점", "흐름", "색연필 수식 흐름이 줄어 있는 채로 D1·E1을 차례로 고른다", "줄어든 아이콘 귀퉁이 점이 숨 쉬듯 깜빡인다. 패널을 열면 점은 없어지고 열림 고리가 보인다."),
    ("(공통) 빠른 설정", "우클릭 메뉴", "매출", "모듈 아이콘(런처·빠른 띠)이나 패널 제목줄을 우클릭한다. 선택 요약은 '끄는 동안 커서 옆 합계', 목록 모듈(오류 셀 등)은 '목록 줄 수'", "맨 위에 그 모듈의 옵션(체크·고르기)이 묶음 제목과 함께 나오고, 맨 아래에 '자세히 설정…'이 있다. 고르면 바로 반영된다."),
    ("(공통) 옵션 쪽", "설정 창 [옵션]", "매출", "알림 영역 아이콘 → '모듈 관리…' → [옵션]", "옵션을 가진 모듈마다 같은 항목이 순서·묶음대로 보이고, 우클릭 메뉴에서 바꾼 값과 같다. 여기서 바꿔도 바로 반영된다."),
    ("(공통) 독 위치", "독은 왼쪽, 패널은 그 오른쪽", "매출", "엑셀을 켜고 훈수꾼을 띄운다. 모듈 관리자에서 [색연필 수식 흐름]을 켠다", "독(훈 원과 아이콘)은 엑셀 창 왼쪽 가장자리에 하나만 있고, 열린 패널은 그 독의 오른쪽에 붙는다(색연필 수식 흐름도 왼쪽). 오른쪽 가장자리에는 아무것도 없다. 오른쪽 독은 모듈 관리자에서 [오른]으로 옮긴 모듈이 있을 때만 나타난다."),
    ("(공통) 따라다니는 글 HUD", "선언만으로 커서·칸 옆 글", "매출", "선택 요약의 우클릭 빠른 설정에서 '끄는 동안 커서 옆에 합계'를 켠 채 B3:F14를 끌어 고른다. 이어서 '붙는 곳'을 '고른 칸 옆'으로 바꿔 다시 고른다", "끄는 동안 커서 옆에 '합계 … · …개'(오류 칸이 있으면 '· 오류 n')가 따라다닌다. '고른 칸 옆'이면 고른 칸 오른쪽에 계속 붙는다. 이 글과 붙는 곳은 모듈 선언(hud)에 적힌 것이고 프로그램은 합계라는 말을 모른다."),
    ("(공통) 선언 단축키", "단축키·허브 메뉴가 모듈 선언에서 나온다", "흐름", "엑셀이 앞일 때 Ctrl+Alt+D를 누른다. 훈 원을 우클릭한다. 설정 창 [일반]을 본다", "그림이 꺼지고 켜진다. 허브 메뉴에 '수식 흐름 그리기 Ctrl+Alt+D'가 있고 눌러도 같다. 설정 창 [일반]에는 색연필 모양·커서 HUD 줄이 없고 [옵션]에 모듈별로 있다."),
    ("(공통) 부채 런처", "펼침 움직임", "매출", "설정 창 [모양]에서 배치를 '부채'로 바꾼다. 마우스를 독 가까이로 가져갔다 뗀다", "접혀 있던 아이콘이 훈 원에서 한 칸씩 차례로 부채처럼 펼쳐지고, 멀어지면 훈 원 속으로 다시 접힌다."),
    ("hunsu-pencilflow", "색연필 수식 흐름", "흐름", "D1을 골라 그림이 나온 뒤 Ctrl+Alt+D(엑셀이 앞일 때)를 누른다. 또 훈 원 우클릭 '수식 흐름 그리기 켜기·끄기', 아이콘 우클릭 '그리기 켜기'", "그림이 사라지고(모듈은 켜진 채), 다시 누르면 새로 그려진다. 모양(색연필·형광 마커·펜)도 우클릭에서 바뀐다. 그리는 모습은 그대로다."),
    ("(공통) 오류 기록", "오류 파일", "아무 시트", "문제가 생겨 꺼지거나 멈추면 %LOCALAPPDATA%\\ClockOut\\Hunsu\\logs 폴더의 오늘 날짜 error-날짜.log를 보낸다", "시각·판 번호·오류 종류·호출 순서가 적혀 있다(셀 값은 없음, 경로가 섞일 수 있으니 확인)."),
    ("hunsu-pencilflow", "색연필 수식 흐름", "흐름", "모듈 관리자에서 [색연필 수식 흐름]을 켠다(형광펜과 같이 켜면 겹치니 형광펜은 끈다). [흐름]에서 B8을 고른 뒤 B9, B10, D1을 차례로 고른다. 칸을 옮길 때마다, 스크롤할 때, 확대를 바꿔도 본다", "B8: 주황 두 겹 고리가 한 번에 쓱 둘러지고, 파란 칠한 B6·B7에서 B8로 휘어진 화살표 두 개와 B8에서 초록 B9·B10으로 가는 화살표가 약 1초 안에 연달아 휙휙 그려진다. B6·B7은 파란 네모, B9·B10은 초록 동그라미로 모양이 다르고(범위를 고르면 파랑은 대괄호, 초록은 동그라미+물결 밑줄), 화살표마다 작은 번호 1·2·3·4가 붙어 읽는 순서대로 따라갈 수 있다. 칸 위에 손글씨 이름표('B6 (쓰는 칸)', 'B9 (쓰이는 칸)'). 한 단계 더 앞뒤도 가늘고 옅게 이어진다(B10을 고르면 B8 → B10, 그리고 B10 → B11, 번호는 계산 순서). 다른 시트: 흐름 시트 E1(=D1+매출!G15)을 고르면 화면 아래 시트 탭의 [매출]을 빙 두르고 칸에서 탭까지 쓕 내려갔다 돌아오는 화살표와 '매출 시트 G15 에서 가져와요' 설명이 나중에 덧붙어 그려진다(번호는 가장 먼저). B10을 고르면 [메모] 탭으로 '메모 시트 G1 로 보내요'. 탭 자리는 시트 이름 글자 수로 어림하니 탭 위치가 몇 칸 어긋나도 설명 글이 어느 시트인지 알려 준다. 설정 창 [일반]의 「수식 흐름 그림」에서 색연필·형광 마커·펜을 바꿔 본다: 셋 다 획이 가늘게 시작해 굵어지고 날렵하게 빠지는 손글씨 느낌이고, 마커는 굵고 반투명, 펜은 가늘고 칠하지 않는다. 바꾸면 바로 새로 그려진다. 값만 바꿔도 그림은 그대로다. 스크롤·확대에서는 움직임 없이 칸에 붙어 따라오고, 화살표 모양은 그대로다. 수식도 쓰이는 칸도 없는 칸(A5)에서는 아무것도 안 그린다. D1은 A1:C1을 한 덩어리로 두르고 E1로 간다."),
    ("hunsu-history", "발자취", "매출 / 흐름", "매출 B3, 매출 F14, 흐름 A1을 1초 넘게 간격을 두고 차례로 고른 뒤 [← 뒤로]를 누른다", "목록에 다녀간 칸 3줄(최근 것이 위). 뒤로를 누르면 매출!F14로 가고, 목록이 다시 쌓이지 않으며 [앞으로 →]가 생긴다."),
    ("hunsu-cell", "셀 정보", "셀정보", "A2부터 A12까지 한 칸씩 고른다", "각 칸 옆(C열)에 적힌 기대와 같다: 수식·종류(숫자/날짜/텍스트/참거짓/오류/빈 칸)·서식·병합·메모·잠금."),
    ("hunsu-sheetinfo", "시트 정보", "필터 / 보호됨 / 매출", "필터 시트: 필터 적용 중. 보호됨: 시트 보호됨. 매출: 틀 고정", "사용 범위 크기, 보호, 필터, 틀 고정, 조건부 서식 수가 맞게 나온다."),
    ("hunsu-composition", "시트 구성", "매출", "시트를 연다", "수식 24 · 숫자 60 · 글자 22 · 그 밖 6(사용 범위 112칸)."),
    ("hunsu-comments", "메모와 댓글", "메모", "시트를 연다", "메모 4개 목록. 줄을 누르면 그 칸으로 이동. 스레드 댓글은 직접 하나 달아 보면 '댓글'로 늘어난다."),
    ("hunsu-links", "외부 링크", "링크", "파일을 열 때 링크 보안 경고가 뜨면 '업데이트 안 함'", "원본_연결용.xlsx 1개(파일 이름만, 경로 없음)."),
    ("hunsu-tables", "표 목록", "표", "시트를 연다", "표 3개(매출표, 직원표, 재고표)와 줄 수. 누르면 그 표로 이동."),
    ("hunsu-validation", "유효성 영역", "유효성", "시트를 연다", "영역 4곳 · 40칸(목록, 정수, 날짜, 사용자 수식). 누르면 이동."),
    ("hunsu-condrules", "조건부 서식", "조건부", "시트를 연다", "규칙 10개(셀 값, 수식, 색조, 데이터 막대, 아이콘, 상위, 중복, 텍스트, 빈 칸, 오류). 누르면 이동."),
    ("hunsu-hidden", "숨은 행·열", "숨김행열", "시트를 연다", "숨은 행 3개(5, 6, 10행)·숨은 열 2개(D, F열). 누르면 그곳으로 이동."),
    ("hunsu-audit", "책 점검(쓰기 실험)", "아무 시트", "[숨은 시트 모두 보이기 (실험)]을 누른다. 처음이면 '실험 기능입니다' 공지가 먼저 뜬다 → [이해했습니다]. 확인 창에서 [바꾸기]. 끝나면 [되돌리기]도 눌러 본다", "공지(처음 한 번) → '1건이 바뀝니다' 확인 → 숨김_시트가 보인다 → [되돌리기]로 다시 숨는다. 아주 숨김 시트는 그대로. 실행 뒤에는 저장하지 말고 닫는다(다음 시험용)."),
    ("hunsu-links", "외부 링크(쓰기 실험)", "링크", "[외부 링크 모두 끊기 (실험)] → [바꾸기]", "'1건이 바뀝니다' 확인 → 링크가 끊겨 패널의 링크 수가 0이 된다. 되돌리기는 없다. 저장하지 말고 닫는다."),
    ("hunsu-names", "이름 목록(쓰기 실험)", "이름", "[깨진 이름(#REF!) 지우기 (실험)] → [바꾸기] → [되돌리기]", "깨진이름(#REF!) 1건 확인 → 지워진다 → 되돌리기로 다시 생긴다. 다른 이름은 그대로."),
    ("hunsu-sheetinfo", "시트 정보(쓰기 실험)", "마지막칸", "시트를 열고 [마지막 칸 바로잡기 (실험)] → [바꾸기]", "'118건이 바뀝니다'(빈 행 95 + 빈 열 23) 확인 → 사용 범위가 5 × 3으로 줄고 Ctrl+End가 C5로 간다. 표·도형·메모·보호가 있는 시트는 거절한다. 되돌리기는 없다. 저장하지 말고 닫는다."),
    ("hunsu-crosshair", "십자 음영", "매출", "B3, 그다음 D8을 고른다. 스크롤해 본다. 틀 고정 시트에서도 해 본다", "고른 칸의 행과 열이 연한 파란 띠로 칠해진다(칸 색은 그대로 보임). 스크롤하면 0.4초 안에 따라온다. 행 머리글을 눌러 통째로 고르면 행 띠는 없고 열 띠만 보인다."),
    ("hunsu-colorsum", "색깔별 합계", "색깔", "A2:A10을 고른다. 그다음 C2:C7을 고른다(패널은 [새로 고침]이나 열 때 계산한다)", "A2:A10: 채우기 #FFC000 4칸 합계 100, #92D050 3칸 합계 45, 색 없음 2칸 합계 10. C2:C7: 글자색 #FF0000 3칸 합계 600, #000000 3칸 합계 6."),
    ("hunsu-coldiff", "두 열 비교", "비교", "A2:B11을 고른다. 한 열만 고르면 안내 문구가 나온다", "다른 줄 5개(3, 5, 7, 8, 10행): 대소문자(banana/Banana), 20/21, 글자 x와 빈 칸, 글자 '1'과 숫자 1, 'a '와 'a'. 줄을 누르면 그 줄 A열로 이동. 선택은 바뀌지 않는다."),
    ("hunsu-folder", "폴더 파일 목록", "아무 시트", "이 파일(훈수꾼_확인용.xlsx)이 든 폴더를 본다", "같은 폴더의 엑셀 파일과 그 밖의 파일이 나뉘어 나온다. 엑셀 파일 줄을 누르면 그 파일이 열린다. 저장하지 않은 새 책에서는 '저장한 파일에서만 볼 수 있습니다'."),
    ("hunsu-valuetools", "값 정리(쓰기 실험)", "값정리", "A2:A6을 고르고 [공백·보이지 않는 글자 지우기] → [바꾸기] → [되돌리기]. 같은 식으로 B열 [글자 숫자를 숫자로], C열 [반올림·올림·절사](단위 10, 반올림), D열 [부호 뒤집기], E열 [앞뒤에 글자 붙이기](앞 '№', 뒤 '건'), F열 [수식을 값으로], G열 [IFERROR로 감싸기], H열 [빈 칸을 위 값으로 채우기], I열 [글자 날짜를 날짜로], J열 [병합 풀고 채우기]를 해 본다", "필요한 입력(단위·글자)은 '다음' 창으로 먼저 묻는다. 바뀌는 칸 수를 보여 주고, 바꾼 뒤 [되돌리기]로 원래 값·수식·서식으로 돌아온다(병합 풀기와 가운데 맞춤은 되돌리기 없음). 앞 0 코드(007)와 15자리 넘는 숫자는 숫자로 바꾸지 않고 건너뛴다. 저장하지 말고 닫는다."),
    ("hunsu-booktools", "책·시트 도구(쓰기 실험)", "시트목록", "저장한 책에서 A2:A5를 고르고 [고른 목록으로 시트 만들기] → [바꾸기] → [되돌리기]. 이어서 D1:E9(머리글 포함)를 고르고 [표를 값별 시트로 나누기](기준 열 1)", "시트 4개가 맨 뒤에 생기고 되돌리면 지워진다('이미 있는 이름'은 건너뜀). 나누기는 팀별 시트가 값만 담겨 생긴다. 저장하지 않은 새 책에서는 만들기만 되고 파일 도구는 '저장한 파일에서만' 안내가 나온다."),
    ("hunsu-booktools", "책·시트 도구(쓰기 실험)", "아무 시트", "[목차 시트 만들기] → [바꾸기] → 목차의 링크를 눌러 본다 → [되돌리기]. 그다음 [지금 책 백업 사본 저장], [시트마다 파일로 내보내기], [고른 시트 PDF로 저장]", "목차 시트가 맨 앞에 생기고 링크가 시트로 이동한다. 백업·PDF·시트별 폴더는 이 파일 옆 폴더에 새로 생기고 기존 파일은 그대로다. 되돌리기는 시트 만들기 계열만 있다."),
    ("hunsu-valuetools", "값 정리(쓰기 실험)", "값정리", "K열(금액)을 고르고 [숫자를 한글 금액으로]. L열(품목)을 고르고 [자연 순서로 정렬]. M열(입력)을 고르고 [입력값 비우기]. 각각 [바꾸기] 뒤 [되돌리기]도 해 본다", "K2:K5가 '일천이백삼십사원', '영원', '일억오천만원', '삼십조원'처럼 글자가 된다(음수·소수는 건너뜀). L열은 품목1, 품목2, 품목10 순으로 정렬된다(보통 정렬이면 품목10이 품목2 앞). M열 상수가 비워지고 수식 칸 M6은 그대로. 모두 되돌려진다."),
    ("hunsu-booktools", "책·시트 도구(쓰기 실험)", "시트목록", "저장한 책에서 [시트 이름 바꾸기](찾을 글자 '쪽', 바꿀 글자 '면') → [되돌리기]. [시트 탭 이름 순 정렬] → [되돌리기]. [공유 전 화면 정리]. 맨 끝에 [고른 시트만 남기기]는 저장해 둔 사본에서 Ctrl로 시트 두 개를 고른 뒤 해 본다", "'동쪽' 등의 이름이 '동면'으로 바뀌고 되돌리면 돌아온다. 탭이 가나다·ABC 순으로 늘어서고 되돌리면 원래 순서. 공유 전 정리는 모든 시트 커서가 A1로 가고 첫 시트가 열린다. 남기기는 고르지 않은 시트가 사라지고 되돌리기가 없다(사본에서만)."),
]

SHEETS = ["안내", "매출", "섞임", "오류", "흐름", "이름", "메모", "표", "유효성", "조건부", "숨김행열", "색깔", "비교", "마지막칸", "값정리", "시트목록", "필터", "셀정보", "보호됨", "링크", "숨김_시트", "아주숨김_시트"]


def build(path: Path) -> None:
    wb = xlsxwriter.Workbook(str(path))
    wb.set_properties({"title": "훈수꾼 시나리오", "comments": "시험용 가짜 자료입니다. 훈수꾼은 셀 값을 바꾸지 않습니다."})
    bold = wb.add_format({"bold": True})
    title = wb.add_format({"bold": True, "font_size": 16})
    head = wb.add_format({"bold": True, "bg_color": "#DDEBF7", "border": 1, "text_wrap": True, "valign": "top"})
    cell = wb.add_format({"border": 1, "text_wrap": True, "valign": "top"})
    muted = wb.add_format({"font_color": "#808080"})
    num1 = wb.add_format({"num_format": "#,##0.0"})
    numb = wb.add_format({"num_format": "#,##0.0", "bold": True})
    datef = wb.add_format({"num_format": "yyyy-mm-dd"})
    ws = {n: wb.add_worksheet(n) for n in SHEETS}

    # --- 안내 ---
    a = ws["안내"]
    a.set_column("A:A", 18); a.set_column("B:B", 16); a.set_column("C:C", 44); a.set_column("D:D", 70)
    lines = [
        ("훈수꾼 시나리오 통합 문서", title),
        ("이 파일 하나로 모든 모듈을 시험합니다. 값은 전부 시험용 가짜 자료이고, 훈수꾼은 셀 값을 바꾸지 않습니다.", None),
        ("", None),
        ("0. 모든 모듈 켜기: 훈수꾼 알림 영역 아이콘 → '모듈 관리…' → [모두 켜기]. (또는 settings.json의 enabledByDefault에 [\"*\"])", None),
        ("1. 엑셀 창 오른쪽(또는 왼쪽)에 모듈 알약이 붙습니다. 아래 표의 '해볼 것'을 하고 '나와야 하는 것'과 같은지 보세요.", None),
        ("2. 다르거나 화면이 떨리거나 엑셀이 버벅이면, 어느 모듈·어느 칸인지 적어서 알려 주세요(빌드 기록은 co copy).", None),
        ("3. 파일을 열 때 '링크 업데이트' 보안 경고가 뜨는 것은 정상입니다(링크 시나리오). [업데이트 안 함]을 누르세요.", None),
        ("4. 큰 시트(큰 표 2만 줄, 대용량 10만 줄)는 co build 중에 엑셀로 더합니다. 없으면 빌드 기록의 경고를 알려 주세요.", None),
        ("", None),
    ]
    for i, (t, f) in enumerate(lines):
        a.write(i, 0, t, f)
    r0 = len(lines)
    for c, h in enumerate(["모듈 id", "이름 · 시트", "해볼 것", "나와야 하는 것"]):
        a.write(r0, c, h, head)
    for i, (mid, name, sheet, todo, expect) in enumerate(SCENARIOS, start=1):
        a.write(r0 + i, 0, mid, cell)
        a.write(r0 + i, 1, f"{name} · {sheet}", cell)
        a.write(r0 + i, 2, todo, cell)
        a.write(r0 + i, 3, expect, cell)
    a.write(r0 + len(SCENARIOS) + 2, 0, "새 모듈을 더하면 이 표(tools/make_scenarios.py의 SCENARIOS)에 시나리오를 더하고 파일을 다시 만듭니다.", muted)

    # --- 매출: 12달 x 5팀 (선택 요약, 구성, 틀 고정) ---
    s = ws["매출"]
    s.write("A1", "월별 매출(만 원)", bold)
    s.write("A2", "월")
    teams = ["영업1팀", "영업2팀", "기술팀", "지원팀", "해외팀"]
    for t, name in enumerate(teams):
        s.write(1, 1 + t, name)
    s.write("G2", "합계")
    for m in range(12):
        s.write(2 + m, 0, f"{m + 1}월")
        row_sum = 0.0
        for t in range(5):
            v = round(800 + ((m + 3) * 131 + (t + 2) * 57) % 1700 + (((m + 3) * (t + 2)) % 10) / 10, 1)
            s.write_number(2 + m, 1 + t, v, num1)
            row_sum += v
        s.write_formula(2 + m, 6, f"=SUM(B{3 + m}:F{3 + m})", num1, round(row_sum, 1))
    s.write("A15", "합계", bold); s.write("A16", "평균", bold)
    for c in "BCDEFG":
        s.write_formula(f"{c}15", f"=SUM({c}3:{c}14)", numb)
        s.write_formula(f"{c}16", f"=AVERAGE({c}3:{c}14)", numb)
    s.freeze_panes(2, 1)
    s.set_column("A:G", 11)

    # --- 섞임 ---
    s = ws["섞임"]
    s.write("A1", "여러 종류(숫자·글자·날짜·빈칸·수식·오류)", bold)
    s.write_number("A2", 120); s.write_number("A3", 45.5); s.write("A4", "글자")
    s.write_formula("A5", "=DATE(2026,11,12)", datef, 46338)
    s.write_formula("A7", "=A2*2", None, 240)
    s.write_formula("A8", "=A2/0", None, "#DIV/0!")
    s.write_formula("A9", "=NA()", None, "#N/A")
    s.write_formula("A10", '=VLOOKUP("없음",A2:A4,1,FALSE)', None, "#N/A")
    s.write_number("A11", -30); s.write("A12", "메모")
    s.write("B2", "← A2:A12를 골라 보세요: 오류 3개, 수식 5개", muted)
    s.set_column("A:A", 14)

    # --- 오류: 종류별 7개 ---
    s = ws["오류"]
    s.write("A1", "오류 셀 7개(종류별)", bold)
    s.write("B2", "입력"); s.write_number("A2", 1); s.write_number("A3", 2)
    errs = [("C2", "=1/0", "#DIV/0!"), ("C3", "=NA()", "#N/A"), ("C4", "=#REF!", "#REF!"), ("C5", "=FOO()", "#NAME?"),
            ("C6", '="가"+1', "#VALUE!"), ("C7", "=SQRT(-1)", "#NUM!"), ("C8", "=A2:A3 B2:B3", "#NULL!")]
    for addr, f, v in errs:
        s.write_formula(addr, f, None, v)
    s.write("D2", "← 각 줄: #DIV/0!, #N/A, #REF!, #NAME?, #VALUE!, #NUM!, #NULL!", muted)

    # --- 흐름 ---
    s = ws["흐름"]
    s.write("A1", 10)
    s.write_formula("B1", "=A1*2", None, 20); s.write_formula("C1", "=B1+A1", None, 30)
    s.write_formula("D1", "=SUM(A1:C1)", None, 60); s.write_formula("E1", "=D1+매출!G15", None, 0)
    s.write("A5", "색연필 시험", bold)
    s.write("A6", "단가"); s.write_number("B6", 1200)
    s.write("A7", "수량"); s.write_number("B7", 3)
    s.write("A8", "공급가"); s.write_formula("B8", "=B6*B7", None, 3600)
    s.write("A9", "세금"); s.write_formula("B9", "=B8*0.1", None, 360)
    s.write("A10", "합계"); s.write_formula("B10", "=B8+B9", None, 3960)
    s.write("D8", "← B8: 쓰는 칸 B6·B7, 쓰이는 칸 B9·B10 (색연필 수식 흐름)", muted)
    s.write("A3", "B1: 쓰는 칸 A1, 쓰는 칸을 쓰는 C1·D1 / D1: 쓰는 칸 3개 / E1: 다른 시트 배지", muted)

    # --- 이름 ---
    s = ws["이름"]
    s.write("A1", "이름(Name) 5개", bold)
    s.write("A2", "세율"); s.write_number("B2", 0.1)
    wb.define_name("세율", "=이름!$B$2")
    wb.define_name("합계범위", "=매출!$B$3:$B$14")
    wb.define_name("버전표시", '="v1.0"')
    wb.define_name("오늘", "=TODAY()")
    wb.define_name("깨진이름", "=#REF!")
    s.write("A4", "← 세율, 합계범위는 눌러 이동 / 버전표시, 오늘, 깨진이름은 수식·상수", muted)

    # --- 메모: 노트 4개 ---
    s = ws["메모"]
    s.write("A1", "메모(노트) 4개", bold)
    for i, (addr, txt) in enumerate([("B3", "확인 필요: 합계가 안 맞음"), ("D5", "이 값은 분기 말에 갱신"), ("B8", "담당자에게 물어볼 것"), ("E10", "삭제 예정")]):
        s.write(addr, f"값{i + 1}")
        s.write_comment(addr, txt, {"author": "시험", "x_scale": 1.5})

    s.write("F1", "← 흐름!B10을 쓰는 다른 시트 칸(색연필 수식 흐름의 '다른 시트로 보내요' 시험)", muted)
    s.write_formula("G1", "=흐름!B10", None, 3960)

    # --- 표(ListObject) 3개 ---
    s = ws["표"]
    s.write("A1", "표 3개", bold)
    s.add_table("B3:E8", {"name": "매출표", "columns": [{"header": h} for h in ["월", "영업", "기술", "지원"]],
                         "data": [[f"{m}월", 100 + m * 3, 80 + m * 2, 60 + m] for m in range(1, 6)]})
    s.add_table("B11:D14", {"name": "직원표", "columns": [{"header": h} for h in ["이름", "팀", "나이"]],
                           "data": [["김가", "영업", 31], ["이나", "기술", 27], ["박다", "지원", 35]]})
    s.add_table("G3:H12", {"name": "재고표", "columns": [{"header": "품목"}, {"header": "수량"}],
                          "data": [[f"품목{i}", i * 5] for i in range(1, 10)]})

    # --- 유효성 4영역 ---
    s = ws["유효성"]
    s.write("A1", "데이터 유효성 4영역(각 10칸)", bold)
    s.write("B1", "목록"); s.write("D1", "정수 1~100"); s.write("F1", "2026년 날짜"); s.write("H1", "사용자 수식(짝수만)")
    s.data_validation("B2:B11", {"validate": "list", "source": ["예", "아니오", "보류"]})
    s.data_validation("D2:D11", {"validate": "integer", "criteria": "between", "minimum": 1, "maximum": 100})
    s.data_validation("F2:F11", {"validate": "date", "criteria": "between", "minimum": __import__("datetime").date(2026, 1, 1), "maximum": __import__("datetime").date(2026, 12, 31)})
    s.data_validation("H2:H11", {"validate": "custom", "value": "=MOD(H2,2)=0"})

    # --- 조건부 서식 10규칙 ---
    s = ws["조건부"]
    s.write("A1", "조건부 서식 10규칙(열마다 하나)", bold)
    red = wb.add_format({"bg_color": "#FFC7CE"})
    for c in range(10):
        for r in range(2, 12):
            s.write_number(r, c, (r * 7 + c * 13) % 20)
    s.conditional_format("A3:A12", {"type": "cell", "criteria": ">", "value": 10, "format": red})
    s.conditional_format("B3:B12", {"type": "formula", "criteria": "=B3<5", "format": red})
    s.conditional_format("C3:C12", {"type": "3_color_scale"})
    s.conditional_format("D3:D12", {"type": "data_bar"})
    s.conditional_format("E3:E12", {"type": "icon_set", "icon_style": "3_arrows"})
    s.conditional_format("F3:F12", {"type": "top", "value": 3, "format": red})
    s.conditional_format("G3:G12", {"type": "duplicate", "format": red})
    s.conditional_format("H3:H12", {"type": "text", "criteria": "containing", "value": "1", "format": red})
    s.conditional_format("I3:I12", {"type": "blanks", "format": red})
    s.conditional_format("J3:J12", {"type": "errors", "format": red})

    # --- 숨김행열: 행 5,6,10 / 열 D,F ---
    s = ws["숨김행열"]
    s.write("A1", "숨은 행 3개(5, 6, 10행), 숨은 열 2개(D, F열)", bold)
    for r in range(1, 14):
        for c in range(7):
            s.write_number(r, c, r * 10 + c)
    for r in (4, 5, 9):
        s.set_row(r, None, None, {"hidden": True})
    s.set_column("D:D", None, None, {"hidden": True})
    s.set_column("F:F", None, None, {"hidden": True})

    # --- 색깔: 채우기 색·글자 색별 합계 ---
    s = ws["색깔"]
    s.write("A1", "채우기 색(A열)과 글자 색(C열)", bold)
    yellow = wb.add_format({"bg_color": "#FFC000"})
    green = wb.add_format({"bg_color": "#92D050"})
    redfont = wb.add_format({"font_color": "#FF0000"})
    for i, (v, f) in enumerate([(10, yellow), (20, yellow), (30, yellow), (40, yellow), (5, green), (15, green), (25, green), (7, None), (3, None)]):
        s.write_number(1 + i, 0, v, f)
    for i, v in enumerate([100, 200, 300]):
        s.write_number(1 + i, 2, v, redfont)
    for i, v in enumerate([1, 2, 3]):
        s.write_number(4 + i, 2, v)

    # --- 비교: 두 열 다른 줄 5개(3, 5, 7, 8, 10행) ---
    s = ws["비교"]
    s.write("A1", "왼쪽", bold)
    s.write("B1", "오른쪽", bold)
    pairs = [("apple", "apple"), ("banana", "Banana"), (10, 10), (20, 21), (None, None), ("x", None), ("1", 1), ("same", "same"), ("a ", "a"), (12, 12)]
    for i, (x, y) in enumerate(pairs):
        for c, v in enumerate((x, y)):
            if v is None:
                continue
            if isinstance(v, str):
                s.write_string(1 + i, c, v)
            else:
                s.write_number(1 + i, c, v)

    # --- 마지막칸: 데이터는 A1:C5뿐인데 Z100에 서식만 있어 사용 범위가 A1:Z100 ---
    s = ws["마지막칸"]
    s.write_row("A1", ["이름", "수량", "단가"], bold)
    for i in range(2, 6):
        s.write_row(f"A{i}", [f"품목{i - 1}", i, i * 100])
    s.write_blank("Z100", None, wb.add_format({"bg_color": "#EEEEEE"}))

    # --- 값정리: 열마다 한 도구용 가짜 자료 ---
    s = ws["값정리"]
    s.write_row("A1", ["공백", "글자숫자", "반올림", "부호", "붙이기", "수식", "오류", "빈칸", "날짜글자", "병합"], bold)
    for i, v in enumerate(["  가나  ", "다\u00a0라", "마\u200b바", " 사 아 ", "자"]):
        s.write_string(1 + i, 0, v)
    for i, v in enumerate(["1,234", "56", "007", "12345678901234567890", "-8"]):
        s.write_string(1 + i, 1, v)
    for i, v in enumerate([123, 456, 789, 15, 14]):
        s.write_number(1 + i, 2, v)
    for i, v in enumerate([5, -3, 0, 12.5, -7]):
        s.write_number(1 + i, 3, v)
    for i, v in enumerate([101, 102, 103, 104, 105]):
        s.write_number(1 + i, 4, v)
    for i in range(5):
        s.write_formula(1 + i, 5, f"=C{2 + i}*2", None, (123 + i * 333) * 2)
    for i, f in enumerate(["=1/0", "=A2", "=B3+1", "=5", "=VLOOKUP(\"x\",A:A,2,0)"]):
        s.write_formula(1 + i, 6, f)
    for i, v in enumerate(["북", None, None, "남", None]):
        if v:
            s.write_string(1 + i, 7, v)
    for i, v in enumerate(["2026.11.08", "20261109", "2026-11-10", "2026/11/11", "아님"]):
        s.write_string(1 + i, 8, v)
    s.merge_range("J2:J4", "합친 칸", wb.add_format({"border": 1}))
    s.set_column("A:J", 14)
    s.write_row("K1", ["금액", "품목", "입력"], bold)
    for i, v in enumerate([1234, 0, 150000000, 30000000000000]):
        s.write_number(1 + i, 10, v)
    for i, v in enumerate(["품목10", "품목2", "품목1", "품목20"]):
        s.write_string(1 + i, 11, v)
    for i, v in enumerate([5, 6, 7, 8]):
        s.write_number(1 + i, 12, v)
    s.write_formula("M6", "=SUM(M2:M5)", None, 26)

    # --- 시트목록: A열 새 시트 이름, D:E 팀별 나누기용 표 ---
    s = ws["시트목록"]
    s.write("A1", "새 시트 이름", bold)
    for i, v in enumerate(["동쪽", "서쪽", "남쪽", "북쪽"]):
        s.write_string(1 + i, 0, v)
    s.write_row("D1", ["팀", "매출"], bold)
    for i, (tm, v) in enumerate([("가", 10), ("나", 20), ("가", 30), ("다", 40), ("나", 50), ("가", 60), ("다", 70), ("나", 80)]):
        s.write_string(1 + i, 3, tm)
        s.write_number(1 + i, 4, v)

    # --- 필터: 자동 필터 + 필터 적용 중 ---
    s = ws["필터"]
    s.write_row("A1", ["이름", "점수", "팀"])
    data = [(f"사람{i}", (i * 17) % 100, ["가", "나", "다"][i % 3]) for i in range(1, 21)]
    for i, row in enumerate(data, start=1):
        s.write_row(i, 0, row)
    s.autofilter("A1:C21")
    s.filter_column(1, "x >= 50")
    for i, row in enumerate(data, start=1):
        if row[1] < 50:
            s.set_row(i, None, None, {"hidden": True})

    # --- 셀정보: 칸 종류별 ---
    s = ws["셀정보"]
    s.write("A1", "칸 하나씩 골라 보세요(기대는 C열)", bold)
    unl = wb.add_format({"locked": False})
    pct = wb.add_format({"num_format": "0.0%"})
    s.write_number("A2", 1234.5, num1); s.write("C2", "숫자, 서식 #,##0.0", muted)
    s.write("A3", "글자"); s.write("C3", "텍스트", muted)
    s.write_number("A4", 46338, datef); s.write("C4", "날짜, 서식 yyyy-mm-dd", muted)
    s.write_boolean("A5", True); s.write("C5", "참/거짓", muted)
    s.write_formula("A6", "=A2*2", num1, 2469); s.write("C6", "수식 =A2*2, 숫자", muted)
    long_f = "=" + "+".join(["A2"] * 80)
    s.write_formula("A7", long_f, None, 98760); s.write("C7", "긴 수식(200글자 넘으면 앞부분만 …)", muted)
    s.write("C8", "A8은 빈 칸 → 빈 칸", muted)
    s.write_formula("A9", "=1/0", None, "#DIV/0!"); s.write("C9", "오류 값(수식)", muted)
    s.merge_range("A10:B10", "병합된 칸", cell); s.write("C10", "병합됨", muted)
    s.write("A11", "메모 있음"); s.write_comment("A11", "셀 정보 시험용 메모", {"author": "시험"}); s.write("C11", "메모 있음", muted)
    s.write_number("A12", 0.256, pct); s.write("A13", "잠금 해제", unl); s.write("C12", "숫자, 서식 0.0%", muted); s.write("C13", "잠금 해제(보호 시 수정 가능)", muted)
    s.set_column("A:A", 20); s.set_column("C:C", 44)

    # --- 보호됨 ---
    s = ws["보호됨"]
    s.write("A1", "이 시트는 보호되어 있습니다(암호 없음)", bold)
    s.write("A2", "수정 불가"); s.write("B3", "수정 가능", unl)
    s.protect()

    # --- 링크: 외부 링크 1개(뒤에서 XML로 더함) ---
    s = ws["링크"]
    s.write("A1", "외부 링크 1개(원본_연결용.xlsx)", bold)
    s.write_formula("B2", "=[1]원본!$B$2", None, 7)
    s.write_formula("B3", "=[1]원본!$B$3*2", None, 14)
    s.write("C2", "← 다른 파일을 가리키는 수식(원본 파일은 없어도 됩니다)", muted)

    # --- 숨김/아주숨김 ---
    ws["숨김_시트"].write("A1", "숨겨진 시트(사용자가 다시 표시 가능)")
    ws["숨김_시트"].hide()
    ws["아주숨김_시트"].write("A1", "아주 숨김 시트(VBA로만 표시 가능)")
    ws["아주숨김_시트"].hide()

    ws["안내"].activate()
    wb.close()


EXT_XML = ('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>\n'
           '<externalLink xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" '
           'xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">'
           '<externalBook r:id="rId1"><sheetNames><sheetName val="원본"/></sheetNames>'
           '<sheetDataSet><sheetData sheetId="0"><row r="2"><cell r="B2"><v>7</v></cell></row>'
           '<row r="3"><cell r="B3"><v>7</v></cell></row></sheetData></sheetDataSet></externalBook></externalLink>')
EXT_RELS = ('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>\n'
            '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
            '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/externalLinkPath" '
            'Target="원본_연결용.xlsx" TargetMode="External"/></Relationships>')


def patch(src: Path, dst: Path) -> None:
    """외부 링크 부품을 더하고 '아주숨김_시트'를 veryHidden으로 바꾼다."""
    with zipfile.ZipFile(src) as zin, zipfile.ZipFile(dst, "w", zipfile.ZIP_DEFLATED) as zout:
        for item in zin.infolist():
            data = zin.read(item.filename)
            if item.filename == "[Content_Types].xml":
                t = data.decode("utf-8")
                t = t.replace("</Types>", '<Override PartName="/xl/externalLinks/externalLink1.xml" '
                              'ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.externalLink+xml"/></Types>')
                data = t.encode("utf-8")
            elif item.filename == "xl/workbook.xml":
                t = data.decode("utf-8")
                t, n = re.subn(r'(<sheet name="아주숨김_시트"[^>]*?)state="hidden"', r'\1state="veryHidden"', t)
                assert n == 1, "veryHidden 치환 실패"
                assert "</sheets>" in t
                t = t.replace("</sheets>", '</sheets><externalReferences><externalReference r:id="rIdExt1"/></externalReferences>', 1)
                data = t.encode("utf-8")
            elif item.filename == "xl/_rels/workbook.xml.rels":
                t = data.decode("utf-8")
                t = t.replace("</Relationships>", '<Relationship Id="rIdExt1" '
                              'Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/externalLink" '
                              'Target="externalLinks/externalLink1.xml"/></Relationships>')
                data = t.encode("utf-8")
            zout.writestr(item, data)
        zout.writestr("xl/externalLinks/externalLink1.xml", EXT_XML.encode("utf-8"))
        zout.writestr("xl/externalLinks/_rels/externalLink1.xml.rels", EXT_RELS.encode("utf-8"))


def main() -> int:
    OUT.parent.mkdir(parents=True, exist_ok=True)
    tmp = OUT.with_suffix(".tmp.xlsx")
    build(tmp)
    patch(tmp, OUT)
    tmp.unlink()
    print("만들었습니다:", OUT, OUT.stat().st_size, "bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
